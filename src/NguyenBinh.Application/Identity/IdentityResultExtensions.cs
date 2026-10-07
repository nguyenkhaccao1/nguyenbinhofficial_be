using Microsoft.AspNetCore.Identity;
using NguyenBinh.Application.Common.Exceptions;

namespace NguyenBinh.Application.Identity;

internal static class IdentityResultExtensions
{
    /// <summary>Chuyen loi Identity thanh 422 gan voi field; loi trung email → field "email".</summary>
    public static AppException ToValidationException(this IdentityResult result, string defaultField)
    {
        var errors = result.Errors
            .GroupBy(e => e.Code switch
            {
                nameof(IdentityErrorDescriber.DuplicateEmail) or nameof(IdentityErrorDescriber.InvalidEmail)
                    or nameof(IdentityErrorDescriber.DuplicateUserName) => "email",
                nameof(IdentityErrorDescriber.PasswordMismatch) => "currentPassword",
                nameof(IdentityErrorDescriber.DuplicateRoleName) or nameof(IdentityErrorDescriber.InvalidRoleName) => "name",
                _ => defaultField,
            })
            .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());

        return new BusinessValidationException(errors);
    }
}
