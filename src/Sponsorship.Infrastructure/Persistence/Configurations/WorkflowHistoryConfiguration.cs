using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sponsorship.Domain.Entities;

namespace Sponsorship.Infrastructure.Persistence.Configurations;

public class WorkflowHistoryConfiguration : IEntityTypeConfiguration<WorkflowHistory>
{
    public void Configure(EntityTypeBuilder<WorkflowHistory> b)
    {
        b.ToTable("WorkflowHistory");
        b.HasKey(x => x.Id);
        b.Property(x => x.Action).HasConversion<int>();
        b.Property(x => x.FromStatus).HasConversion<int>();
        b.Property(x => x.ToStatus).HasConversion<int>();
        b.Property(x => x.Remarks).HasMaxLength(1000);
        b.Property(x => x.ActionAt).HasDefaultValueSql("sysutcdatetime()");

        b.HasOne(x => x.ActionBy)
            .WithMany()
            .HasForeignKey(x => x.ActionById)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.RequestId);
    }
}
