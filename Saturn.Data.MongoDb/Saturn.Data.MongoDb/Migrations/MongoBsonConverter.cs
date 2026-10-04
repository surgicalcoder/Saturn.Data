using GoLive.Saturn.Data.Migrations;
using MongoDB.Bson;

namespace Saturn.Data.MongoDb;

internal static class MongoBsonConverter
{
    public static MigrationObject ToMigrationObject(BsonDocument document)
    {
        var result = new MigrationObject();

        foreach (var element in document)
        {
            result.Set(element.Name, ToMigrationValue(element.Value));
        }

        return result;
    }

    public static BsonDocument ToBsonDocument(MigrationObject document)
    {
        var result = new BsonDocument();

        foreach (var element in document)
        {
            result[element.Key] = ToBsonValue(element.Value);
        }

        return result;
    }

    public static MigrationValue ToMigrationValue(BsonValue value)
    {
        if (value == null || value.IsBsonNull)
        {
            return MigrationValue.Null;
        }

        if (value.IsBsonDocument)
        {
            return ToMigrationObject(value.AsBsonDocument);
        }

        if (value.IsBsonArray)
        {
            var array = new MigrationArray();

            foreach (var item in value.AsBsonArray)
            {
                array.Add(ToMigrationValue(item));
            }

            return array;
        }

        return value.BsonType switch
        {
            BsonType.ObjectId => MigrationValue.From(new MigrationObjectId(value.AsObjectId.ToString())),
            BsonType.String => MigrationValue.From(value.AsString),
            BsonType.Boolean => MigrationValue.From(value.AsBoolean),
            BsonType.Int32 => MigrationValue.From(value.AsInt32),
            BsonType.Int64 => MigrationValue.From(value.AsInt64),
            BsonType.Double => MigrationValue.From(value.AsDouble),
            BsonType.Decimal128 => MigrationValue.From(value.AsDecimal),
            BsonType.DateTime => MigrationValue.From(value.ToUniversalTime()),
            BsonType.Binary => value.AsBsonBinaryData.SubType == BsonBinarySubType.UuidStandard
                ? MigrationValue.From(value.AsGuid)
                : MigrationValue.From(value.AsByteArray),
            BsonType.MinKey => MigrationValue.MinValue,
            BsonType.MaxKey => MigrationValue.MaxValue,
            _ => MigrationValue.Null
        };
    }

    public static BsonValue ToBsonValue(MigrationValue value)
    {
        if (value == null || value.IsNull)
        {
            return BsonNull.Value;
        }

        if (value.IsObject)
        {
            return ToBsonDocument(value.AsObject());
        }

        if (value.IsArray)
        {
            var array = new BsonArray();

            foreach (var item in value.AsArray())
            {
                array.Add(ToBsonValue(item));
            }

            return array;
        }

        return value.Kind switch
        {
            MigrationValueKind.String => new BsonString(value.AsString),
            MigrationValueKind.Boolean => BsonBoolean.Create(value.AsBoolean),
            MigrationValueKind.Int32 => new BsonInt32(value.AsInt32),
            MigrationValueKind.Int64 => new BsonInt64(value.AsInt64),
            MigrationValueKind.Double => new BsonDouble(value.AsDouble),
            MigrationValueKind.Decimal => new BsonDecimal128(value.AsDecimal),
            MigrationValueKind.DateTime => new BsonDateTime(value.AsDateTime),
            MigrationValueKind.Guid => new BsonBinaryData(value.AsGuid, GuidRepresentation.Standard),
            MigrationValueKind.ObjectId => new BsonObjectId(new ObjectId(value.AsObjectId.ToString())),
            MigrationValueKind.Binary => new BsonBinaryData(value.AsBinary),
            MigrationValueKind.MinValue => BsonMinKey.Value,
            MigrationValueKind.MaxValue => BsonMaxKey.Value,
            _ => BsonNull.Value
        };
    }
}
