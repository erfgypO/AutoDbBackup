using System.Collections.Specialized;
using System.Diagnostics;
using Microsoft.Extensions.Options;

namespace AutoDbBackup;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IOptions<ServerConfig> _configuration;
    public Worker(ILogger<Worker> logger, IOptions<ServerConfig> configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var server in _configuration.Value.DbServers)
            {
                await CreateDbBackup(server);
            }
            await Task.Delay(TimeSpan.FromHours(12), stoppingToken);
        }
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
            _logger.LogError("Failed to create backup for {name}, {error}", server.Name, error);
        else
            _logger.LogInformation("Backed up {name}", server.Name);
    }
}