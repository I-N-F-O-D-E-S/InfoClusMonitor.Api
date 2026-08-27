using Microsoft.EntityFrameworkCore;
using InfoClusMonitor.Api.Models.Entities;

namespace InfoClusMonitor.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Machine> Machines => Set<Machine>();
    public DbSet<Command> Commands => Set<Command>();
    public DbSet<User> Users => Set<User>();
    public DbSet<FileTransfer> FileTransfers => Set<FileTransfer>();
    public DbSet<MachineBackup> Backups => Set<MachineBackup>();
    public DbSet<ScheduledTask> ScheduledTasks => Set<ScheduledTask>();
    public DbSet<ScheduledTaskExecution> ScheduledTaskExecutions => Set<ScheduledTaskExecution>();

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

        modelBuilder.Entity<FileTransfer>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Id).ValueGeneratedOnAdd();
            entity.HasIndex(t => t.TransferId).IsUnique();
            entity.HasIndex(t => t.SourceMachineId);
            entity.HasIndex(t => t.TargetMachineId);
            entity.HasIndex(t => t.Status);
            entity.HasQueryFilter(t => !t.IsDeleted);
        });

        modelBuilder.Entity<MachineBackup>(entity =>
        {
            entity.HasKey(b => b.Id);
            entity.Property(b => b.Id).ValueGeneratedOnAdd();
            entity.HasIndex(b => b.BackupId).IsUnique();
            entity.HasIndex(b => b.MachineId);
            entity.HasIndex(b => b.Status);
            entity.HasIndex(b => b.CreatedAt);
            entity.HasQueryFilter(b => !b.IsDeleted);
        });

        modelBuilder.Entity<ScheduledTask>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Id).ValueGeneratedOnAdd();
            entity.HasIndex(s => s.TaskId).IsUnique();
            entity.HasIndex(s => s.MachineId);
            entity.HasIndex(s => s.IsEnabled);
            entity.HasIndex(s => s.NextRunAt);
            entity.HasQueryFilter(s => !s.IsDeleted);
        });

        modelBuilder.Entity<ScheduledTaskExecution>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.HasIndex(e => e.ExecutionId).IsUnique();
            entity.HasIndex(e => e.TaskId);
            entity.HasIndex(e => e.MachineId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.StartedAt);
            entity.HasQueryFilter(e => !e.IsDeleted);
        });
    }
}
