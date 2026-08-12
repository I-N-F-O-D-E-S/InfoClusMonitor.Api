using MediatR;
using InfoClusMonitor.Api.Models.Entities;
using InfoClusMonitor.Api.Models.Dtos;

namespace InfoClusMonitor.Api.Features.Notifications;

public record TransferCreatedNotification(FileTransfer Transfer) : INotification;
public record TransferUpdatedNotification(FileTransfer Transfer) : INotification;
public record DirectoryLoadedNotification(string MachineId, DirectoryContentDto Content) : INotification;
