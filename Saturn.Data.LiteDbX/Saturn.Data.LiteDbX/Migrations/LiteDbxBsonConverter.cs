using System.Collections.Generic;
using GoLive.Saturn.Data.Migrations;
using LiteDbX;

namespace Saturn.Data.LiteDbX;

internal static class LiteDbxBsonConverter
{
    public static MigrationObject ToMigrationObject(BsonDocument document)
    {
        var result = new MigrationObject();

        foreach (var element in document)
        {
            result.Set(element.Key, ToMigrationValue(element.Value));
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
        if (value == null || value.IsNull)
        {
            return MigrationValue.Null;
        }

        if (value.IsDocument)
        {
            return ToMigrationObject(value.AsDocument);
        }

        if (value.IsArray)
        {
            var array = new MigrationArray();

            foreach (var item in value.AsArray)
            {
                array.Add(ToMigrationValue(item));
            }

            return array;
        }

        if (value.IsBinary)
        {
            return MigrationValue.From(value.AsBinary);
        }

        return value.Type switch
        {
            BsonType.ObjectId => MigrationValue.From(new MigrationObjectId(value.AsObjectId.ToString())),
            BsonType.String => MigrationValue.From(value.AsString),
            BsonType.Boolean => MigrationValue.From(value.AsBoolean),
            BsonType.Int32 => MigrationValue.From(value.AsInt32),
            BsonType.Int64 => MigrationValue.From(value.AsInt64),
            BsonType.Double => MigrationValue.From(value.AsDouble),
            BsonType.Decimal => MigrationValue.From(value.AsDecimal),
            BsonType.DateTime => MigrationValue.From(value.AsDateTime),
            BsonType.Guid => MigrationValue.From(value.AsGuid),
            BsonType.MinValue => MigrationValue.MinValue,
            BsonType.MaxValue => MigrationValue.MaxValue,
            _ => MigrationValue.Null
        };
    }

    public static BsonValue ToBsonValue(MigrationValue value)
    {
        if (value == null || value.IsNull)
        {
            return BsonValue.Null;
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
            MigrationValueKind.String => new BsonValue(value.AsString),
            MigrationValueKind.Boolean => new BsonValue(value.AsBoolean),
            MigrationValueKind.Int32 => new BsonValue(value.AsInt32),
            MigrationValueKind.Int64 => new BsonValue(value.AsInt64),
            MigrationValueKind.Double => new BsonValue(value.AsDouble),
            MigrationValueKind.Decimal => new BsonValue(value.AsDecimal),
            MigrationValueKind.DateTime => new BsonValue(value.AsDateTime),
            MigrationValueKind.Guid => new BsonValue(value.AsGuid),
            MigrationValueKind.ObjectId => new BsonValue(new ObjectId(value.AsObjectId.ToString())),
            MigrationValueKind.Binary => new BsonValue(value.AsBinary),
            MigrationValueKind.MinValue => BsonValue.MinValue,
            MigrationValueKind.MaxValue => BsonValue.MaxValue,
            _ => BsonValue.Null
        };
    }
}
