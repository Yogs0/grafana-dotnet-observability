using Microsoft.EntityFrameworkCore;
using OpenTelemetryExample.Migration.Entities;

namespace OpenTelemetryExample.Migration;

public class LogsDbContext : DbContext
{
    public LogsDbContext(DbContextOptions<LogsDbContext> options) : base(options)
    {
    }

    public DbSet<AuditTrailLog> AuditTrailLogs => Set<AuditTrailLog>();
    public DbSet<ApiLog> ApiLogs => Set<ApiLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditTrailLog>().ToTable("Log_AuditTrail_T");
        modelBuilder.Entity<ApiLog>().ToTable("LogsApi");
    }
}
