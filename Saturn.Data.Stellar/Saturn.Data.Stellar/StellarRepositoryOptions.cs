namespace Saturn.Data.Stellar;

public class StellarRepositoryOptions
{
    public string BaseDirectory { get; set; }
    public string DatabaseName { get; set; }
    public bool IsCompressed { get; set; }
    public bool IsEncrypted { get; set; }
    public string EncryptionKey { get; set; }
    public int MaxDegreeOfParallelism { get; set; } = Environment.ProcessorCount;
}