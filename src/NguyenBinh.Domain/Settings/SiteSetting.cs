using NguyenBinh.Domain.Common;

namespace NguyenBinh.Domain.Settings;

/// <summary>
/// Mot nhom cau hinh website (brand, theme, contact, social, tracking, seo...) luu dang JSON.
/// Schema typed cua tung nhom nam o Application/Settings.
/// </summary>
public class SiteSetting : AuditableEntity
{
    public string Key { get; set; } = string.Empty;
    public string ValueJson { get; set; } = "{}";
    public bool IsPublic { get; set; }
}
