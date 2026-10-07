using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NguyenBinh.Application.Common.Abstractions;

namespace NguyenBinh.Infrastructure.Storage;

public sealed class ImageKitOptions
{
    public const string Section = "ImageKit";

    /// <summary>vd https://ik.imagekit.io/hflybceh5</summary>
    public string UrlEndpoint { get; set; } = string.Empty;

    public string PublicKey { get; set; } = string.Empty;

    /// <summary>Bi mat — chi dat qua bien moi truong ImageKit__PrivateKey.</summary>
    public string PrivateKey { get; set; } = string.Empty;

    public string UploadUrl { get; set; } = "https://upload.imagekit.io/api/v1/files/upload";
    public string ApiUrl { get; set; } = "https://api.imagekit.io/v1";
}

/// <summary>
/// Luu file public tren ImageKit (CDN). Cay thu muc tren ImageKit khop voi khoa:
/// "nguyenbinhofficial/du-an/perfectkey-workforce/dashboard-k3f9qa.png". Resize + WebP/AVIF do CDN lam qua URL (?tr=).
/// File private (vd dinh kem lead) khong bao gio len CDN — luu o dia qua <see cref="LocalFileStorage"/>.
/// </summary>
internal sealed class ImageKitFileStorage(
    HttpClient http,
    IOptions<ImageKitOptions> options,
    LocalFileStorage privateStorage,
    ILogger<ImageKitFileStorage> logger) : IFileStorage
{
    private ImageKitOptions Options => options.Value;

    public bool SupportsTransformations => true;

    public async Task<StoredFile> SaveAsync(string key, Stream content, bool isPrivate, CancellationToken ct = default)
    {
        if (isPrivate) return await privateStorage.SaveAsync(key, content, true, ct);

        // Dem file ra bo nho/temp de gui lai duoc khi ket noi bi ngat giua chung.
        await using var buffer = await BufferAsync(content, ct);
        var (folder, fileName) = Split(key);

        for (var attempt = 1; ; attempt++)
        {
            buffer.Position = 0;
            using var form = new MultipartFormDataContent();
            var file = new StreamContent(new NonClosingStream(buffer));
            file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            form.Add(file, "file", fileName);
            form.Add(new StringContent(fileName), "fileName");
            form.Add(new StringContent(folder), "folder");
            form.Add(new StringContent("false"), "useUniqueFileName");
            form.Add(new StringContent("false"), "overwriteFile");

            using var request = new HttpRequestMessage(HttpMethod.Post, Options.UploadUrl) { Content = form };
            Authorize(request);

            HttpResponseMessage? response = null;
            try
            {
                response = await http.SendAsync(request, ct);
                if (IsTransient(response.StatusCode) && attempt < MaxAttempts)
                {
                    logger.LogWarning("ImageKit upload {Key} attempt {Attempt} got {Status}, retrying", key, attempt, response.StatusCode);
                    await Task.Delay(RetryDelay(attempt), ct);
                    continue;
                }

                await EnsureSuccessAsync(response, "upload", key, ct);
                var result = await response.Content.ReadFromJsonAsync<UploadResponse>(ct)
                             ?? throw new InvalidOperationException("ImageKit upload trả về rỗng.");
                return new StoredFile(result.FilePath.TrimStart('/'), result.FileId);
            }
            catch (Exception ex) when (attempt < MaxAttempts && !ct.IsCancellationRequested &&
                                       ex is HttpRequestException or IOException or TaskCanceledException)
            {
                logger.LogWarning(ex, "ImageKit upload {Key} attempt {Attempt} failed, retrying", key, attempt);
                await Task.Delay(RetryDelay(attempt), ct);
            }
            finally
            {
                response?.Dispose();
            }
        }
    }

    private const int MaxAttempts = 3;

    private static TimeSpan RetryDelay(int attempt) => TimeSpan.FromMilliseconds(500 * attempt * attempt);

    private static bool IsTransient(System.Net.HttpStatusCode status) =>
        status is System.Net.HttpStatusCode.TooManyRequests or System.Net.HttpStatusCode.RequestTimeout || (int)status >= 500;

    /// <summary>File nho giu trong RAM, file lon (video) ghi tam ra dia.</summary>
    private static async Task<Stream> BufferAsync(Stream content, CancellationToken ct)
    {
        const long memoryLimit = 32 * 1024 * 1024;
        if (content.CanSeek && content.Length <= memoryLimit)
        {
            var ms = new MemoryStream((int)content.Length);
            await content.CopyToAsync(ms, ct);
            return ms;
        }

        var temp = new FileStream(Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite, FileShare.None, 81920,
            FileOptions.DeleteOnClose | FileOptions.Asynchronous);
        await content.CopyToAsync(temp, ct);
        return temp;
    }

    /// <summary>HttpClient dong stream sau moi lan gui — boc lai de dung buffer cho lan thu tiep theo.</summary>
    private sealed class NonClosingStream(Stream inner) : Stream
    {
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => false;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => inner.Position = value; }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct) =>
            inner.ReadAsync(buffer, offset, count, ct);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default) => inner.ReadAsync(buffer, ct);
        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { }
    }

    public async Task<Stream?> OpenReadAsync(string key, bool isPrivate, CancellationToken ct = default)
    {
        if (isPrivate) return await privateStorage.OpenReadAsync(key, true, ct);

        // Ban goc (khong transform) qua CDN; "orig-true" bo qua toi uu tu dong de lay dung file da tai len.
        var response = await http.GetAsync($"{GetPublicUrl(key)}?tr=orig-true", HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("ImageKit read {Key} failed: {Status}", key, response.StatusCode);
            response.Dispose();
            return null;
        }

        return await response.Content.ReadAsStreamAsync(ct);
    }

    public async Task DeleteAsync(string key, string? providerFileId, bool isPrivate, CancellationToken ct = default)
    {
        if (isPrivate)
        {
            await privateStorage.DeleteAsync(key, null, true, ct);
            return;
        }

        if (string.IsNullOrEmpty(providerFileId)) return;
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"{Options.ApiUrl}/files/{providerFileId}");
        Authorize(request);
        using var response = await http.SendAsync(request, ct);
        if (response.StatusCode != System.Net.HttpStatusCode.NotFound)
            await EnsureSuccessAsync(response, "delete", key, ct);
    }

    /// <summary>
    /// Di chuyen = tai ban goc → tai len vi tri moi → xoa file cu. Khong dung API "move" cua ImageKit vi API do
    /// can tinh nang file versioning (khong co o goi mien phi → loi VERSION_LIMIT_EXCEEDED).
    /// </summary>
    public async Task<StoredFile> MoveAsync(string key, string newKey, string? providerFileId, bool isPrivate,
        CancellationToken ct = default)
    {
        if (isPrivate) return await privateStorage.MoveAsync(key, newKey, null, true, ct);

        await using var original = await OpenReadAsync(key, false, ct)
                                   ?? throw new InvalidOperationException($"Không đọc được file gốc {key} trên ImageKit.");
        var moved = await SaveAsync(newKey, original, false, ct);

        try
        {
            await DeleteAsync(key, providerFileId, false, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // File moi da san sang; ban cu con sot lai chi ton dung luong, khong anh huong website.
            logger.LogWarning(ex, "Moved {Key} → {NewKey} but could not delete the old file", key, moved.Key);
        }

        return moved;
    }

    public string GetPublicUrl(string key) => $"{Options.UrlEndpoint.TrimEnd('/')}/{key}";

    public string? GetTransformedUrl(string key, int width, string format) =>
        $"{GetPublicUrl(key)}?tr=w-{width},f-{format}";

    private void Authorize(HttpRequestMessage request)
    {
        if (string.IsNullOrWhiteSpace(Options.PrivateKey))
            throw new InvalidOperationException("Thiếu ImageKit__PrivateKey — không thể tải file lên ImageKit.");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes(Options.PrivateKey + ":")));
    }

    private async Task EnsureSuccessAsync(HttpResponseMessage response, string operation, string key, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;
        var body = await response.Content.ReadAsStringAsync(ct);
        logger.LogError("ImageKit {Operation} {Key} failed: {Status} {Body}", operation, key, (int)response.StatusCode,
            body.Length > 500 ? body[..500] : body);
        throw new InvalidOperationException($"Lưu trữ ảnh (ImageKit) lỗi khi {operation}: HTTP {(int)response.StatusCode}.");
    }

    /// <summary>"a/b/c/file.png" → ("/a/b/c", "file.png").</summary>
    private static (string Folder, string FileName) Split(string key)
    {
        var slash = key.LastIndexOf('/');
        return slash < 0 ? ("/", key) : ("/" + key[..slash], key[(slash + 1)..]);
    }

    private sealed record UploadResponse(
        [property: JsonPropertyName("fileId")] string FileId,
        [property: JsonPropertyName("filePath")] string FilePath);
}
