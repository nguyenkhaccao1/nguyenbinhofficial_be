using Ganss.Xss;

namespace NguyenBinh.Application.Content.Common;

/// <summary>Lam sach HTML rich text truoc khi luu (chong XSS — muc 44).</summary>
public interface IHtmlSanitizerService
{
    string? Sanitize(string? html);
}

internal sealed class HtmlSanitizerService : IHtmlSanitizerService
{
    private static readonly string[] AllowedIframeHosts =
        ["https://www.youtube.com/embed/", "https://www.youtube-nocookie.com/embed/", "https://player.vimeo.com/video/"];

    private readonly HtmlSanitizer _sanitizer;

    public HtmlSanitizerService()
    {
        _sanitizer = new HtmlSanitizer();
        _sanitizer.AllowedTags.UnionWith(["figure", "figcaption", "iframe", "mark", "s", "u"]);
        _sanitizer.AllowedAttributes.UnionWith(["class", "loading", "allowfullscreen", "frameborder", "allow"]);
        _sanitizer.AllowedSchemes.Add("mailto");
        _sanitizer.AllowedSchemes.Add("tel");
        // Chi cho phep iframe video tu YouTube/Vimeo.
        _sanitizer.PostProcessNode += (_, e) =>
        {
            if (e.Node is AngleSharp.Html.Dom.IHtmlInlineFrameElement frame &&
                !AllowedIframeHosts.Any(h => frame.Source?.StartsWith(h, StringComparison.OrdinalIgnoreCase) == true))
                frame.Remove();
            if (e.Node is AngleSharp.Html.Dom.IHtmlAnchorElement { Target: "_blank" } a)
                a.SetAttribute("rel", "noopener noreferrer");
        };
    }

    public string? Sanitize(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return null;
        var clean = _sanitizer.Sanitize(html).Trim();
        return clean.Length == 0 || clean is "<p></p>" ? null : clean;
    }
}
