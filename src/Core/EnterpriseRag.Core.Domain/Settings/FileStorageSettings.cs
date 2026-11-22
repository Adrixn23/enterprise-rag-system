namespace EnterpriseRag.Core.Domain.Settings;

public class FileStorageSettings
{
    public const string SectionName = "FileStorage";

    public string Provider { get; set; } = "Local";
    public string? BasePath { get; set; }
    public string ServiceUrl { get; set; } = "http://localhost:9000";
    public string BucketName { get; set; } = "enterprise-rag-documents";
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string Region { get; set; } = "us-east-1";
}
