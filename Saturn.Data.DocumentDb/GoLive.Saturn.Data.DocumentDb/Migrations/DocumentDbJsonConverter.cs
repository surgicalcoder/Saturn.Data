using System;
using System.Text.Json.Nodes;
using GoLive.Saturn.Data.Migrations;

namespace Saturn.Data.DocumentDb;

internal static class DocumentDbJsonConverter
{
    public static MigrationObject ToMigrationObject(JsonNode source)
    {
        var document = (MigrationObject)ToMigrationValue(source);

        if (source is JsonObject jsonObject && jsonObject.TryGetPropertyValue("Id", out var idNode) && idNode is JsonValue idValue && idValue.TryGetValue(out string id))
        {
            document.Set(MigrationIds.IdField, MigrationValue.From(id));
        }

        return document;
    }

    public static JsonObject ToJsonObject(MigrationObject document)
    {
        var output = new JsonObject();

        foreach (var element in document)
        {
            if (string.Equals(element.Key, MigrationIds.IdField, StringComparison.Ordinal))
            {
                continue;
            }

            output[element.Key] = ToJsonNode(element.Value);
        }

        var id = GetIdText(document);

        if (id != null)
        {
            output["Id"] = JsonValue.Create(id);
        }

        return output;
    }

    public static string GetIdText(MigrationObject document)
    {
        if (!document.TryGetValue(MigrationIds.IdField, out var value) || value == null || value.IsNull)
        {
            return null;
        }

        return value.IsObjectId ? value.AsObjectId.ToString() : value.AsString;
    }

    public static MigrationValue ToMigrationValue(JsonNode node)
    {
        if (node is null)
        {
            return MigrationValue.Null;
        }

        if (node is JsonObject jsonObject)
        {
            var result = new MigrationObject();

            foreach (var property in jsonObject)
            {
                result.Set(property.Key, ToMigrationValue(property.Value));
            }

            return result;
        }

        if (node is JsonArray jsonArray)
        {
            var result = new MigrationArray();

            foreach (var item in jsonArray)
            {
                result.Add(ToMigrationValue(item));
            }

            return result;
        }

        if (node is JsonValue jsonValue)
        {
            if (jsonValue.TryGetValue(out bool boolean))
            {
                return MigrationValue.From(boolean);
            }

            if (jsonValue.TryGetValue(out long integer))
            {
                return MigrationValue.From(integer);
            }

            if (jsonValue.TryGetValue(out double number))
            {
                return MigrationValue.From(number);
            }

            if (jsonValue.TryGetValue(out decimal decimalValue))
            {
                return MigrationValue.From(decimalValue);
            }

            if (jsonValue.TryGetValue(out string text))
            {
                return MigrationValue.From(text);
            }
        }

        return MigrationValue.Null;
    }

    public static JsonNode ToJsonNode(MigrationValue value)
    {
        if (value == null || value.IsNull)
        {
            return null;
        }

        if (value.IsObject)
        {
            var result = new JsonObject();

            foreach (var element in value.AsObject())
            {
                result[element.Key] = ToJsonNode(element.Value);
            }

            return result;
        }

        if (value.IsArray)
        {
            var result = new JsonArray();

            foreach (var item in value.AsArray())
            {
                result.Add(ToJsonNode(item));
            }

            return result;
        }

        return value.Kind switch
        {
            MigrationValueKind.String => JsonValue.Create(value.AsString),
            MigrationValueKind.Boolean => JsonValue.Create(value.AsBoolean),
            MigrationValueKind.Int32 => JsonValue.Create(value.AsInt32),
            MigrationValueKind.Int64 => JsonValue.Create(value.AsInt64),
            MigrationValueKind.Double => JsonValue.Create(value.AsDouble),
            MigrationValueKind.Decimal => JsonValue.Create(value.AsDecimal),
            MigrationValueKind.DateTime => JsonValue.Create(value.AsDateTime),
            MigrationValueKind.Guid => JsonValue.Create(value.AsGuid),
            MigrationValueKind.ObjectId => JsonValue.Create(value.AsObjectId.ToString()),
            MigrationValueKind.Binary => JsonValue.Create(Convert.ToBase64String(value.AsBinary)),
            _ => null
        };
    }
}
