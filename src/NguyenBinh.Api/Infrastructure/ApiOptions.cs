namespace NguyenBinh.Api.Infrastructure;

public sealed class RefreshCookieOptions
{
    public const string Section = "Auth:RefreshCookie";
    public const string AntiForgeryHeader = "X-Requested-With";
    public const string AntiForgeryValue = "nb-admin";

    public string Name { get; set; } = "nb_rt";
    public string Path { get; set; } = "/api/v1/admin/auth";

    /// <summary>Luon true o moi truong that; chi tat cho test chay tren http.</summary>
    public bool Secure { get; set; } = true;
}

public static class RateLimitPolicies
{
    public const string Auth = "auth";
    public const string PublicForms = "public-forms";
    public const string PublicApi = "public-api";
}
