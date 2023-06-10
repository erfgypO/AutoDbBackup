using System.Collections.Specialized;
using System.Diagnostics;
using Microsoft.Extensions.Options;
using Minio;

namespace AutoDbBackup;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IOptions<ServerConfig> _configuration;
    private readonly IOptions<MinioConfig> _minioConfig;
    private readonly MinioClient _minioClient;
    public Worker(ILogger<Worker> logger, IOptions<ServerConfig> configuration, IOptions<MinioConfig> minioConfig)
    {
        _logger = logger;
        _configuration = configuration;
        _minioConfig = minioConfig;
        _minioClient = new MinioClient()
            .WithEndpoint(_minioConfig.Value.Endpoint)
            .WithCredentials(_minioConfig.Value.AccessKey, _minioConfig.Value.Secret)
            .WithSSL(_minioConfig.Value.Secure)
            .Build();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await SetupBucket();
        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var server in _configuration.Value.DbServers)
            {
                await CreateDbBackup(server);
            }
            await Task.Delay(TimeSpan.FromHours(12), stoppingToken);
        }
    }

    private async Task SetupBucket()
    {
        _logger.LogInformation("Setup S3 Bucket");
        var bucket = _minioConfig.Value.Bucket;

        var bucketExistArgs = new BucketExistsArgs()
            .WithBucket(bucket);

        
        var bucketExists = await _minioClient.BucketExistsAsync(bucketExistArgs);
        if (!bucketExists)
        {
            var makeBucketArgs = new MakeBucketArgs()
                .WithBucket(bucket);
            await _minioClient.MakeBucketAsync(makeBucketArgs);
        }        
        
        var setVersioningArgs = new SetVersioningArgs()
            .WithBucket(bucket)
            .WithVersioningEnabled();

        await _minioClient.SetVersioningAsync(setVersioningArgs);
    }

    private async Task CreateDbBackup(DbServer server)
    {
        _logger.LogInformation("Backing up {name}", server.Name);
        
        var backupName = $"{server.Name}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.backup";
        var backupFile = Path.Combine(_configuration.Value.BackupBaseDir, backupName);

        var process = new Process
        {
            StartInfo = new()
            {
                FileName = _configuration.Value.PgDumpPath,
                Arguments = $"-Fc --inserts -f {backupFile}",
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };
        
        process.StartInfo.EnvironmentVariables.Add("PGHOST", server.Address);
        process.StartInfo.EnvironmentVariables.Add("PGPORT", server.Port.ToString());
        process.StartInfo.EnvironmentVariables.Add("PGDATABASE", server.DbName);        
        process.StartInfo.EnvironmentVariables.Add("PGUSER", server.Username);
        process.StartInfo.EnvironmentVariables.Add("PGPASSWORD", server.Password);

        process.Start();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        if (!string.IsNullOrWhiteSpace(error))
        {
            _logger.LogError("Failed to create backup for {name}, {error}", server.Name, error);
        }
        else
        {
            _logger.LogInformation("Created local backup");
            var bucket = _minioConfig.Value.Bucket;
            _logger.LogInformation("Uploading backup");
            var uploadBackupArgs = new PutObjectArgs()
                .WithBucket(bucket)
                .WithObject($"{server.Name}.backup")
                .WithFileName(backupFile);

            
            await _minioClient.PutObjectAsync(uploadBackupArgs);

            _logger.LogInformation("Uploaded backup");
        }

    }
}