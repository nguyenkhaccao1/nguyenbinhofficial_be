namespace NguyenBinh.Application.Common.Exceptions;

/// <summary>
/// Loi nghiep vu co kieu. GlobalExceptionMiddleware chuyen thanh status code tuong ung,
/// nen service chi can throw, khong phai biet ve HTTP.
/// </summary>
public abstract class AppException(string message) : Exception(message)
{
    /// <summary>Du lieu bo sung tra ve trong "data" cua response loi (vd danh sach noi dang dung media).</summary>
    public object? Details { get; init; }
}

/// <summary>404</summary>
public sealed class NotFoundException(string message = "Không tìm thấy dữ liệu.") : AppException(message)
{
    public static NotFoundException For(string entity, object id) => new($"Không tìm thấy {entity} ({id}).");
}

/// <summary>409 — trung unique, xung dot RowVersion, xoa thu dang duoc dung...</summary>
public sealed class ConflictException(string message) : AppException(message);

/// <summary>403 — da xac thuc nhung khong duoc phep thao tac nay (ngoai kiem tra permission).</summary>
public sealed class ForbiddenException(string message = "Bạn không có quyền thực hiện thao tác này.")
    : AppException(message);

/// <summary>401 — sai thong tin dang nhap, token khong hop le.</summary>
public sealed class UnauthorizedException(string message = "Phiên đăng nhập không hợp lệ.") : AppException(message);

/// <summary>422 — loi nghiep vu gan voi field (ngoai validator FluentValidation).</summary>
public sealed class BusinessValidationException(IReadOnlyDictionary<string, string[]> errors,
    string message = "Dữ liệu không hợp lệ.") : AppException(message)
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;

    public BusinessValidationException(string field, string error)
        : this(new Dictionary<string, string[]> { [field] = [error] })
    {
    }
}
