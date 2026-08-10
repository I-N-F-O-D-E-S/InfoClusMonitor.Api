using Microsoft.AspNetCore.SignalR;

namespace InfoClusMonitor.Api.Hubs;

public class MachineHub : Hub
{
    public async Task SubscribeToMachine(string machineId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"machine-{machineId}");
    }

    public async Task UnsubscribeFromMachine(string machineId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"machine-{machineId}");
    }

    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "all-machines");
        await base.OnConnectedAsync();
    }
}
