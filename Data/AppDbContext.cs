using Microsoft.EntityFrameworkCore;
using InfoClusMonitor.Api.Models;

namespace InfoClusMonitor.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Machine> Machines => Set<Machine>();
    public DbSet<Command> Commands => Set<Command>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Machine>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.HasIndex(m => m.Hostname);
            entity.HasIndex(m => m.Status);
        });

        modelBuilder.Entity<Command>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.HasIndex(c => c.MachineId);
            entity.HasIndex(c => c.Status);
            entity.HasIndex(c => c.Type);

            entity.HasOne(c => c.Machine)
                  .WithMany(m => m.Commands)
                  .HasForeignKey(c => c.MachineId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
