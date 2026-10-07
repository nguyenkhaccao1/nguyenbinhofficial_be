using FluentValidation;
using NguyenBinh.Application.Common.Paging;

namespace NguyenBinh.Application.Identity.Users;

public sealed class UserListQuery : PageQuery
{
    public string? Role { get; set; }
    public bool? IsActive { get; set; }
}

public sealed record UserListItemDto(
    Guid Id,
    string Email,
    string FullName,
    IReadOnlyList<string> Roles,
    bool IsActive,
    bool IsLockedOut,
    DateTimeOffset? LastLoginAt,
    DateTimeOffset CreatedAt);

public sealed record UserDetailDto(
    Guid Id,
    string Email,
    string FullName,
    string? PhoneNumber,
    Guid? AvatarMediaId,
    IReadOnlyList<string> Roles,
    bool IsActive,
    bool IsLockedOut,
    DateTimeOffset? LockoutEnd,
    DateTimeOffset? LastLoginAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record CreateUserRequest(
    string Email,
    string FullName,
    string Password,
    string? PhoneNumber,
    IReadOnlyList<string> Roles,
    bool IsActive = true);

public sealed record UpdateUserRequest(
    string FullName,
    string? PhoneNumber,
    IReadOnlyList<string> Roles,
    bool IsActive);

public sealed record ResetPasswordRequest(string NewPassword);

internal sealed class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().WithMessage("Vui lòng nhập email.")
            .EmailAddress().WithMessage("Email không hợp lệ.").MaximumLength(256);
        RuleFor(x => x.FullName).NotEmpty().WithMessage("Vui lòng nhập họ tên.").MaximumLength(150);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(10).WithMessage("Mật khẩu tối thiểu 10 ký tự.")
            .MaximumLength(128);
        RuleFor(x => x.PhoneNumber).MaximumLength(30);
        RuleFor(x => x.Roles).NotEmpty().WithMessage("Chọn ít nhất 1 vai trò.");
    }
}

internal sealed class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().WithMessage("Vui lòng nhập họ tên.").MaximumLength(150);
        RuleFor(x => x.PhoneNumber).MaximumLength(30);
        RuleFor(x => x.Roles).NotEmpty().WithMessage("Chọn ít nhất 1 vai trò.");
    }
}

internal sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(10).WithMessage("Mật khẩu tối thiểu 10 ký tự.")
            .MaximumLength(128);
    }
}
