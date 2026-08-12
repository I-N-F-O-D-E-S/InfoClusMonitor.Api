using System.Collections.Concurrent;
using InfoClusMonitor.Api.Models.Dtos;

namespace InfoClusMonitor.Api.Services;

public record RawDownloadResult(string DownloadId, string Status, long SizeBytes, string? Error = null);

public interface IFileBrowseManager
{
    Task<DirectoryContentDto> RequestBrowseAsync(
        string correlationId,
        Func<Task> sendCommandFunc,
        TimeSpan timeout,
        CancellationToken ct = default);

    bool SetResult(string correlationId, DirectoryContentDto result);

    Task<RawDownloadResult> RequestDownloadAsync(
        string correlationId,
        Func<Task> sendCommandFunc,
        TimeSpan timeout,
        CancellationToken ct = default);

    bool SetDownloadResult(string correlationId, RawDownloadResult result);
}

public class FileBrowseManager(ILogger<FileBrowseManager> logger) : IFileBrowseManager
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource<DirectoryContentDto>> _pendingBrowseRequests = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<RawDownloadResult>> _pendingDownloadRequests = new();

    public async Task<DirectoryContentDto> RequestBrowseAsync(
        string correlationId,
        Func<Task> sendCommandFunc,
        TimeSpan timeout,
        CancellationToken ct = default)
    {
        var tcs = new TaskCompletionSource<DirectoryContentDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingBrowseRequests[correlationId] = tcs;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);

        using var reg = cts.Token.Register(() =>
        {
            if (_pendingBrowseRequests.TryRemove(correlationId, out var removedTcs))
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
            _pendingBrowseRequests.TryRemove(correlationId, out _);
        }
    }

    public bool SetResult(string correlationId, DirectoryContentDto result)
    {
        if (_pendingBrowseRequests.TryRemove(correlationId, out var tcs))
        {
            return tcs.TrySetResult(result);
        }
        return false;
    }

    public async Task<RawDownloadResult> RequestDownloadAsync(
        string correlationId,
        Func<Task> sendCommandFunc,
        TimeSpan timeout,
        CancellationToken ct = default)
    {
        var tcs = new TaskCompletionSource<RawDownloadResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingDownloadRequests[correlationId] = tcs;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);

        using var reg = cts.Token.Register(() =>
        {
            if (_pendingDownloadRequests.TryRemove(correlationId, out var removedTcs))
            {
                removedTcs.TrySetResult(new RawDownloadResult(
                    DownloadId: correlationId,
                    Status: "Failed",
                    SizeBytes: 0,
                    Error: "Tiempo de espera agotado al preparar la descarga en el servidor remoto."
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
            logger.LogError(ex, "Error al preparar descarga para {CorrelationId}", correlationId);
            return new RawDownloadResult(
                DownloadId: correlationId,
                Status: "Failed",
                SizeBytes: 0,
                Error: ex.Message
            );
        }
        finally
        {
            _pendingDownloadRequests.TryRemove(correlationId, out _);
        }
    }

    public bool SetDownloadResult(string correlationId, RawDownloadResult result)
    {
        if (_pendingDownloadRequests.TryRemove(correlationId, out var tcs))
        {
            return tcs.TrySetResult(result);
        }
        return false;
    }
}
