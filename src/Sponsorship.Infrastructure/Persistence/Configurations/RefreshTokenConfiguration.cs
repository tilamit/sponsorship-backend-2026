using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sponsorship.Domain.Entities;

namespace Sponsorship.Infrastructure.Persistence.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.ToTable("RefreshTokens");
        b.HasKey(x => x.Id);
        b.Property(x => x.Token).IsRequired().HasMaxLength(500);
        b.HasIndex(x => x.Token).IsUnique();
        b.Property(x => x.ReplacedByToken).HasMaxLength(500);
        b.Property(x => x.CreatedAt).HasDefaultValueSql("sysutcdatetime()");

        b.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
