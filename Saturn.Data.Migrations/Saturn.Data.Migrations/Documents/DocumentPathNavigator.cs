using System;
using System.Collections.Generic;

namespace GoLive.Saturn.Data.Migrations;

public static class DocumentPathNavigator
{
    public static bool PathsConflict(string sourcePath, string targetPath)
    {
        if (string.Equals(sourcePath, targetPath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!TryParsePath(sourcePath, out var sourceSegments) || !TryParsePath(targetPath, out var targetSegments))
        {
            return IsAncestorOrDescendant(sourcePath, targetPath) || IsAncestorOrDescendant(targetPath, sourcePath);
        }

        return IsAncestorOrDescendant(sourceSegments, targetSegments) || IsAncestorOrDescendant(targetSegments, sourceSegments);
    }

    internal static bool HasWildcard(string path) => TryParsePath(path, out var segments) && HasPattern(segments);

    internal static bool HasRecursive(string path) => TryParsePath(path, out var segments) && HasRecursive(segments);

    internal static bool HasPattern(string path) => TryParsePath(path, out var segments) && HasPattern(segments);

    internal static MigrationPathResolutionFailure ResolveFailure(MigrationObject document, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !TryParsePath(path, out _))
        {
            return MigrationPathResolutionFailure.InvalidPath;
        }

        return TryGet(document, path, out _, out _, out _, out var failure) ? MigrationPathResolutionFailure.None : failure;
    }

    internal static bool CanBindWildcardSiblingPath(string primaryPath, string siblingPath)
    {
        if (!TryParsePath(primaryPath, out var primarySegments) || !TryParsePath(siblingPath, out var siblingSegments))
        {
            return false;
        }

        if (!HasArrayWildcard(primarySegments) || !HasArrayWildcard(siblingSegments) || HasRecursive(primarySegments) || HasRecursive(siblingSegments) || primarySegments.Length != siblingSegments.Length || primarySegments.Length == 0)
        {
            return false;
        }

        for (var i = 0; i < primarySegments.Length - 1; i++)
        {
            if (!SegmentsMatchExactly(primarySegments[i], siblingSegments[i]))
            {
                return false;
            }
        }

        var primaryLeaf = primarySegments[^1];
        var siblingLeaf = siblingSegments[^1];

        if (primaryLeaf.Kind != siblingLeaf.Kind)
        {
            return false;
        }

        return primaryLeaf.Kind != MigrationPathSegmentKind.ArrayIndex || primaryLeaf.ArrayIndex == siblingLeaf.ArrayIndex;
    }

    internal static bool TryBindPath(string templatePath, string concretePath, out string boundPath)
    {
        boundPath = null;

        if (!TryParsePath(templatePath, out var templateSegments) || !TryParsePath(concretePath, out var concreteSegments) || templateSegments.Length != concreteSegments.Length)
        {
            return false;
        }

        var currentPath = string.Empty;

        for (var i = 0; i < templateSegments.Length; i++)
        {
            var templateSegment = templateSegments[i];
            var concreteSegment = concreteSegments[i];
            var isLeaf = i == templateSegments.Length - 1;

            switch (templateSegment.Kind)
            {
                case MigrationPathSegmentKind.Property:
                    if (concreteSegment.Kind != MigrationPathSegmentKind.Property)
                    {
                        return false;
                    }

                    if (!string.Equals(templateSegment.PropertyName, concreteSegment.PropertyName, StringComparison.OrdinalIgnoreCase) && !isLeaf)
                    {
                        return false;
                    }

                    currentPath = AppendPropertyName(currentPath, templateSegment.PropertyName);
                    break;
                case MigrationPathSegmentKind.ArrayIndex:
                    if (concreteSegment.Kind != MigrationPathSegmentKind.ArrayIndex || templateSegment.ArrayIndex != concreteSegment.ArrayIndex)
                    {
                        return false;
                    }

                    currentPath = AppendArrayIndex(currentPath, templateSegment.ArrayIndex);
                    break;
                case MigrationPathSegmentKind.ArrayWildcard:
                    if (concreteSegment.Kind != MigrationPathSegmentKind.ArrayIndex)
                    {
                        return false;
                    }

                    currentPath = AppendArrayIndex(currentPath, concreteSegment.ArrayIndex);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        boundPath = currentPath;
        return true;
    }

    internal static IReadOnlyList<MigrationPredicateContext> CreateContexts(MigrationObject document, string path, bool includeLeafWhenMissing, string collection, string migrationName)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentNullException(nameof(path));
        }

        if (!TryParsePath(path, out var segments))
        {
            return Array.Empty<MigrationPredicateContext>();
        }

        if (!HasPattern(segments))
        {
            return new[] { CreateContext(document, path, collection, migrationName) };
        }

        var contexts = new List<MigrationPredicateContext>();
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        CreateContexts(document, document, segments, 0, string.Empty, includeLeafWhenMissing, collection, migrationName, seenPaths, contexts);
        return contexts;
    }

