using Microsoft.EntityFrameworkCore;

namespace Rochambeau.Api.Common.Persistence;

/// <summary>
/// The single EF Core context (ADR-0003). Each module contributes
/// <see cref="IEntityTypeConfiguration{TEntity}"/> classes that map to its own schema.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
