using GoLive.Saturn.Data.Entities;
using MessagePack;
using MessagePack.Formatters;

public class EncryptedStringFormatter : IMessagePackFormatter<EncryptedString>
{
    public void Serialize(ref MessagePackWriter writer, EncryptedString value, MessagePackSerializerOptions options)
    {
        if (value == null || (string.IsNullOrEmpty(value.Encoded) && string.IsNullOrEmpty(value.Hash) && string.IsNullOrEmpty(value.Salt) && string.IsNullOrEmpty(value.Decoded)))
        {
            writer.WriteNil();
            return;
        }

        // Generate salt if not present
        if (string.IsNullOrEmpty(value.Salt))
        {
            value.Salt = GoLive.Saturn.Crypto.Random.GetRandomString(32);
        }

        // Encrypt the decoded value if present
        if (!string.IsNullOrWhiteSpace(value.Decoded))
        {
            value.Encoded = GoLive.Saturn.Crypto.Encryption.EncryptStringAES(value.Decoded, GoLive.Saturn.Crypto.CryptoSingleton.Instance.MasterEncryptionKey, value.Salt);
            value.Hash = GoLive.Saturn.Crypto.Hash.CalculateSHA512(value.Decoded);
        }

        // Write as a map with 3 properties
        writer.WriteMapHeader(3);
        
        writer.Write("Encoded");
        writer.Write(value.Encoded);
        
        writer.Write("Hash");
        writer.Write(value.Hash);
        
        writer.Write("Salt");
        writer.Write(value.Salt);
    }

    public EncryptedString Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        if (reader.TryReadNil())
        {
            return null;
        }

        var mapLength = reader.ReadMapHeader();
        var item = new EncryptedString();

        for (int i = 0; i < mapLength; i++)
        {
            var key = reader.ReadString();
            switch (key)
            {
                case "Encoded":
                    item.Encoded = reader.ReadString();
                    break;
                case "Hash":
                    item.Hash = reader.ReadString();
                    break;
                case "Salt":
                    item.Salt = reader.ReadString();
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        // Decrypt if we have the necessary data
        if (!string.IsNullOrWhiteSpace(item.Encoded) && !string.IsNullOrWhiteSpace(item.Salt))
        {
            item.Decoded = GoLive.Saturn.Crypto.Encryption.DecryptStringAES(item.Encoded, GoLive.Saturn.Crypto.CryptoSingleton.Instance.MasterEncryptionKey, item.Salt);
            item.Populated = true;
        }

        return item;
    }
}