    internal static IReadOnlyList<MigrationPredicateContext> CreateCleanupContexts(MigrationObject document, CleanupScope scope, string collection, string migrationName)
    {
        ArgumentNullException.ThrowIfNull(document);

        var contexts = new List<MigrationPredicateContext>();

        foreach (var element in document)
        {
            if (string.Equals(element.Key, MigrationIds.IdField, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var path = element.Key;

            if (scope == CleanupScope.TopLevel)
            {
                contexts.Add(new MigrationPredicateContext(document, path, true, element.Value, collection, migrationName));
                continue;
            }

            CreateCleanupContexts(document, element.Value, path, collection, migrationName, contexts);
        }

        return contexts;
    }

    public static bool TryGet(MigrationObject document, string path, out MigrationObject parent, out string fieldName, out MigrationValue value)
        => TryGet(document, path, out parent, out fieldName, out value, out _);

    internal static bool TryGet(MigrationObject document, string path, out MigrationObject parent, out string fieldName, out MigrationValue value, out MigrationPathResolutionFailure failure)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentNullException(nameof(path));
        }

        parent = null;
        fieldName = string.Empty;
        value = MigrationValue.Null;
        failure = MigrationPathResolutionFailure.None;

        if (!TryParsePath(path, out var segments))
        {
            failure = MigrationPathResolutionFailure.InvalidPath;
            return false;
        }

        if (!TryResolveTarget(document, segments, segments.Length, createParents: false, out var target, out failure))
        {
            return false;
        }

        parent = target.DocumentParent;
        fieldName = target.FieldName;
        value = target.Value;
        return target.Exists;
    }

    public static MigrationPredicateContext CreateContext(MigrationObject document, string path, string collection, string migrationName)
        => CreateContext(document, path, collection, migrationName, out _);

    internal static MigrationPredicateContext CreateContext(MigrationObject document, string path, string collection, string migrationName, out MigrationPathResolutionFailure failure)
    {
        return TryGet(document, path, out _, out _, out var value, out failure)
            ? new MigrationPredicateContext(document, path, true, value, collection, migrationName)
            : MigrationPredicateContext.Missing(document, path, collection, migrationName);
    }

    public static bool TryAdd(MigrationObject document, string path, MigrationValue value, bool overwrite)
        => TryAdd(document, path, value, overwrite ? FieldWriteMode.Overwrite : FieldWriteMode.MissingOnly, createParents: false);

    internal static bool TryAdd(MigrationObject document, string path, MigrationValue value, bool overwrite, out MigrationPathResolutionFailure failure)
        => TryAdd(document, path, value, overwrite ? FieldWriteMode.Overwrite : FieldWriteMode.MissingOnly, createParents: false, out failure);

