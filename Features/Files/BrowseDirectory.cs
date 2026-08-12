using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using InfoClusMonitor.Api.Data;
using InfoClusMonitor.Api.Models.Entities;
using InfoClusMonitor.Api.Commons.Enums;
using InfoClusMonitor.Api.Models.Dtos;
using InfoClusMonitor.Api.Services;
using InfoClusMonitor.Api.Features.Notifications;

namespace InfoClusMonitor.Api.Features.Files;

public record BrowseDirectoryQuery(string MachineId, string Path) : IRequest<DirectoryContentDto>;

public class BrowseDirectoryHandler(
    AppDbContext db,
    IRabbitMqService rabbit,
    IFileBrowseManager browseManager,
    IMediator mediator) : IRequestHandler<BrowseDirectoryQuery, DirectoryContentDto>
{
    public async Task<DirectoryContentDto> Handle(BrowseDirectoryQuery query, CancellationToken ct)
    {
        long.TryParse(query.MachineId, out var numericId);

        var machine = await db.Machines
            .FirstOrDefaultAsync(m => m.ExternalMachineId == query.MachineId || (numericId > 0 && m.Id == numericId), ct);

        if (machine is null)
        {
            return new DirectoryContentDto(
                CurrentPath: query.Path,
                ParentPath: null,
                Items: [],
                Error: "Servidor no encontrado."
            );
        }

        if (machine.Status != MachineStatus.Online)
        {
            return new DirectoryContentDto(
                CurrentPath: query.Path,
                ParentPath: null,
                Items: [],
                Error: "El servidor se encuentra fuera de línea."
            );
        }

        var correlationId = Guid.NewGuid().ToString();
        var requestedPath = string.IsNullOrWhiteSpace(query.Path) ? "/" : query.Path.Trim();

        var result = await browseManager.RequestBrowseAsync(
            correlationId,
            async () =>
            {
                await rabbit.SendCustomCommandAsync(
                    machine.ExternalMachineId,
                    correlationId,
                    "BrowseFiles",
                    new { path = requestedPath }
                );
            },
            timeout: TimeSpan.FromSeconds(8),
            ct
        );

        if (result.Error == null)
        {
            await mediator.Publish(new DirectoryLoadedNotification(machine.ExternalMachineId, result), ct);
        }

        return result;
    }
}
