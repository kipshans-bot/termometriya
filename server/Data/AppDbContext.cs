using Microsoft.EntityFrameworkCore;
using Termometriya.Server.Models;

namespace Termometriya.Server.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<ElevatorLine> ElevatorLines => Set<ElevatorLine>();
    public DbSet<Silo> Silos => Set<Silo>();
    public DbSet<Culture> Cultures => Set<Culture>();
    public DbSet<SensorReading> SensorReadings => Set<SensorReading>();
    public DbSet<AlertEvent> AlertEvents => Set<AlertEvent>();
    public DbSet<Thermopendant> Thermopendants => Set<Thermopendant>();
    public DbSet<AlertConfig> AlertConfigs => Set<AlertConfig>();
    public DbSet<PollingConfig> PollingConfigs => Set<PollingConfig>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ElevatorLine>(e =>
        {
            e.HasIndex(x => x.DisplayOrder).IsUnique();
        });

        modelBuilder.Entity<Silo>(e =>
        {
            e.HasOne(x => x.Line).WithMany(x => x.Silos).HasForeignKey(x => x.LineId);
            e.HasOne(x => x.Culture).WithMany(x => x.Silos).HasForeignKey(x => x.CultureId);
            e.HasIndex(x => new { x.LineId, x.Number }).IsUnique();
        });

        modelBuilder.Entity<Thermopendant>(e =>
        {
            e.HasOne(x => x.Silo).WithMany(x => x.Thermopendants).HasForeignKey(x => x.SiloId);
            e.HasIndex(x => new { x.SiloId, x.PositionIndex }).IsUnique();
        });

        modelBuilder.Entity<SensorReading>(e =>
        {
            e.HasOne(x => x.Silo).WithMany(x => x.Readings).HasForeignKey(x => x.SiloId);
            e.HasOne(x => x.Thermopendant).WithMany(x => x.Readings).HasForeignKey(x => x.ThermopendantId);
            e.HasIndex(x => new { x.SiloId, x.Timestamp });
            e.HasIndex(x => new { x.ThermopendantId, x.Timestamp });
        });

        modelBuilder.Entity<AlertEvent>(e =>
        {
            e.HasOne(x => x.Silo).WithMany(x => x.Alerts).HasForeignKey(x => x.SiloId);
            e.HasIndex(x => new { x.SiloId, x.Timestamp, x.IsActive });
        });

        modelBuilder.Entity<AlertConfig>().HasData(new AlertConfig
        {
            Id = 1,
            SoundEnabled = true,
            EmailEnabled = false
        });

        modelBuilder.Entity<PollingConfig>().HasData(new PollingConfig
        {
            Id = 1,
            NormalIntervalSec = 3600,
            ElevatedIntervalSec = 900
        });
    }
}