    public static bool TryAdd(MigrationObject document, string path, MigrationValue value, FieldWriteMode writeMode, bool createParents)
        => TryAdd(document, path, value, writeMode, createParents, out _);

    internal static bool TryAdd(MigrationObject document, string path, MigrationValue value, FieldWriteMode writeMode, bool createParents, out MigrationPathResolutionFailure failure)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentNullException(nameof(path));
        }

        failure = MigrationPathResolutionFailure.None;

        if (!TryParsePath(path, out var segments))
        {
            failure = MigrationPathResolutionFailure.InvalidPath;
            return false;
        }

        if (!TryResolveTarget(document, segments, segments.Length, createParents, out var target, out failure) || !target.CanWrite)
        {
            return false;
        }

        value ??= MigrationValue.Null;

        if (!CanWrite(target, writeMode))
        {
            return false;
        }

        if (target.Exists && target.Value == value)
        {
            return false;
        }

        return SetValue(target, value);
    }

    public static bool TryReplace(MigrationObject document, string path, MigrationValue value)
        => TryReplace(document, path, value, out _);

    internal static bool TryReplace(MigrationObject document, string path, MigrationValue value, out MigrationPathResolutionFailure failure)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentNullException(nameof(path));
        }

        failure = MigrationPathResolutionFailure.None;

        if (!TryParsePath(path, out var segments))
        {
            failure = MigrationPathResolutionFailure.InvalidPath;
            return false;
        }

        if (!TryResolveTarget(document, segments, segments.Length, createParents: false, out var target, out failure) || !target.Exists)
        {
            return false;
        }

        value ??= MigrationValue.Null;

        if (target.Value == value)
        {
            return false;
        }

        return SetValue(target, value);
    }

    public static bool TryRemove(MigrationObject document, string path, bool pruneEmptyParents)
        => TryRemove(document, path, pruneEmptyParents, out _);

    internal static bool TryRemove(MigrationObject document, string path, bool pruneEmptyParents, out MigrationPathResolutionFailure failure)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentNullException(nameof(path));
        }

        failure = MigrationPathResolutionFailure.None;

        if (!TryParsePath(path, out var segments))
        {
            failure = MigrationPathResolutionFailure.InvalidPath;
            return false;
        }

        if (!TryResolveTarget(document, segments, segments.Length, createParents: false, out var target, out failure) || !target.Exists)
        {
            return false;
        }

        if (!RemoveValue(target))
        {
            return false;
        }

        if (pruneEmptyParents)
        {
            PruneEmptyParents(document, segments);
        }

        return true;
    }

    private static void PruneEmptyParents(MigrationObject document, IReadOnlyList<MigrationPathSegment> segments)
    {
        for (var depth = segments.Count - 1; depth > 0; depth--)
        {
            if (!TryResolveTarget(document, segments, depth, createParents: false, out var target) || !target.Exists || !IsEmptyContainer(target.Value))
            {
                continue;
            }

            if (!RemoveValue(target))
            {
                break;
            }
        }
    }

    private static bool IsAncestorOrDescendant(string candidateAncestor, string candidateDescendant)
    {
        if (candidateAncestor.Length >= candidateDescendant.Length)
        {
            return false;
        }

        return candidateDescendant.StartsWith(candidateAncestor + ".", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAncestorOrDescendant(IReadOnlyList<MigrationPathSegment> candidateAncestor, IReadOnlyList<MigrationPathSegment> candidateDescendant)
    {
        if (candidateAncestor.Count >= candidateDescendant.Count)
        {
            return false;
        }

        for (var i = 0; i < candidateAncestor.Count; i++)
        {
            if (!SegmentsOverlap(candidateAncestor[i], candidateDescendant[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryParsePath(string path, out MigrationPathSegment[] segments)
    {
        segments = null;

        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var parsed = new List<MigrationPathSegment>();
        var position = 0;

        while (position < path.Length)
        {
            var nextSeparator = path.IndexOf('.', position);
            var tokenLength = (nextSeparator < 0 ? path.Length : nextSeparator) - position;

            if (tokenLength <= 0 || !TryParseToken(path.Substring(position, tokenLength), parsed))
            {
                return false;
            }

            if (nextSeparator < 0)
            {
                segments = parsed.ToArray();
                return segments.Length > 0 && ValidateSegments(segments);
            }

            position = nextSeparator + 1;

            if (position >= path.Length)
            {
                return false;
            }
        }

        return false;
    }

    private static bool TryParseToken(string token, ICollection<MigrationPathSegment> segments)
    {
        if (string.Equals(token, "**", StringComparison.Ordinal))
        {
            segments.Add(MigrationPathSegment.ForRecursiveDescent());
            return true;
        }

        var firstBracket = token.IndexOf('[');

        if (firstBracket < 0)
        {
            if (token.Length == 0)
            {
                return false;
            }

            segments.Add(MigrationPathSegment.ForProperty(token));
            return true;
        }

        var propertyName = token.Substring(0, firstBracket);

        if (propertyName.Length == 0)
        {
            return false;
        }

        segments.Add(MigrationPathSegment.ForProperty(propertyName));

        var position = firstBracket;

        while (position < token.Length)
        {
            if (token[position] != '[')
            {
                return false;
            }

            position++;

            if (position < token.Length && token[position] == '*')
            {
                position++;

                if (position >= token.Length || token[position] != ']')
                {
                    return false;
                }

                segments.Add(MigrationPathSegment.ForArrayWildcard());
                position++;
                continue;
            }

            if (position >= token.Length || !char.IsDigit(token[position]))
            {
                return false;
            }

            var start = position;

            while (position < token.Length && char.IsDigit(token[position]))
            {
                position++;
            }

            if (position >= token.Length || token[position] != ']')
            {
                return false;
            }

            if (!int.TryParse(token.AsSpan(start, position - start), out var index))
            {
                return false;
            }

            segments.Add(MigrationPathSegment.ForArrayIndex(index));
            position++;
        }

        return true;
    }

    private static bool TryResolveTarget(MigrationObject document, IReadOnlyList<MigrationPathSegment> segments, int segmentCount, bool createParents, out MigrationPathTarget target)
        => TryResolveTarget(document, segments, segmentCount, createParents, out target, out _);

    private static bool TryResolveTarget(MigrationObject document, IReadOnlyList<MigrationPathSegment> segments, int segmentCount, bool createParents, out MigrationPathTarget target, out MigrationPathResolutionFailure failure)
    {
        target = default;
        failure = MigrationPathResolutionFailure.None;

        if (segments == null || segmentCount <= 0 || segmentCount > segments.Count)
        {
            failure = MigrationPathResolutionFailure.InvalidPath;
            return false;
        }

        MigrationValue current = document;

        for (var i = 0; i < segmentCount - 1; i++)
        {
            var segment = segments[i];

            if (segment.Kind == MigrationPathSegmentKind.Property)
            {
                if (current == null || current.IsNull || !current.IsObject)
                {
                    failure = current == null || current.IsNull
                        ? MigrationPathResolutionFailure.MissingIntermediate
                        : MigrationPathResolutionFailure.ExpectedDocument;
                    return false;
                }

                var parent = current.AsObject();

                if (!parent.TryGetValue(segment.PropertyName, out current) || current.IsNull)
                {
                    if (!createParents)
                    {
                        failure = MigrationPathResolutionFailure.MissingIntermediate;
                        return false;
                    }

                    if (!TryCreateIntermediateValue(segments[i + 1], out current))
                    {
                        return false;
                    }

                    parent.Set(segment.PropertyName, current);
                    continue;
                }

                if (!CanTraverseValue(current, segments[i + 1]))
                {
                    failure = segments[i + 1].Kind == MigrationPathSegmentKind.ArrayIndex
                        ? MigrationPathResolutionFailure.ExpectedArray
                        : MigrationPathResolutionFailure.ExpectedDocument;
                    return false;
                }

                continue;
            }

            if (segment.Kind is MigrationPathSegmentKind.ArrayWildcard or MigrationPathSegmentKind.RecursiveDescent)
            {
                failure = MigrationPathResolutionFailure.UnsupportedPattern;
                return false;
            }

            if (current == null || current.IsNull || !current.IsArray)
            {
                failure = current == null || current.IsNull
                    ? MigrationPathResolutionFailure.MissingIntermediate
                    : MigrationPathResolutionFailure.ExpectedArray;
                return false;
            }

            var array = current.AsArray();

            if (segment.ArrayIndex < 0)
            {
                return false;
            }

            if (segment.ArrayIndex >= array.Count)
            {
                if (!createParents)
                {
                    failure = MigrationPathResolutionFailure.MissingIntermediate;
                    return false;
                }

                EnsureArrayIndex(array, segment.ArrayIndex);
            }

            current = array[segment.ArrayIndex];

            if ((current == null || current.IsNull) && createParents)
            {
                if (!TryCreateIntermediateValue(segments[i + 1], out current))
                {
                    return false;
                }

                array[segment.ArrayIndex] = current;
                continue;
            }

            if (!CanTraverseValue(current, segments[i + 1]))
            {
                failure = segments[i + 1].Kind == MigrationPathSegmentKind.ArrayIndex
                    ? MigrationPathResolutionFailure.ExpectedArray
                    : MigrationPathResolutionFailure.ExpectedDocument;
                return false;
            }
        }

        var leaf = segments[segmentCount - 1];

        if (leaf.Kind == MigrationPathSegmentKind.Property)
        {
            if (current == null || current.IsNull || !current.IsObject)
            {
                failure = current == null || current.IsNull
                    ? MigrationPathResolutionFailure.MissingIntermediate
                    : MigrationPathResolutionFailure.ExpectedDocument;
                return false;
            }

            var parent = current.AsObject();

            if (parent.TryGetValue(leaf.PropertyName, out var value))
            {
                target = MigrationPathTarget.ForProperty(parent, leaf.PropertyName, true, value);
                return true;
            }

            target = MigrationPathTarget.ForProperty(parent, leaf.PropertyName, false, MigrationValue.Null);
            failure = MigrationPathResolutionFailure.MissingLeaf;
            return true;
        }

        if (leaf.Kind is MigrationPathSegmentKind.ArrayWildcard or MigrationPathSegmentKind.RecursiveDescent)
        {
            failure = MigrationPathResolutionFailure.UnsupportedPattern;
            return false;
        }

        if (current == null || current.IsNull || !current.IsArray)
        {
            failure = current == null || current.IsNull
                ? MigrationPathResolutionFailure.MissingIntermediate
                : MigrationPathResolutionFailure.ExpectedArray;
            return false;
        }

        var arrayParent = current.AsArray();

        if (leaf.ArrayIndex < 0)
        {
            return false;
        }

        var exists = leaf.ArrayIndex < arrayParent.Count;

        if (!exists && !createParents)
        {
            failure = MigrationPathResolutionFailure.MissingLeaf;
            return false;
        }

        var currentValue = exists ? arrayParent[leaf.ArrayIndex] : MigrationValue.Null;
        target = MigrationPathTarget.ForArrayIndex(arrayParent, leaf.ArrayIndex, exists, currentValue);
        return true;
    }

    private static void CreateContexts(MigrationObject root, MigrationValue current, IReadOnlyList<MigrationPathSegment> segments, int index, string currentPath, bool includeLeafWhenMissing, string collection, string migrationName, ISet<string> seenPaths, ICollection<MigrationPredicateContext> contexts)
    {
        var segment = segments[index];
        var isLeaf = index == segments.Count - 1;

        if (segment.Kind == MigrationPathSegmentKind.RecursiveDescent)
        {
            CreateContexts(root, current, segments, index + 1, currentPath, includeLeafWhenMissing, collection, migrationName, seenPaths, contexts);

            if (current == null || current.IsNull)
            {
                return;
            }

            if (current.IsObject)
            {
                foreach (var element in current.AsObject())
                {
                    CreateContexts(root, element.Value, segments, index, AppendPropertyName(currentPath, element.Key), includeLeafWhenMissing, collection, migrationName, seenPaths, contexts);
                }

                return;
            }

            if (current.IsArray)
            {
                var recursiveArray = current.AsArray();

                for (var i = 0; i < recursiveArray.Count; i++)
                {
                    CreateContexts(root, recursiveArray[i], segments, index, AppendArrayIndex(currentPath, i), includeLeafWhenMissing, collection, migrationName, seenPaths, contexts);
                }
            }

            return;
        }

        if (segment.Kind == MigrationPathSegmentKind.Property)
        {
            if (current == null || current.IsNull || !current.IsObject)
            {
                if (includeLeafWhenMissing && current != null && current.IsNull && TryBuildRemainingPath(currentPath, segments, index, out var missingPath))
                {
                    AddContext(missingPath, MigrationPredicateContext.Missing(root, missingPath, collection, migrationName), seenPaths, contexts);
                }

                return;
            }

            var path = AppendPropertyName(currentPath, segment.PropertyName);
            var document = current.AsObject();

            if (isLeaf)
            {
                if (document.TryGetValue(segment.PropertyName, out var value))
                {
                    AddContext(path, new MigrationPredicateContext(root, path, true, value, collection, migrationName), seenPaths, contexts);
                }
                else if (includeLeafWhenMissing)
                {
                    AddContext(path, MigrationPredicateContext.Missing(root, path, collection, migrationName), seenPaths, contexts);
                }

                return;
            }

            if (document.TryGetValue(segment.PropertyName, out var child) && !child.IsNull)
            {
                CreateContexts(root, child, segments, index + 1, path, includeLeafWhenMissing, collection, migrationName, seenPaths, contexts);
            }
            else if (includeLeafWhenMissing && TryBuildRemainingPath(currentPath, segments, index, out var missingPath))
            {
                AddContext(missingPath, MigrationPredicateContext.Missing(root, missingPath, collection, migrationName), seenPaths, contexts);
            }

            return;
        }

        if (current == null || current.IsNull || !current.IsArray)
        {
            if (includeLeafWhenMissing && current != null && current.IsNull && TryBuildRemainingPath(currentPath, segments, index, out var missingPath))
            {
                AddContext(missingPath, MigrationPredicateContext.Missing(root, missingPath, collection, migrationName), seenPaths, contexts);
            }

            return;
        }

        var array = current.AsArray();

        if (segment.Kind == MigrationPathSegmentKind.ArrayIndex)
        {
            if (segment.ArrayIndex < 0 || segment.ArrayIndex >= array.Count)
            {
                if (includeLeafWhenMissing && TryBuildRemainingPath(currentPath, segments, index, out var missingPath))
                {
                    AddContext(missingPath, MigrationPredicateContext.Missing(root, missingPath, collection, migrationName), seenPaths, contexts);
                }

                return;
            }

            var path = AppendArrayIndex(currentPath, segment.ArrayIndex);
            var value = array[segment.ArrayIndex];

            if (isLeaf)
            {
                AddContext(path, new MigrationPredicateContext(root, path, true, value, collection, migrationName), seenPaths, contexts);
                return;
            }

            CreateContexts(root, value, segments, index + 1, path, includeLeafWhenMissing, collection, migrationName, seenPaths, contexts);
            return;
        }

        for (var i = 0; i < array.Count; i++)
        {
            var path = AppendArrayIndex(currentPath, i);
            var value = array[i];

            if (isLeaf)
            {
                AddContext(path, new MigrationPredicateContext(root, path, true, value, collection, migrationName), seenPaths, contexts);
            }
            else
            {
                CreateContexts(root, value, segments, index + 1, path, includeLeafWhenMissing, collection, migrationName, seenPaths, contexts);
            }
        }
    }

    private static void AddContext(string path, MigrationPredicateContext context, ISet<string> seenPaths, ICollection<MigrationPredicateContext> contexts)
    {
        if (seenPaths.Add(path))
        {
            contexts.Add(context);
        }
    }

    private static void CreateCleanupContexts(MigrationObject root, MigrationValue current, string currentPath, string collection, string migrationName, ICollection<MigrationPredicateContext> contexts)
    {
        if (current != null && !current.IsNull)
        {
            if (current.IsObject)
            {
                foreach (var element in current.AsObject())
                {
                    CreateCleanupContexts(root, element.Value, AppendPropertyName(currentPath, element.Key), collection, migrationName, contexts);
                }
            }
            else if (current.IsArray)
            {
                var array = current.AsArray();

                for (var i = array.Count - 1; i >= 0; i--)
                {
                    CreateCleanupContexts(root, array[i], AppendArrayIndex(currentPath, i), collection, migrationName, contexts);
                }
            }
        }

        contexts.Add(new MigrationPredicateContext(root, currentPath, true, current ?? MigrationValue.Null, collection, migrationName));
    }

    private static bool SetValue(MigrationPathTarget target, MigrationValue value)
    {
        if (target.IsArrayIndex)
        {
            if (target.ArrayParent == null || target.ArrayIndex < 0)
            {
                return false;
            }

            EnsureArrayIndex(target.ArrayParent, target.ArrayIndex);
            target.ArrayParent[target.ArrayIndex] = value;
            return true;
        }

        if (target.DocumentParent == null || target.FieldName.Length == 0)
        {
            return false;
        }

        target.DocumentParent.Set(target.FieldName, value);
        return true;
    }

    private static bool RemoveValue(MigrationPathTarget target)
    {
        if (target.IsArrayIndex)
        {
            if (target.ArrayParent == null || target.ArrayIndex < 0 || target.ArrayIndex >= target.ArrayParent.Count)
            {
                return false;
            }

            target.ArrayParent.RemoveAt(target.ArrayIndex);
            return true;
        }

        return target.DocumentParent != null && target.FieldName.Length > 0 && target.DocumentParent.Remove(target.FieldName);
    }

    private static bool IsEmptyContainer(MigrationValue value)
    {
        if (value == null || value.IsNull)
        {
            return false;
        }

        if (value.IsObject)
        {
            return value.AsObject().Count == 0;
        }

        if (value.IsArray)
        {
            return value.AsArray().Count == 0;
        }

        return false;
    }

    private static bool HasPattern(IReadOnlyList<MigrationPathSegment> segments) => HasArrayWildcard(segments) || HasRecursive(segments);

    private static bool HasArrayWildcard(IReadOnlyList<MigrationPathSegment> segments)
    {
        for (var i = 0; i < segments.Count; i++)
        {
            if (segments[i].Kind == MigrationPathSegmentKind.ArrayWildcard)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasRecursive(IReadOnlyList<MigrationPathSegment> segments)
    {
        for (var i = 0; i < segments.Count; i++)
        {
            if (segments[i].Kind == MigrationPathSegmentKind.RecursiveDescent)
            {
                return true;
            }
        }

        return false;
    }

    private static bool SegmentsOverlap(MigrationPathSegment left, MigrationPathSegment right)
    {
        if (left.Kind == MigrationPathSegmentKind.RecursiveDescent || right.Kind == MigrationPathSegmentKind.RecursiveDescent)
        {
            return left.Kind == right.Kind;
        }

        if (left.Kind == MigrationPathSegmentKind.Property || right.Kind == MigrationPathSegmentKind.Property)
        {
            return left.Kind == right.Kind && string.Equals(left.PropertyName, right.PropertyName, StringComparison.OrdinalIgnoreCase);
        }

        if (left.Kind == MigrationPathSegmentKind.ArrayWildcard || right.Kind == MigrationPathSegmentKind.ArrayWildcard)
        {
            return true;
        }

        return left.ArrayIndex == right.ArrayIndex;
    }

    private static bool SegmentsMatchExactly(MigrationPathSegment left, MigrationPathSegment right)
    {
        if (left.Kind != right.Kind)
        {
            return false;
        }

        return left.Kind switch
        {
            MigrationPathSegmentKind.Property => string.Equals(left.PropertyName, right.PropertyName, StringComparison.OrdinalIgnoreCase),
            MigrationPathSegmentKind.ArrayIndex => left.ArrayIndex == right.ArrayIndex,
            MigrationPathSegmentKind.ArrayWildcard => true,
            MigrationPathSegmentKind.RecursiveDescent => true,
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    private static bool ValidateSegments(IReadOnlyList<MigrationPathSegment> segments)
    {
        var recursiveCount = 0;

        for (var i = 0; i < segments.Count; i++)
        {
            if (segments[i].Kind != MigrationPathSegmentKind.RecursiveDescent)
            {
                continue;
            }

            recursiveCount++;

            if (recursiveCount > 1 || i == segments.Count - 1)
            {
                return false;
            }
        }

        return true;
    }

    private static string AppendPropertyName(string path, string propertyName) => path.Length == 0 ? propertyName : path + "." + propertyName;

    private static string AppendArrayIndex(string path, int index) => path + "[" + index + "]";

    private static bool TryBuildRemainingPath(string currentPath, IReadOnlyList<MigrationPathSegment> segments, int startIndex, out string path)
    {
        path = currentPath;

        for (var i = startIndex; i < segments.Count; i++)
        {
            var segment = segments[i];

            switch (segment.Kind)
            {
                case MigrationPathSegmentKind.Property:
                    path = AppendPropertyName(path, segment.PropertyName);
                    break;
                case MigrationPathSegmentKind.ArrayIndex:
                    path = AppendArrayIndex(path, segment.ArrayIndex);
                    break;
                default:
                    path = null;
                    return false;
            }
        }

        return path.Length > 0;
    }

    private static bool TryCreateIntermediateValue(MigrationPathSegment nextSegment, out MigrationValue value)
    {
        switch (nextSegment.Kind)
        {
            case MigrationPathSegmentKind.Property:
                value = new MigrationObject();
                return true;
            case MigrationPathSegmentKind.ArrayIndex:
                value = new MigrationArray();
                return true;
            default:
                value = MigrationValue.Null;
                return false;
        }
    }

    private static bool CanTraverseValue(MigrationValue value, MigrationPathSegment nextSegment)
    {
        if (value == null || value.IsNull)
        {
            return false;
        }

        return nextSegment.Kind switch
        {
            MigrationPathSegmentKind.Property => value.IsObject,
            MigrationPathSegmentKind.ArrayIndex => value.IsArray,
            _ => false
        };
    }

    private static void EnsureArrayIndex(MigrationArray array, int index)
    {
        while (array.Count <= index)
        {
            array.Add(MigrationValue.Null);
        }
    }

    private static bool CanWrite(MigrationPathTarget target, FieldWriteMode writeMode)
    {
        return writeMode switch
        {
            FieldWriteMode.MissingOnly => !target.Exists,
            FieldWriteMode.ExistingOnly => target.Exists,
            FieldWriteMode.NullOrMissing => !target.Exists || target.Value.IsNull,
            FieldWriteMode.Overwrite => true,
            _ => throw new ArgumentOutOfRangeException(nameof(writeMode))
        };
    }
}
