using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using NguyenBinh.Application.Common.Abstractions;
using NguyenBinh.Application.Identity.Auth;
using NguyenBinh.Domain.Identity;

namespace NguyenBinh.Infrastructure.Auth;

public sealed class JwtOptions
{
    public const string Section = "Jwt";

    public string Issuer { get; set; } = "nguyenbinh-official";
    public string Audience { get; set; } = "nguyenbinh-admin";

    /// <summary>Khoa HMAC ≥ 32 byte. Bat buoc cau hinh qua secret/bien moi truong (Jwt__Secret).</summary>
    public string Secret { get; set; } = string.Empty;

    public SymmetricSecurityKey SigningKey()
    {
        var bytes = Encoding.UTF8.GetBytes(Secret);
        if (bytes.Length < 32)
            throw new InvalidOperationException("Jwt:Secret phải dài tối thiểu 32 byte (cấu hình qua biến môi trường Jwt__Secret).");
        return new SymmetricSecurityKey(bytes);
    }
}

public static class AppClaims
{
    public const string Subject = JwtRegisteredClaimNames.Sub;
    public const string Email = JwtRegisteredClaimNames.Email;
    public const string Name = "name";
    public const string Role = "role";
    public const string SecurityStamp = "sstamp";
}

internal sealed class JwtTokenService(IOptions<JwtOptions> jwtOptions, IOptions<AuthOptions> authOptions,
    TimeProvider clock) : ITokenService
{
    private readonly JsonWebTokenHandler _handler = new();

    public AccessToken CreateAccessToken(AppUser user, IReadOnlyCollection<string> roles)
    {
        var jwt = jwtOptions.Value;
        var now = clock.GetUtcNow();
        var expires = now.AddMinutes(authOptions.Value.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(AppClaims.Subject, user.Id.ToString()),
            new(AppClaims.Email, user.Email ?? string.Empty),
            new(AppClaims.Name, user.FullName),
            new(AppClaims.SecurityStamp, user.SecurityStamp ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
        };
        claims.AddRange(roles.Select(r => new Claim(AppClaims.Role, r)));

        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            SigningCredentials = new SigningCredentials(jwt.SigningKey(), SecurityAlgorithms.HmacSha256),
        });

        return new AccessToken(token, expires);
    }
}
