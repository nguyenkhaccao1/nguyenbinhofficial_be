using NguyenBinh.Application.Settings;
using NguyenBinh.Domain.Common;

namespace NguyenBinh.UnitTests.Settings;

public class SettingValidatorTests
{
    [Fact]
    public void Theme_requires_hex_colors()
    {
        var validator = new ThemeSettingsValidator();
        validator.Validate(new ThemeSettings()).IsValid.Should().BeTrue();
        validator.Validate(new ThemeSettings { PrimaryColor = "blue" }).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("G-ABC123XYZ", "GTM-AB12CD", "123456789", true)]
    [InlineData("UA-12345", null, null, false)]
    [InlineData(null, "GTM_123", null, false)]
    [InlineData(null, null, "pixel", false)]
    [InlineData(null, null, null, true)]
    public void Tracking_ids_have_expected_format(string? ga4, string? gtm, string? pixel, bool valid)
    {
        var result = new TrackingSettingsValidator().Validate(new TrackingSettings
        {
            Ga4MeasurementId = ga4, GtmContainerId = gtm, MetaPixelId = pixel,
        });
        result.IsValid.Should().Be(valid);
    }

    [Fact]
    public void Seo_title_template_must_contain_placeholder_and_site_url_has_no_trailing_slash()
    {
        var validator = new SeoSettingsValidator();
        validator.Validate(new SeoSettings()).IsValid.Should().BeTrue();
        validator.Validate(new SeoSettings { TitleTemplate = "Nguyên Bình" }).IsValid.Should().BeFalse();
        validator.Validate(new SeoSettings { SiteUrl = "https://nguyenbinhofficial.com.vn/" }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Contact_urls_must_be_http()
    {
        var validator = new ContactSettingsValidator();
        validator.Validate(new ContactSettings { ZaloUrl = "https://zalo.me/0900000000" }).IsValid.Should().BeTrue();
        validator.Validate(new ContactSettings { ZaloUrl = "javascript:alert(1)" }).IsValid.Should().BeFalse();
    }
}

public class ContentEntityTests
{
    private sealed class Sample : ContentEntity;

    private static readonly DateTimeOffset Now = new(2026, 10, 7, 9, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(ContentStatus.Draft, null, false)]
    [InlineData(ContentStatus.Published, null, true)]
    [InlineData(ContentStatus.Unpublished, null, false)]
    [InlineData(ContentStatus.Scheduled, -1, true)]
    [InlineData(ContentStatus.Scheduled, 1, false)]
    public void IsPublicAt_respects_status_and_schedule(ContentStatus status, int? publishOffsetHours, bool expected)
    {
        var entity = new Sample
        {
            Status = status,
            PublishAt = publishOffsetHours is { } h ? Now.AddHours(h) : null,
        };
        entity.IsPublicAt(Now).Should().Be(expected);
    }

    [Fact]
    public void Deleted_content_is_never_public() =>
        new Sample { Status = ContentStatus.Published, IsDeleted = true }.IsPublicAt(Now).Should().BeFalse();
}
