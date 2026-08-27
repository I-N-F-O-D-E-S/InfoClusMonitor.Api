using MediatR;
using InfoClusMonitor.Api.Models.Entities;

namespace InfoClusMonitor.Api.Features.Notifications;

public record BackupCreatedNotification(MachineBackup Backup) : INotification;
public record BackupUpdatedNotification(MachineBackup Backup) : INotification;
public record BackupDeletedNotification(string BackupId) : INotification;
