using GoLive.Saturn.Data.Entities;
using MessagePack;
using MessagePack.Formatters;

namespace Saturn.Data.Stellar.Resolvers;

public class HashedStringFormatter : IMessagePackFormatter<HashedString>
{
    public void Serialize(ref MessagePackWriter writer, HashedString value, MessagePackSerializerOptions options)
    {
        if (value == null || (string.IsNullOrEmpty(value.Hash) && string.IsNullOrEmpty(value.Salt) && string.IsNullOrEmpty(value.Decoded)))
        {
            writer.WriteNil();
            return;
        }

        // Generate salt if not present
        if (string.IsNullOrEmpty(value.Salt))
        {
            value.Salt = GoLive.Saturn.Crypto.Random.GetRandomString(32);
        }

        // Hash the decoded value if present
        if (!string.IsNullOrWhiteSpace(value.Decoded))
        {
            value.Hash = GoLive.Saturn.Crypto.Hash.CalculateSHA512(value.Decoded + value.Salt);
        }
        else if (string.IsNullOrWhiteSpace(value.Hash))
        {
            writer.WriteNil();
            return;
        }

        // Write as a map with 2 properties (Hash and Salt only)
        writer.WriteMapHeader(2);
            
        writer.Write("Hash");
        writer.Write(value.Hash);
            
        writer.Write("Salt");
        writer.Write(value.Salt);
    }

    public HashedString Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        if (reader.TryReadNil())
        {
            return null;
        }

        var mapLength = reader.ReadMapHeader();
        var item = new HashedString();

        for (int i = 0; i < mapLength; i++)
        {
            var key = reader.ReadString();
            switch (key)
            {
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

        // Set populated if we have the necessary data
        if (!string.IsNullOrWhiteSpace(item.Hash) && !string.IsNullOrWhiteSpace(item.Salt))
        {
            item.Populated = true;
        }

        return item;
    }
}