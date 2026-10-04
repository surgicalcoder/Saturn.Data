using System;
using System.Collections.Generic;

namespace GoLive.Saturn.Data.Migrations;

public interface IMigrationFieldMap
{
    string ToLogical(string physical);

    string ToPhysical(string logical);
}

public sealed class IdentityMigrationFieldMap : IMigrationFieldMap
{
    public static readonly IdentityMigrationFieldMap Instance = new();

    private IdentityMigrationFieldMap()
    {
    }

    public string ToLogical(string physical) => physical;

    public string ToPhysical(string logical) => logical;
}

public sealed class MigrationFieldMap : IMigrationFieldMap
{
    private readonly Dictionary<string, string> physicalToLogical;
    private readonly Dictionary<string, string> logicalToPhysical;

    public MigrationFieldMap(IEnumerable<KeyValuePair<string, string>> logicalToPhysicalMappings)
    {
        ArgumentNullException.ThrowIfNull(logicalToPhysicalMappings);

        logicalToPhysical = new Dictionary<string, string>(StringComparer.Ordinal);
        physicalToLogical = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var mapping in logicalToPhysicalMappings)
        {
            logicalToPhysical[mapping.Key] = mapping.Value;
            physicalToLogical[mapping.Value] = mapping.Key;
        }
    }

    public static MigrationFieldMap Empty { get; } = new(Array.Empty<KeyValuePair<string, string>>());

    public string ToLogical(string physical) => physicalToLogical.TryGetValue(physical, out var logical) ? logical : physical;

    public string ToPhysical(string logical) => logicalToPhysical.TryGetValue(logical, out var physical) ? physical : logical;
}

public static class MigrationFieldTranslation
{
    public static void ApplyToLogical(MigrationObject document, IMigrationFieldMap map)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(map);

        if (ReferenceEquals(map, IdentityMigrationFieldMap.Instance))
        {
            return;
        }

        Apply(document, map.ToLogical);
    }

    public static void ApplyToPhysical(MigrationObject document, IMigrationFieldMap map)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(map);

        if (ReferenceEquals(map, IdentityMigrationFieldMap.Instance))
        {
            return;
        }

        Apply(document, map.ToPhysical);
    }

    private static void Apply(MigrationObject document, Func<string, string> map)
    {
        List<(string OldKey, string NewKey, MigrationValue Value)> moves = null;

        foreach (var element in document)
        {
            var mapped = map(element.Key);

            if (string.Equals(mapped, element.Key, StringComparison.Ordinal))
            {
                continue;
            }

            moves ??= new List<(string, string, MigrationValue)>();
            moves.Add((element.Key, mapped, element.Value));
        }

        if (moves == null)
        {
            return;
        }

        foreach (var move in moves)
        {
            document.Remove(move.OldKey);

            if (!document.ContainsKey(move.NewKey))
            {
                document.Set(move.NewKey, move.Value);
            }
        }
    }
}
