using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;

namespace InfoClusMonitor.Api.Services;

public interface IMinioService
{
    string BucketName { get; }
    string BackupsBucketName { get; }
    string ReleasesBucketName { get; }
    string GetPublicUrl(string objectName, string? bucketName = null);
    Task EnsureBucketExistsAsync(string? bucketName = null, CancellationToken ct = default);
    Task EnsurePublicReadBucketAsync(string? bucketName = null, CancellationToken ct = default);
    Task<string> GetPresignedUploadUrlAsync(string objectName, int expirySeconds = 7200, string? bucketName = null);
    Task<string> GetPresignedDownloadUrlAsync(string objectName, int expirySeconds = 7200, string? bucketName = null);
    Task UploadStreamAsync(string objectName, Stream data, long size, string contentType = "application/octet-stream", string? bucketName = null, CancellationToken ct = default);
    Task RemoveObjectAsync(string objectName, string? bucketName = null, CancellationToken ct = default);
    Task<bool> ObjectExistsAsync(string objectName, string? bucketName = null, CancellationToken ct = default);
}

public class MinioService : IMinioService
{
    private readonly IMinioClient _client;
    private readonly ILogger<MinioService> _logger;
    private readonly string _endpoint;
    private readonly string _defaultBucket;
    private readonly string _backupsBucket;
    private readonly string _releasesBucket;

    public string BucketName => _defaultBucket;
    public string BackupsBucketName => _backupsBucket;
    public string ReleasesBucketName => _releasesBucket;

    public MinioService(IConfiguration configuration, ILogger<MinioService> logger)
    {
        _logger = logger;
        var endpoint = configuration["Minio:Endpoint"] ?? "storageapi.mrapy.com";
        var accessKey = configuration["Minio:AccessKey"] ?? "buhoadmin";
        var secretKey = configuration["Minio:SecretKey"] ?? "Maca*2023";
        _defaultBucket = configuration["Minio:BucketName"] ?? "infoclus-transfers";
        _backupsBucket = configuration["Minio:BackupsBucketName"] ?? "copias-de-seguridad";
        _releasesBucket = configuration["Minio:ReleasesBucketName"] ?? "infoclus-releases";
        var useSsl = bool.Parse(configuration["Minio:UseSSL"] ?? "true");

        // Clean endpoint if user provided protocol prefix
        endpoint = endpoint.Replace("https://", "", StringComparison.OrdinalIgnoreCase)
                           .Replace("http://", "", StringComparison.OrdinalIgnoreCase)
                           .TrimEnd('/');

        _endpoint = endpoint;

        var builder = new MinioClient()
            .WithEndpoint(endpoint)
            .WithCredentials(accessKey, secretKey);

        if (useSsl)
        {
            builder = builder.WithSSL();
        }

        _client = builder.Build();
    }

    public string GetPublicUrl(string objectName, string? bucketName = null)
    {
        var targetBucket = bucketName ?? _releasesBucket;
        return $"https://{_endpoint}/{targetBucket}/{objectName.TrimStart('/')}";
    }

    public async Task EnsurePublicReadBucketAsync(string? bucketName = null, CancellationToken ct = default)
    {
        var targetBucket = bucketName ?? _releasesBucket;
        await EnsureBucketExistsAsync(targetBucket, ct);

        try
        {
            var policy = $$"""
            {
              "Version": "2012-10-17",
              "Statement": [
                {
                  "Effect": "Allow",
                  "Principal": {"AWS": ["*"]},
                  "Action": ["s3:GetBucketLocation", "s3:ListBucket"],
                  "Resource": ["arn:aws:s3:::{{targetBucket}}"]
                },
                {
                  "Effect": "Allow",
                  "Principal": {"AWS": ["*"]},
                  "Action": ["s3:GetObject"],
                  "Resource": ["arn:aws:s3:::{{targetBucket}}/*"]
                }
              ]
            }
            """;
            var spArgs = new SetPolicyArgs().WithBucket(targetBucket).WithPolicy(policy);
            await _client.SetPolicyAsync(spArgs, ct);
            _logger.LogInformation("Política de lectura pública aplicada al bucket {Bucket}", targetBucket);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo aplicar la política de lectura pública a {Bucket}: {Message}", targetBucket, ex.Message);
        }
    }

