using NguyenBinh.Shared.Text;

namespace NguyenBinh.UnitTests.Shared;

public class SlugTests
{
    [Theory]
    [InlineData("POS Nguyên Bình", "pos-nguyen-binh")]
    [InlineData("Phần mềm quản lý khách sạn", "phan-mem-quan-ly-khach-san")]
    [InlineData("Đặt món & Thanh toán", "dat-mon-thanh-toan")]
    [InlineData("  ASP.NET   Core 9 ", "asp-net-core-9")]
    [InlineData("Cơm Thị Nở!!!", "com-thi-no")]
    [InlineData("---", "")]
    [InlineData("", "")]
    public void From_converts_vietnamese_text(string input, string expected) =>
        Slug.From(input).Should().Be(expected);

    [Fact]
    public void From_truncates_without_trailing_dash()
    {
        var slug = Slug.From(new string('a', 10) + " " + new string('b', 10), maxLength: 11);
        slug.Should().Be("aaaaaaaaaa");
    }

    [Theory]
    [InlineData("pos-nguyen-binh", true)]
    [InlineData("POS-nguyen", false)]
    [InlineData("pos--nguyen", false)]
    [InlineData("-pos", false)]
    [InlineData("", false)]
    public void IsValid(string slug, bool expected) => Slug.IsValid(slug).Should().Be(expected);
}
