using FluentValidation;

namespace NguyenBinh.Application.Identity.Auth;

public sealed class AuthOptions
{
    public const string Section = "Auth";

    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 7;
    public int PersistentRefreshTokenDays { get; set; } = 30;
}

public sealed record LoginRequest(string Email, string Password, bool RememberMe = false);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record UpdateProfileRequest(string FullName, Guid? AvatarMediaId);

public sealed record CurrentUserDto(
    Guid Id,
    string Email,
    string FullName,
    Guid? AvatarMediaId,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);

/// <summary>Ket qua dang nhap/refresh. RefreshToken chi dat vao cookie httpOnly, khong tra trong body.</summary>
public sealed record AuthSession(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    bool IsPersistent,
    CurrentUserDto User);

internal sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().WithMessage("Vui lòng nhập email.")
            .EmailAddress().WithMessage("Email không hợp lệ.").MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty().WithMessage("Vui lòng nhập mật khẩu.").MaximumLength(128);
    }
}

internal sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().WithMessage("Vui lòng nhập mật khẩu hiện tại.");
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(10)
            .WithMessage("Mật khẩu mới tối thiểu 10 ký tự.").MaximumLength(128)
            .NotEqual(x => x.CurrentPassword).WithMessage("Mật khẩu mới phải khác mật khẩu hiện tại.");
    }
}

internal sealed class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().WithMessage("Vui lòng nhập họ tên.").MaximumLength(150);
    }
}
