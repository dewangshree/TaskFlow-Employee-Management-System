using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Domain.Entities;

namespace TaskFlow.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(u => u.FirstName).HasMaxLength(50).IsRequired();
        builder.Property(u => u.LastName).HasMaxLength(50).IsRequired();
        builder.Property(u => u.Department).HasMaxLength(100).IsRequired();

        // Department filter on the employee list and DISTINCT for the department lookup.
        builder.HasIndex(u => u.Department);
        // Name sort (default employee ordering).
        builder.HasIndex(u => new { u.FirstName, u.LastName });
    }
}
