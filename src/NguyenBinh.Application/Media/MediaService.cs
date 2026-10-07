using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NguyenBinh.Application.Common.Abstractions;
using NguyenBinh.Application.Common.Exceptions;
using NguyenBinh.Application.Common.Paging;
using NguyenBinh.Domain.Media;
using NguyenBinh.Shared.Results;

namespace NguyenBinh.Application.Media;

public interface IMediaService
{
    Task<PagedResult<MediaDto>> ListAsync(MediaListQuery query, CancellationToken ct = default);
    Task<MediaDetailDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<UploadResultItem>> UploadAsync(IReadOnlyList<UploadFile> files, Guid? folderId,
        CancellationToken ct = default);
    Task<MediaDto> UpdateAsync(Guid id, UpdateMediaRequest request, CancellationToken ct = default);
    Task<MediaDto> CropAsync(Guid id, CropMediaRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, bool force, CancellationToken ct = default);
    Task BulkAsync(MediaBulkRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<MediaFolderDto>> ListFoldersAsync(CancellationToken ct = default);
    Task<MediaFolderDto> CreateFolderAsync(SaveMediaFolderRequest request, CancellationToken ct = default);
    Task<MediaFolderDto> UpdateFolderAsync(Guid id, SaveMediaFolderRequest request, CancellationToken ct = default);
    Task DeleteFolderAsync(Guid id, CancellationToken ct = default);
}

internal sealed class MediaService(
    IAppDbContext db,
    IFileStorage storage,
    IImageProcessor images,
    IMalwareScanner malwareScanner,
    IMediaProcessingQueue processingQueue,
    TimeProvider clock,
    ILogger<MediaService> logger) : IMediaService
{
    private static readonly SortMap<MediaFile> Sorts = new SortMap<MediaFile>()
        .Add("fileName", m => m.FileName)
        .Add("sizeBytes", m => m.SizeBytes)
        .Add("createdAt", m => m.CreatedAt)
        .Add("updatedAt", m => m.UpdatedAt);

    public async Task<PagedResult<MediaDto>> ListAsync(MediaListQuery query, CancellationToken ct = default)
    {
        var media = db.MediaFiles.AsNoTracking().Where(m => !m.IsPrivate);

        if (query.FolderId is { } folderId) media = media.Where(m => m.FolderId == folderId);
        else if (query.RootOnly) media = media.Where(m => m.FolderId == null);
        if (query.Kind is { } kind) media = media.Where(m => m.Kind == kind);
        if (!string.IsNullOrWhiteSpace(query.Tag)) media = media.Where(m => m.Tags.Contains(query.Tag));
        if (query.Search is { } q)
            media = media.Where(m => m.FileName.Contains(q) || m.OriginalName.Contains(q) ||
                                     (m.Title != null && m.Title.Contains(q)) || (m.Alt != null && m.Alt.Contains(q)));

        var page = await Sorts.Apply(media, query.Sort, "-createdAt").ToPagedAsync(query, ct);
        var usageCounts = await UsageCountsAsync(page.Items.Select(m => m.Id), ct);
        return page.Map(m => ToDto(m, usageCounts.GetValueOrDefault(m.Id)));
    }

    public async Task<MediaDetailDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var media = await db.MediaFiles.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, ct)
                    ?? throw NotFoundException.For("media", id);
        var usages = await db.MediaUsages.AsNoTracking().Where(u => u.MediaId == id)
            .Select(u => new MediaUsageDto(u.EntityType, u.EntityId, u.Field)).ToListAsync(ct);
        return new MediaDetailDto(ToDto(media, usages.Count), usages);
    }

    public async Task<IReadOnlyList<UploadResultItem>> UploadAsync(IReadOnlyList<UploadFile> files, Guid? folderId,
        CancellationToken ct = default)
    {
        if (folderId is { } fid && !await db.MediaFolders.AnyAsync(f => f.Id == fid, ct))
            throw new BusinessValidationException("folderId", "Thư mục không tồn tại.");

        var results = new List<UploadResultItem>(files.Count);
        foreach (var file in files)
        {
            try
            {
                var media = await UploadOneAsync(file, folderId, ct);
                results.Add(new UploadResultItem(file.FileName, true, ToDto(media, 0), null));
            }
            catch (UploadRejectedException ex)
            {
                results.Add(new UploadResultItem(file.FileName, false, null, ex.Message));
            }
        }

        return results;
    }

    private async Task<MediaFile> UploadOneAsync(UploadFile file, Guid? folderId, CancellationToken ct)
    {
        var originalName = Path.GetFileName(file.FileName);
        var rule = UploadPolicy.Find(originalName)
                   ?? throw new UploadRejectedException(
                       $"Định dạng không được hỗ trợ. Cho phép: {string.Join(", ", UploadPolicy.AllowedExtensions)}.");
        if (file.Length <= 0) throw new UploadRejectedException("File rỗng.");
        if (file.Length > rule.MaxBytes)
            throw new UploadRejectedException($"File vượt quá {rule.MaxBytes / 1024 / 1024}MB.");

        var header = new byte[FileSignature.HeaderLength];
        int headerLength;
        await using (var s = file.OpenReadStream())
            headerLength = await s.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, ct);
        if (!FileSignature.Matches(rule.Extension, header.AsSpan(0, headerLength)))
            throw new UploadRejectedException("Nội dung file không khớp với định dạng.");

        await using (var s = file.OpenReadStream())
            if (!await malwareScanner.IsCleanAsync(s, originalName, ct))
                throw new UploadRejectedException("File không an toàn.");

        var id = Guid.CreateVersion7();
        var now = clock.GetUtcNow();
        var extension = rule.Extension.ToLowerInvariant() == ".jpeg" ? ".jpg" : rule.Extension.ToLowerInvariant();
        var media = new MediaFile
        {
            Id = id,
            FolderId = folderId,
            OriginalName = originalName,
            FileName = Path.GetFileNameWithoutExtension(originalName),
            StorageKey = $"{now:yyyy}/{now:MM}/{id:N}{extension}",
            MimeType = rule.MimeType,
            Extension = extension,
            Kind = rule.Kind,
            SizeBytes = file.Length,
        };

        if (rule.Kind == MediaKind.Image)
        {
            byte[] bytes;
            await using (var s = file.OpenReadStream())
            using (var ms = new MemoryStream((int)file.Length))
            {
                await s.CopyToAsync(ms, ct);
                bytes = ms.ToArray();
            }

            var info = images.Probe(bytes) ?? throw new UploadRejectedException("Không đọc được ảnh (file hỏng?).");
            media.Width = info.Width;
            media.Height = info.Height;
            media.Checksum = Convert.ToHexString(SHA256.HashData(bytes));
            if (UploadPolicy.IsResizableImage(extension))
            {
                media.BlurDataUrl = TryBlur(bytes);
                media.ProcessingState = MediaProcessingState.Pending;
            }

            await storage.SaveAsync(media.StorageKey, new MemoryStream(bytes), false, ct);
        }
        else
        {
            await using (var s = file.OpenReadStream())
                media.Checksum = Convert.ToHexString(await SHA256.HashDataAsync(s, ct));
            await using (var s = file.OpenReadStream())
                await storage.SaveAsync(media.StorageKey, s, false, ct);
        }

        db.MediaFiles.Add(media);
        await db.SaveChangesAsync(ct);

        if (media.ProcessingState == MediaProcessingState.Pending)
            await processingQueue.EnqueueAsync(media.Id, ct);

        return media;
    }

    public async Task<MediaDto> UpdateAsync(Guid id, UpdateMediaRequest request, CancellationToken ct = default)
    {
        var media = await FindAsync(id, ct);
        if (request.FolderId is { } fid && !await db.MediaFolders.AnyAsync(f => f.Id == fid, ct))
            throw new BusinessValidationException("folderId", "Thư mục không tồn tại.");

        media.FileName = request.FileName.Trim();
        media.Title = Clean(request.Title);
        media.Alt = Clean(request.Alt);
        media.Caption = Clean(request.Caption);
        media.Tags = request.Tags?.Select(t => t.Trim()).Where(t => t.Length > 0).Distinct().ToList() ?? [];
        media.FolderId = request.FolderId;
        await db.SaveChangesAsync(ct);

        return ToDto(media, await db.MediaUsages.CountAsync(u => u.MediaId == id, ct));
    }

    public async Task<MediaDto> CropAsync(Guid id, CropMediaRequest request, CancellationToken ct = default)
    {
        var source = await FindAsync(id, ct);
        if (source.Kind != MediaKind.Image || !UploadPolicy.IsResizableImage(source.Extension))
            throw new BusinessValidationException("id", "Chỉ cắt được ảnh JPG, PNG, WebP, AVIF.");
        if (request.X + request.Width > source.Width || request.Y + request.Height > source.Height)
            throw new BusinessValidationException("width", "Vùng cắt vượt ra ngoài ảnh.");

        var bytes = await ReadAllAsync(source, ct);
        var cropped = images.Crop(bytes, request.X, request.Y, request.Width, request.Height,
            source.Extension.TrimStart('.'));

        var now = clock.GetUtcNow();
        var newId = Guid.CreateVersion7();
        var media = new MediaFile
        {
            Id = newId,
            FolderId = source.FolderId,
            OriginalName = source.OriginalName,
            FileName = $"{source.FileName}-crop",
            StorageKey = $"{now:yyyy}/{now:MM}/{newId:N}{source.Extension}",
            MimeType = source.MimeType,
            Extension = source.Extension,
            Kind = MediaKind.Image,
            SizeBytes = cropped.Bytes.LongLength,
            Width = cropped.Width,
            Height = cropped.Height,
            Title = source.Title,
            Alt = source.Alt,
            Caption = source.Caption,
            Tags = [.. source.Tags],
            Checksum = Convert.ToHexString(SHA256.HashData(cropped.Bytes)),
            BlurDataUrl = TryBlur(cropped.Bytes),
            ProcessingState = MediaProcessingState.Pending,
        };

        await storage.SaveAsync(media.StorageKey, new MemoryStream(cropped.Bytes), false, ct);
        db.MediaFiles.Add(media);
        await db.SaveChangesAsync(ct);
        await processingQueue.EnqueueAsync(media.Id, ct);
        return ToDto(media, 0);
    }

    public async Task DeleteAsync(Guid id, bool force, CancellationToken ct = default)
    {
        var media = await FindAsync(id, ct);
        var usages = await db.MediaUsages.Where(u => u.MediaId == id).ToListAsync(ct);
        if (usages.Count > 0 && !force)
            throw new ConflictException($"Media đang được sử dụng ở {usages.Count} nơi. Xác nhận để vẫn xoá.")
            {
                Details = new
                {
                    usages = usages.Select(u => new MediaUsageDto(u.EntityType, u.EntityId, u.Field)).ToList(),
                },
            };

        db.MediaUsages.RemoveRange(usages);
        db.MediaFiles.Remove(media); // soft delete; file vat ly giu lai den khi purge
        await db.SaveChangesAsync(ct);
    }

    public async Task BulkAsync(MediaBulkRequest request, CancellationToken ct = default)
    {
        var ids = request.Ids.Distinct().ToList();
        var items = await db.MediaFiles.Where(m => ids.Contains(m.Id)).ToListAsync(ct);

        if (request.Action == "move")
        {
            if (request.FolderId is { } fid && !await db.MediaFolders.AnyAsync(f => f.Id == fid, ct))
                throw new BusinessValidationException("folderId", "Thư mục không tồn tại.");
            foreach (var m in items) m.FolderId = request.FolderId;
            await db.SaveChangesAsync(ct);
            return;
        }

        var usages = await db.MediaUsages.Where(u => ids.Contains(u.MediaId)).ToListAsync(ct);
        if (usages.Count > 0 && !request.Force)
            throw new ConflictException($"{usages.Select(u => u.MediaId).Distinct().Count()} file đang được sử dụng. Xác nhận để vẫn xoá.")
            {
                Details = new { inUse = usages.Select(u => u.MediaId).Distinct().ToList() },
            };

        db.MediaUsages.RemoveRange(usages);
        db.MediaFiles.RemoveRange(items);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<MediaFolderDto>> ListFoldersAsync(CancellationToken ct = default) =>
        await db.MediaFolders.AsNoTracking().OrderBy(f => f.Name)
            .Select(f => new MediaFolderDto(f.Id, f.Name, f.ParentId, db.MediaFiles.Count(m => m.FolderId == f.Id)))
            .ToListAsync(ct);

    public async Task<MediaFolderDto> CreateFolderAsync(SaveMediaFolderRequest request, CancellationToken ct = default)
    {
        await ValidateFolderAsync(null, request, ct);
        var folder = new MediaFolder { Name = request.Name.Trim(), ParentId = request.ParentId };
        db.MediaFolders.Add(folder);
        await db.SaveChangesAsync(ct);
        return new MediaFolderDto(folder.Id, folder.Name, folder.ParentId, 0);
    }

    public async Task<MediaFolderDto> UpdateFolderAsync(Guid id, SaveMediaFolderRequest request,
        CancellationToken ct = default)
    {
        var folder = await db.MediaFolders.FirstOrDefaultAsync(f => f.Id == id, ct)
                     ?? throw NotFoundException.For("thư mục", id);
        await ValidateFolderAsync(id, request, ct);

        folder.Name = request.Name.Trim();
        folder.ParentId = request.ParentId;
        await db.SaveChangesAsync(ct);
        return new MediaFolderDto(folder.Id, folder.Name, folder.ParentId,
            await db.MediaFiles.CountAsync(m => m.FolderId == id, ct));
    }

    public async Task DeleteFolderAsync(Guid id, CancellationToken ct = default)
    {
        var folder = await db.MediaFolders.FirstOrDefaultAsync(f => f.Id == id, ct)
                     ?? throw NotFoundException.For("thư mục", id);
        if (await db.MediaFolders.AnyAsync(f => f.ParentId == id, ct) ||
            await db.MediaFiles.AnyAsync(m => m.FolderId == id, ct))
            throw new ConflictException("Thư mục không rỗng. Di chuyển hoặc xoá nội dung bên trong trước.");

        db.MediaFolders.Remove(folder);
        await db.SaveChangesAsync(ct);
    }

    private async Task ValidateFolderAsync(Guid? id, SaveMediaFolderRequest request, CancellationToken ct)
    {
        var name = request.Name.Trim();
        if (await db.MediaFolders.AnyAsync(f => f.Id != id && f.ParentId == request.ParentId && f.Name == name, ct))
            throw new ConflictException($"Đã có thư mục '{name}' ở vị trí này.");

        if (request.ParentId is not { } parentId) return;

        // Chong vong lap: thu muc cha khong duoc la chinh no hoac thu muc con cua no.
        var folders = await db.MediaFolders.AsNoTracking().Select(f => new { f.Id, f.ParentId }).ToListAsync(ct);
        if (folders.All(f => f.Id != parentId))
            throw new BusinessValidationException("parentId", "Thư mục cha không tồn tại.");

        Guid? cursor = parentId;
        while (cursor is { } c)
        {
            if (c == id) throw new BusinessValidationException("parentId", "Không thể chuyển thư mục vào chính nó.");
            cursor = folders.First(f => f.Id == c).ParentId;
        }
    }

    private async Task<MediaFile> FindAsync(Guid id, CancellationToken ct) =>
        await db.MediaFiles.FirstOrDefaultAsync(m => m.Id == id, ct) ?? throw NotFoundException.For("media", id);

    private async Task<byte[]> ReadAllAsync(MediaFile media, CancellationToken ct)
    {
        await using var s = await storage.OpenReadAsync(media.StorageKey, media.IsPrivate, ct)
                            ?? throw new NotFoundException("File gốc không còn trong kho lưu trữ.");
        using var ms = new MemoryStream();
        await s.CopyToAsync(ms, ct);
        return ms.ToArray();
    }

    private string? TryBlur(byte[] bytes)
    {
        try
        {
            return images.CreateBlurDataUrl(bytes);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Khong tao duoc blur placeholder");
            return null;
        }
    }

    private async Task<Dictionary<Guid, int>> UsageCountsAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var list = ids.ToList();
        return await db.MediaUsages.Where(u => list.Contains(u.MediaId)).GroupBy(u => u.MediaId)
            .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    internal MediaDto ToDto(MediaFile m, int usageCount) => new(
        m.Id, m.FolderId, m.FileName, m.OriginalName,
        m.IsPrivate ? null : storage.GetPublicUrl(m.StorageKey),
        m.MimeType, m.Extension, m.Kind, m.SizeBytes, m.Width, m.Height, m.Title, m.Alt, m.Caption, m.Tags,
        m.Variants.Select(v => new MediaVariantDto(v.Format, v.Width, v.Height, storage.GetPublicUrl(v.StorageKey),
            v.SizeBytes)).ToList(),
        m.ProcessingState, m.BlurDataUrl, m.IsPrivate, usageCount, m.CreatedAt, m.UpdatedAt);

    private sealed class UploadRejectedException(string message) : Exception(message);
}
