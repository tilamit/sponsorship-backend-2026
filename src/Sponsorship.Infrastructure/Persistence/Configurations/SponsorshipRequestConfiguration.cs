using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sponsorship.Domain.Entities;

namespace Sponsorship.Infrastructure.Persistence.Configurations;

public class SponsorshipRequestConfiguration : IEntityTypeConfiguration<SponsorshipRequest>
{
    public void Configure(EntityTypeBuilder<SponsorshipRequest> b)
    {
        b.ToTable("SponsorshipRequests");
        b.HasKey(x => x.Id);
        b.Property(x => x.Title).IsRequired().HasMaxLength(200);
        b.Property(x => x.Department).IsRequired().HasMaxLength(100);
        b.Property(x => x.EventName).IsRequired().HasMaxLength(200);
        b.Property(x => x.EventDate).HasColumnType("date");
        b.Property(x => x.RequestedAmount).HasColumnType("decimal(18,2)");
        b.Property(x => x.Purpose).IsRequired().HasMaxLength(2000);
        b.Property(x => x.ExpectedBenefit).HasMaxLength(1000);
        b.Property(x => x.Remarks).HasMaxLength(500);
        b.Property(x => x.Status).HasConversion<int>();
        b.Property(x => x.SupportingDocumentPath).HasMaxLength(500);
        b.Property(x => x.CreatedAt).HasDefaultValueSql("sysutcdatetime()");

        b.HasOne(x => x.Requestor)
            .WithMany()
            .HasForeignKey(x => x.RequestorId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.SponsorshipType)
            .WithMany()
            .HasForeignKey(x => x.SponsorshipTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasMany(x => x.History)
            .WithOne()
            .HasForeignKey(h => h.RequestId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Metadata.FindNavigation(nameof(SponsorshipRequest.History))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        b.HasIndex(x => x.RequestorId);
        b.HasIndex(x => x.Status);
    }
}
