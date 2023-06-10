namespace AutoDbBackup;

public class MinioConfig
{
    public string Endpoint { get; set; }
    public string AccessKey { get; set; }
    public string Secret { get; set; }
    public bool Secure { get; set; }
    public string Bucket { get; set; }
}