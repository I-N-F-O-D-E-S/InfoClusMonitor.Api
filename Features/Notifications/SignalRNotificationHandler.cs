using MediatR;
using Microsoft.AspNetCore.SignalR;
using InfoClusMonitor.Api.Hubs;

namespace InfoClusMonitor.Api.Features.Notifications;

public class SignalRNotificationHandler(IHubContext<MachineHub> hub) :
    INotificationHandler<MachineCreatedNotification>,
    INotificationHandler<MachineUpdatedNotification>,
    INotificationHandler<MachineDeletedNotification>,
    INotificationHandler<CommandCreatedNotification>,
    INotificationHandler<CommandUpdatedNotification>,
    INotificationHandler<TransferCreatedNotification>,
    INotificationHandler<TransferUpdatedNotification>,
    INotificationHandler<DirectoryLoadedNotification>
{
    public async Task Handle(MachineCreatedNotification notification, CancellationToken ct)
    {
        await hub.Clients.Group("all-machines")
            .SendAsync("MachineCreated", notification.Machine, ct);
    }

    public async Task Handle(MachineUpdatedNotification notification, CancellationToken ct)
    {
        await hub.Clients.Group("all-machines")
            .SendAsync("MachineUpdated", notification.Machine, ct);
    }

    public async Task Handle(MachineDeletedNotification notification, CancellationToken ct)
    {
        await hub.Clients.Group("all-machines")
            .SendAsync("MachineDeleted", notification.MachineId, ct);
    }

    public async Task Handle(CommandCreatedNotification notification, CancellationToken ct)
    {
        var cmd = notification.Command;
        await hub.Clients.Group($"machine-{cmd.ExternalMachineId}")
            .SendAsync("CommandCreated", cmd, ct);
        await hub.Clients.Group("all-machines")
            .SendAsync("CommandCreated", cmd, ct);
    }

    public async Task Handle(CommandUpdatedNotification notification, CancellationToken ct)
    {
        var cmd = notification.Command;
        await hub.Clients.Group($"machine-{cmd.ExternalMachineId}")
            .SendAsync("CommandUpdated", cmd, ct);
        await hub.Clients.Group("all-machines")
            .SendAsync("CommandUpdated", cmd, ct);
    }

    public async Task Handle(TransferCreatedNotification notification, CancellationToken ct)
    {
        var transfer = notification.Transfer;
        await hub.Clients.Group("all-machines")
            .SendAsync("TransferCreated", transfer, ct);
        await hub.Clients.Group($"machine-{transfer.SourceMachineId}")
            .SendAsync("TransferCreated", transfer, ct);
        await hub.Clients.Group($"machine-{transfer.TargetMachineId}")
            .SendAsync("TransferCreated", transfer, ct);
    }

    public async Task Handle(TransferUpdatedNotification notification, CancellationToken ct)
    {
        var transfer = notification.Transfer;
        await hub.Clients.Group("all-machines")
            .SendAsync("TransferUpdated", transfer, ct);
        await hub.Clients.Group($"machine-{transfer.SourceMachineId}")
            .SendAsync("TransferUpdated", transfer, ct);
        await hub.Clients.Group($"machine-{transfer.TargetMachineId}")
            .SendAsync("TransferUpdated", transfer, ct);
    }

    public async Task Handle(DirectoryLoadedNotification notification, CancellationToken ct)
    {
        await hub.Clients.Group($"machine-{notification.MachineId}")
            .SendAsync("DirectoryLoaded", notification.Content, ct);
        await hub.Clients.Group("all-machines")
            .SendAsync("DirectoryLoaded", notification.Content, ct);
    }
}
