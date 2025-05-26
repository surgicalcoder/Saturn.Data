using System.Reflection;
using GoLive.Saturn.Data.Entities;
using LiteDB;

namespace Saturn.Data.LiteDb;

public class CustomEntityMapper : BsonMapper
{
    public CustomEntityMapper(Func<Type, object> customTypeInstantiator = null, ITypeNameBinder typeNameBinder = null) : base(customTypeInstantiator, typeNameBinder)
    {
        RegisterType<HashedString>(serialize: (HashedString value) =>
            {
                if (value == null || (string.IsNullOrEmpty(value.Hash) &&
                                      string.IsNullOrEmpty(value.Salt) &&
                                      string.IsNullOrEmpty(value.Decoded)))
                {
                    return BsonValue.Null;
                }
                
                if (string.IsNullOrEmpty(value.Salt))
                {
                    value.Salt = GoLive.Saturn.Crypto.Random.GetRandomString(32);
                }

                if (!string.IsNullOrWhiteSpace(value.Decoded))
                {
                    // Important: The original MongoDB serializer hashes Decoded + Salt
                    value.Hash = GoLive.Saturn.Crypto.Hash.CalculateSHA512($"{value.Decoded}{value.Salt}");
                }
                else if (string.IsNullOrWhiteSpace(value.Hash))
                {
                    return BsonValue.Null;
                }
                
                var doc = new BsonDocument();
                doc["Hash"] = value.Hash; // BsonValue will handle null string correctly
                doc["Salt"] = value.Salt;
                
                return doc;
            },
            deserialize: (BsonValue bson) =>
            {
                if (bson == null || bson.IsNull)
                {
                    return null;
                }

                if (!bson.IsDocument)
                {
                    Console.WriteLine("Warning: Expected BsonDocument for HashedString, but received: " + bson.Type);
                    return null; 
                }

                var doc = bson.AsDocument;
                var item = new HashedString();

                if (doc.TryGetValue("Hash", out var hashValue))
                    item.Hash = hashValue.AsString;
                
                if (doc.TryGetValue("Salt", out var saltValue))
                    item.Salt = saltValue.AsString;
                
                if (!string.IsNullOrWhiteSpace(item.Hash) && !string.IsNullOrWhiteSpace(item.Salt))
                {
                    item.Populated = true;
                }
                
                return item;
            });

        RegisterType<EncryptedString>(serialize: (EncryptedString value) =>
            {
                if (value == null || (string.IsNullOrEmpty(value.Encoded) &&
                                      string.IsNullOrEmpty(value.Hash) &&
                                      string.IsNullOrEmpty(value.Salt) &&
                                      string.IsNullOrEmpty(value.Decoded)))
                {
                    return BsonValue.Null;
                }

                if (string.IsNullOrEmpty(value.Salt))
                {
                    value.Salt = GoLive.Saturn.Crypto.Random.GetRandomString(32);
                }

                if (!string.IsNullOrWhiteSpace(value.Decoded))
                {
                    value.Encoded = GoLive.Saturn.Crypto.Encryption.EncryptStringAES(value.Decoded, GoLive.Saturn.Crypto.CryptoSingleton.Instance.MasterEncryptionKey, value.Salt);
                    value.Hash = GoLive.Saturn.Crypto.Hash.CalculateSHA512(value.Decoded);
                }

                var doc = new BsonDocument();
                doc["Encoded"] = value.Encoded; // BsonValue will handle null string correctly
                doc["Hash"] = value.Hash;
                doc["Salt"] = value.Salt;
                
                return doc;
            },
            deserialize: (BsonValue bson) =>
            {
                if (bson == null || bson.IsNull)
                {
                    return null;
                }

                if (!bson.IsDocument)
                {
                    Console.WriteLine($"Warning: Expected BsonDocument for EncryptedString, but received: {bson.Type}");
                    return null; 
                }

                var doc = bson.AsDocument;
                var item = new EncryptedString();

                if (doc.TryGetValue("Encoded", out var encodedValue))
                    item.Encoded = encodedValue.AsString; // AsString handles BsonNull by returning null
                
                if (doc.TryGetValue("Hash", out var hashValue))
                    item.Hash = hashValue.AsString;
                
                if (doc.TryGetValue("Salt", out var saltValue))
                    item.Salt = saltValue.AsString;

                if (!string.IsNullOrWhiteSpace(item.Encoded) && !string.IsNullOrWhiteSpace(item.Salt))
                {
                    item.Decoded = GoLive.Saturn.Crypto.Encryption.DecryptStringAES(item.Encoded, GoLive.Saturn.Crypto.CryptoSingleton.Instance.MasterEncryptionKey, item.Salt);
                    item.Populated = true;
                }
                
                return item;
            }
            );
    }

    protected override IEnumerable<MemberInfo> GetTypeMembers(Type type)
    {
        if (type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(Ref<>) || type.GetGenericTypeDefinition() == typeof(WeakRef<>)))
        {
            return type
                   .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                   .Where(p => p.Name == nameof(GoLive.Saturn.Data.Entities.Entity.Id));
        }

        
        return base.GetTypeMembers(type)
                   .Where(m => !m.IsDefined(typeof(NonSerializedAttribute), true) &&
                               m.Name != "_shortId" &&
                               m.Name != "Changes" &&
                               m.Name != "EnableChangeTracking");
    }
}