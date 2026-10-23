using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


public class QdrantSettings
{
    public const string SectionName = "QdrantSettings";

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 6334; // Puerto gRPC por defecto de Qdrant
    public string? ApiKey { get; set; }
    public string CollectionName { get; set; } = "enterprise-documents";

}