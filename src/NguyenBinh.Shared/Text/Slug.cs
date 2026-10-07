using System.Globalization;
using System.Text;

namespace NguyenBinh.Shared.Text;

/// <summary>Sinh slug URL tu tieng Viet: "Phần mềm POS Nguyên Bình" → "phan-mem-pos-nguyen-binh".</summary>
public static class Slug
{
    public const int MaxLength = 200;

    public static string From(string? input, int maxLength = MaxLength)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        // "đ" khong phai ky tu co dau ghep nen FormD khong tach duoc, phai thay tay.
        var normalized = input.Trim().Replace('đ', 'd').Replace('Đ', 'D').Normalize(NormalizationForm.FormD);

        var sb = new StringBuilder(normalized.Length);
        var lastWasDash = true; // chan dau "-" o dau chuoi
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;

            var lower = char.ToLowerInvariant(c);
            if (lower is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                sb.Append(lower);
                lastWasDash = false;
            }
            else if (!lastWasDash)
            {
                sb.Append('-');
                lastWasDash = true;
            }
        }

        var slug = sb.ToString().TrimEnd('-');
        if (slug.Length > maxLength) slug = slug[..maxLength].TrimEnd('-');
        return slug;
    }

    public static bool IsValid(string? slug) =>
        !string.IsNullOrEmpty(slug) && slug.Length <= MaxLength && From(slug) == slug;
}
