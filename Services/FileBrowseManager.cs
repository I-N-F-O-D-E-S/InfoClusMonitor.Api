using System.Collections.Concurrent;
using InfoClusMonitor.Api.Models.Dtos;

namespace InfoClusMonitor.Api.Services;

public interface IFileBrowseManager
{
    Task<DirectoryContentDto> RequestBrowseAsync(
        string correlationId,
        Func<Task> sendCommandFunc,
        TimeSpan timeout,
        CancellationToken ct = default);

    bool SetResult(string correlationId, DirectoryContentDto result);
}

public class FileBrowseManager(ILogger<FileBrowseManager> logger) : IFileBrowseManager
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource<DirectoryContentDto>> _pendingRequests = new();

    public async Task<DirectoryContentDto> RequestBrowseAsync(
        string correlationId,
        Func<Task> sendCommandFunc,
        TimeSpan timeout,
        CancellationToken ct = default)
    {
        var tcs = new TaskCompletionSource<DirectoryContentDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingRequests[correlationId] = tcs;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);

        using var reg = cts.Token.Register(() =>
        {
            if (_pendingRequests.TryRemove(correlationId, out var removedTcs))
            {
                removedTcs.TrySetResult(new DirectoryContentDto(
                    CurrentPath: "",
                    ParentPath: null,
                    Items: [],
                    Error: "Tiempo de espera agotado al consultar el servidor remoto."
                ));
            }
        });

        try
        {
            await sendCommandFunc();
            return await tcs.Task;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error al solicitar listado de archivos para {CorrelationId}", correlationId);
            return new DirectoryContentDto(
                CurrentPath: "",
                ParentPath: null,
                Items: [],
                Error: ex.Message
            );
        }
        finally
        {
            _pendingRequests.TryRemove(correlationId, out _);
        }
    }

    public bool SetResult(string correlationId, DirectoryContentDto result)
    {
        if (_pendingRequests.TryRemove(correlationId, out var tcs))
        {
            return tcs.TrySetResult(result);
        }
        return false;
    }
}
