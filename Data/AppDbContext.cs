using Microsoft.EntityFrameworkCore;
using InfoClusMonitor.Api.Models.Entities;

namespace InfoClusMonitor.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Machine> Machines => Set<Machine>();
    public DbSet<Command> Commands => Set<Command>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Id).ValueGeneratedOnAdd();
            entity.HasIndex(u => u.UserId).IsUnique();
            entity.HasIndex(u => u.Username).IsUnique();
            entity.HasIndex(u => u.Email).IsUnique();
            entity.HasQueryFilter(u => !u.IsDeleted);
        });

        modelBuilder.Entity<Machine>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Id).ValueGeneratedOnAdd();
            entity.HasIndex(m => m.ExternalMachineId).IsUnique();
            entity.HasIndex(m => m.Hostname);
            entity.HasIndex(m => m.Status);
            entity.HasQueryFilter(m => !m.IsDeleted);
        });

        modelBuilder.Entity<Command>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Id).ValueGeneratedOnAdd();
            entity.HasIndex(c => c.ExternalMachineId);
            entity.HasIndex(c => c.Status);
            entity.HasIndex(c => c.Type);
            entity.HasQueryFilter(c => !c.IsDeleted);

            entity.HasOne(c => c.Machine)
                  .WithMany(m => m.Commands)
                  .HasPrincipalKey(m => m.ExternalMachineId)
                  .HasForeignKey(c => c.ExternalMachineId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
