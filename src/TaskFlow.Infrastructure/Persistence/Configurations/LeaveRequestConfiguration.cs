using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Domain.Entities;

namespace TaskFlow.Infrastructure.Persistence.Configurations;

public sealed class LeaveRequestConfiguration : IEntityTypeConfiguration<LeaveRequest>
{
    public void Configure(EntityTypeBuilder<LeaveRequest> builder)
    {
        builder.ToTable("LeaveRequests", t =>
        {
            t.HasCheckConstraint("CK_LeaveRequests_DateRange", "\"EndDate\" >= \"StartDate\"");
            t.HasCheckConstraint("CK_LeaveRequests_Status", "\"Status\" IN ('Pending','Approved','Rejected')");
            // A decided request must record who decided it.
            t.HasCheckConstraint("CK_LeaveRequests_ReviewedBy",
                "(\"Status\" = 'Pending') OR (\"ReviewedAt\" IS NOT NULL)");
        });

        builder.Property(l => l.LeaveType).HasConversion<string>().HasMaxLength(20);
        builder.Property(l => l.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(l => l.StartDate).HasColumnType("date");
        builder.Property(l => l.EndDate).HasColumnType("date");
        builder.Property(l => l.Reason).HasMaxLength(LeaveRequest.ReasonMaxLength).IsRequired();
        builder.Property(l => l.AdminComment).HasMaxLength(LeaveRequest.CommentMaxLength);
        builder.Property(l => l.Version).IsRowVersion();

        builder.HasOne(l => l.Employee)
            .WithMany(u => u.LeaveRequests)
            .HasForeignKey(l => l.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(l => l.ReviewedBy)
            .WithMany()
            .HasForeignKey(l => l.ReviewedById)
            .OnDelete(DeleteBehavior.SetNull);

        // Employee leave history (WHERE EmployeeId = ? ORDER BY AppliedOn DESC).
        builder.HasIndex(l => new { l.EmployeeId, l.AppliedOn }).IsDescending(false, true);
        // Admin review queue (WHERE Status = 'Pending' ORDER BY AppliedOn DESC) and status counts.
        builder.HasIndex(l => new { l.Status, l.AppliedOn }).IsDescending(false, true);
        // Overlap check on apply and "on leave today" (range predicates on start/end).
        builder.HasIndex(l => new { l.EmployeeId, l.StartDate, l.EndDate });
    }
}