    public async Task EnsureBucketExistsAsync(string? bucketName = null, CancellationToken ct = default)
    {
        var targetBucket = bucketName ?? _defaultBucket;
        try
        {
            var beArgs = new BucketExistsArgs().WithBucket(targetBucket);
            bool found = await _client.BucketExistsAsync(beArgs, ct);
            if (!found)
            {
                _logger.LogInformation("Creando bucket en MinIO: {Bucket}", targetBucket);
                var mbArgs = new MakeBucketArgs().WithBucket(targetBucket);
                await _client.MakeBucketAsync(mbArgs, ct);
                _logger.LogInformation("Bucket {Bucket} creado exitosamente.", targetBucket);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al verificar o crear el bucket {Bucket} en MinIO.", targetBucket);
            throw;
        }
    }

    public async Task<string> GetPresignedUploadUrlAsync(string objectName, int expirySeconds = 7200, string? bucketName = null)
    {
        var targetBucket = bucketName ?? _defaultBucket;
        await EnsureBucketExistsAsync(targetBucket);

        var args = new PresignedPutObjectArgs()
            .WithBucket(targetBucket)
            .WithObject(objectName)
            .WithExpiry(expirySeconds);

        return await _client.PresignedPutObjectAsync(args);
    }

    public async Task<string> GetPresignedDownloadUrlAsync(string objectName, int expirySeconds = 7200, string? bucketName = null)
    {
        var targetBucket = bucketName ?? _defaultBucket;
        var args = new PresignedGetObjectArgs()
            .WithBucket(targetBucket)
            .WithObject(objectName)
            .WithExpiry(expirySeconds);

        return await _client.PresignedGetObjectAsync(args);
    }

    public async Task UploadStreamAsync(string objectName, Stream data, long size, string contentType = "application/octet-stream", string? bucketName = null, CancellationToken ct = default)
    {
        var targetBucket = bucketName ?? _defaultBucket;
        await EnsureBucketExistsAsync(targetBucket, ct);

        var args = new PutObjectArgs()
            .WithBucket(targetBucket)
            .WithObject(objectName)
            .WithStreamData(data)
            .WithObjectSize(size)
            .WithContentType(contentType);

        await _client.PutObjectAsync(args, ct);
        _logger.LogInformation("Archivo {Object} subido a MinIO bucket {Bucket} ({Size} bytes).", objectName, targetBucket, size);
    }

    public async Task RemoveObjectAsync(string objectName, string? bucketName = null, CancellationToken ct = default)
    {
        var targetBucket = bucketName ?? _defaultBucket;
        try
        {
            var args = new RemoveObjectArgs()
                .WithBucket(targetBucket)
                .WithObject(objectName);

            await _client.RemoveObjectAsync(args, ct);
            _logger.LogInformation("Objeto temporal {Object} eliminado de MinIO bucket {Bucket}.", objectName, targetBucket);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo eliminar el objeto temporal {Object} de MinIO: {Message}", objectName, ex.Message);
        }
    }

    public async Task<bool> ObjectExistsAsync(string objectName, string? bucketName = null, CancellationToken ct = default)
    {
        var targetBucket = bucketName ?? _defaultBucket;
        try
        {
            var args = new StatObjectArgs()
                .WithBucket(targetBucket)
                .WithObject(objectName);

            var stat = await _client.StatObjectAsync(args, ct);
            return stat != null && stat.Size > 0;
        }
        catch (ObjectNotFoundException)
        {
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al consultar existencia de {Object} en MinIO.", objectName);
            return false;
        }
    }
}
