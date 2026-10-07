using NguyenBinh.Application.Media;
using NguyenBinh.Domain.Media;

namespace NguyenBinh.UnitTests.Media;

public class UploadPolicyTests
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0];
    private static readonly byte[] Exe = "MZ\u0090\0 renamed"u8.ToArray();

    [Theory]
    [InlineData("anh.PNG", MediaKind.Image)]
    [InlineData("demo.mp4", MediaKind.Video)]
    [InlineData("ho-so.pdf", MediaKind.Document)]
    public void Find_returns_rule_for_allowed_extensions(string fileName, MediaKind kind) =>
        UploadPolicy.Find(fileName)!.Kind.Should().Be(kind);

    [Theory]
    [InlineData("setup.exe")]
    [InlineData("logo.svg")]
    [InlineData("script.js")]
    [InlineData("run.bat")]
    [InlineData("page.html")]
    [InlineData("noextension")]
    public void Find_rejects_dangerous_or_unknown_types(string fileName) =>
        UploadPolicy.Find(fileName).Should().BeNull();

    [Fact]
    public void Signature_must_match_extension()
    {
        FileSignature.Matches(".png", Png).Should().BeTrue();
        FileSignature.Matches(".jpg", Jpeg).Should().BeTrue();
        FileSignature.Matches(".png", Jpeg).Should().BeFalse();
    }

    [Fact]
    public void Executable_content_is_rejected_whatever_the_extension()
    {
        FileSignature.Matches(".png", Exe).Should().BeFalse();
        FileSignature.Matches(".txt", Exe).Should().BeFalse();
    }

    [Fact]
    public void Text_files_cannot_contain_html_or_binary()
    {
        FileSignature.Matches(".csv", "name,email\na,b"u8).Should().BeTrue();
        FileSignature.Matches(".txt", "<script>alert(1)</script>"u8).Should().BeFalse();
        FileSignature.Matches(".txt", new byte[] { 0x41, 0x00, 0x42 }).Should().BeFalse();
    }

    [Fact]
    public void Webp_and_avif_signatures()
    {
        FileSignature.Matches(".webp", "RIFF\0\0\0\0WEBPVP8 "u8).Should().BeTrue();
        FileSignature.Matches(".avif", "\0\0\0\u001cftypavif\0\0\0\0"u8).Should().BeTrue();
        FileSignature.Matches(".avif", "\0\0\0\u001cftypisom\0\0\0\0"u8).Should().BeFalse();
    }

    [Theory]
    [InlineData(2400, new[] { 320, 640, 960, 1280, 1920 })]
    [InlineData(1400, new[] { 320, 640, 960, 1280, 1400 })]
    [InlineData(500, new[] { 320, 500 })]
    [InlineData(200, new[] { 200 })]
    [InlineData(0, new int[0])]
    public void Variant_widths_never_upscale(int original, int[] expected) =>
        MediaVariantProcessor.TargetWidths(original).Should().Equal(expected);
}
