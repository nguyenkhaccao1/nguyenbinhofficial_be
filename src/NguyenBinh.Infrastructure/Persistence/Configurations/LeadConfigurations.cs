using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NguyenBinh.Domain.Leads;

namespace NguyenBinh.Infrastructure.Persistence.Configurations;

internal sealed class LeadConfiguration : IEntityTypeConfiguration<Lead>
{
    public void Configure(EntityTypeBuilder<Lead> b)
    {
        b.ToTable("Leads");
        b.Property(x => x.FullName).HasMaxLength(120);
        b.Property(x => x.Phone).HasMaxLength(30).IsUnicode(false);
        b.Property(x => x.Email).HasMaxLength(200);
        b.Property(x => x.Company).HasMaxLength(200);
        b.Property(x => x.Need).HasMaxLength(200);
        b.Property(x => x.ProductSlug).HasMaxLength(200).IsUnicode(false);
        b.Property(x => x.ServiceSlug).HasMaxLength(200).IsUnicode(false);
        b.Property(x => x.Message).HasMaxLength(4000);
        b.Property(x => x.PageUrl).HasMaxLength(500);
        b.Property(x => x.Referrer).HasMaxLength(500);
        b.Property(x => x.UtmSource).HasMaxLength(100);
        b.Property(x => x.UtmMedium).HasMaxLength(100);
        b.Property(x => x.UtmCampaign).HasMaxLength(100);
        b.Property(x => x.IpAddress).HasMaxLength(64).IsUnicode(false);
        b.Property(x => x.UserAgent).HasMaxLength(400);
        b.Property(x => x.Note).HasMaxLength(4000);
        b.Property(x => x.NotifyError).HasMaxLength(1000);
        b.HasIndex(x => new { x.Status, x.CreatedAt });
        b.HasIndex(x => x.Phone);
    }
}
