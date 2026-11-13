using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Domain.Entities;

namespace TaskFlow.Infrastructure.Persistence.Configurations;

public sealed class TaskItemConfiguration : IEntityTypeConfiguration<TaskItem>
{
    public void Configure(EntityTypeBuilder<TaskItem> builder)
    {
        builder.ToTable("TaskItems", t =>
        {
            t.HasCheckConstraint("CK_TaskItems_Title_NotBlank", "length(trim(\"Title\")) > 0");
            t.HasCheckConstraint("CK_TaskItems_Status", "\"Status\" IN ('Pending','InProgress','Completed','Cancelled')");
            t.HasCheckConstraint("CK_TaskItems_Priority", "\"Priority\" IN ('Low','Medium','High','Critical')");
        });

        builder.Property(t => t.Title).HasMaxLength(TaskItem.TitleMaxLength).IsRequired();
        builder.Property(t => t.Description).HasMaxLength(TaskItem.DescriptionMaxLength).IsRequired();
        // Enums stored as readable strings: safe against enum reordering and self-describing in SQL.
        builder.Property(t => t.Priority).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.DueDate).HasColumnType("date");

        // PostgreSQL's xmin system column changes on every row update, making it a
        // free optimistic concurrency token (no extra column or trigger needed).
        builder.Property(t => t.Version).IsRowVersion();

        builder.HasOne(t => t.AssignedTo)
            .WithMany(u => u.AssignedTasks)
            .HasForeignKey(t => t.AssignedToId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(t => t.CreatedBy)
            .WithMany(u => u.CreatedTasks)
            .HasForeignKey(t => t.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);

        // "My tasks" list, employee dashboard counts and employee open-task counts.
        builder.HasIndex(t => new { t.AssignedToId, t.Status });
        // Status filter + overdue detection (Status IN (...) AND DueDate < today).
        builder.HasIndex(t => new { t.Status, t.DueDate });
        // Default admin list ordering and "recent tasks" on the dashboard.
        builder.HasIndex(t => t.CreatedAt).IsDescending();
    }
}
