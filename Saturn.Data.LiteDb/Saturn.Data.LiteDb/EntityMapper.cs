using System.Reflection;
using GoLive.Saturn.Data.Entities;
using LiteDbX;

namespace Saturn.Data.LiteDb;

internal class EmptyRefForMapping : Entity { };

public class CustomEntityMapper : BsonMapper
{
    public CustomEntityMapper(Func<Type, object>? customTypeInstantiator = null, ITypeNameBinder? typeNameBinder = null) : base(customTypeInstantiator, typeNameBinder)
    {
        Inheritance<Entity>()
            .Id(e => e.Id, BsonType.ObjectId, false)
            .Ignore(e => e.EnableChangeTracking)
            .Ignore(e => e.Changes)
            .Ignore(e => e.Properties);
        
        RegisterOpenGenericType(
            typeof(Ref<>),
            serializeFactory: type =>
        {
                var idProperty = type.GetProperty(nameof(Ref<EmptyRefForMapping>.Id))
                               ?? throw new InvalidOperationException($"Id property was not found on {type.FullName}.");

                return (o, mapper) =>
                {
                    if (o == null)
                    {
                        return BsonValue.Null;
                    }
        
                    var id = idProperty.GetValue(o);
                    return id == null ? BsonValue.Null : new BsonValue(new ObjectId(id as string));
                };
            },
            deserializeFactory: type =>
            {
                var idProperty = type.GetProperty(nameof(Ref<EmptyRefForMapping>.Id))
                                 ?? throw new InvalidOperationException($"Id property was not found on {type.FullName}.");

                return (bson, mapper) =>
                {
                    if (bson == null || bson.IsNull)
                    {
                        return null;
                    }
        
                    var instance = Activator.CreateInstance(type)
                                   ?? throw new InvalidOperationException($"Could not create instance of {type.FullName}.");
        
                    idProperty.SetValue(instance, mapper.Deserialize(idProperty.PropertyType, bson));
                    return instance;
                };
        });
        

        RegisterType<HashedString>(
            serialize: value =>
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
            deserialize: bson =>
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

        RegisterType<EncryptedString>(
            serialize: value =>
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
            deserialize: bson =>
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
            });
    }
}
