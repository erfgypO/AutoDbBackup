namespace AutoDbBackup;

public class ServerConfig
{
    public string BackupBaseDir { get; set; }
    public string PgDumpPath { get; set; }
    public List<DbServer> DbServers { get; set; }
}