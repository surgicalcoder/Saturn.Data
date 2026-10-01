using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace GoLive.Saturn.Generator.Entities;

public static class DtoGenerator
{
    private sealed class DtoMember
    {
        public MemberToGenerate Source { get; set; }
        public string Name { get; set; }
        public bool IsCollection { get; set; }
        public bool IsReference { get; set; }
        public bool IsEntity { get; set; }
        public ITypeSymbol ElementType { get; set; }
        public ITypeSymbol ReferenceTarget { get; set; }
        public string DtoType { get; set; }
        public bool Projectable { get; set; }
        public string NestedDtoType { get; set; }
        public string ElementDtoType { get; set; }
        public bool ExpandedRef { get; set; }
        public string ExpandedRefEntityType { get; set; }
    }

    public static void Generate(SourceStringBuilder source, ClassToGenerate classToGen, bool expandRefs, bool useFullId, string namespaceName,
        bool trackChanges, IReadOnlyDictionary<string, string> knownDtos)
    {
        var members = new List<DtoMember>();

        foreach (var member in classToGen.Members)
        {
            if (member.Type is null || member.ExcludeFromDto || member.WriteOnly)
            {
                continue;
            }

            var name = member.Name.FirstCharToUpper();

            if (member.IsCollection)
            {
                var element = member.CollectionType ?? member.Type;
                var elementReference = TryGetReferenceTarget(element, out var referenceTarget);
                var elementDto = elementReference ? ResolveDto(referenceTarget, knownDtos) : ResolveDto(element, knownDtos);

                var expandElementRef = expandRefs || member.IsEmbedded;
                var dtoElement = elementReference
                    ? (expandElementRef ? (elementDto ?? SourceCodeGenerator.RenderType(referenceTarget)) : "string?")
                    : (elementDto ?? SourceCodeGenerator.RenderType(element));

                members.Add(new DtoMember
                {
                    Source = member,
                    Name = name,
                    IsCollection = true,
                    IsReference = elementReference,
                    ElementType = element,
                    ReferenceTarget = referenceTarget,
                    DtoType = $"List<{dtoElement}>",
                    ElementDtoType = elementReference && !expandElementRef ? null : elementDto,
                    ExpandedRef = expandElementRef,
                    Projectable = false
                });

                continue;
            }

            if (TryGetReferenceTarget(member.Type, out var target))
            {
                var targetDto = ResolveDto(target, knownDtos);
                var expandMemberRef = expandRefs || member.IsEmbedded;

                members.Add(new DtoMember
                {
                    Source = member,
                    Name = name,
                    IsReference = true,
                    ReferenceTarget = target,
                    ExpandedRef = expandMemberRef,
                    ExpandedRefEntityType = SourceCodeGenerator.RenderType(target),
                    NestedDtoType = targetDto,
                    DtoType = expandMemberRef ? (targetDto is null ? SourceCodeGenerator.RenderType(target) : targetDto) : "string?",
                    Projectable = !expandMemberRef
                });

                continue;
            }

            var isEntity = IsEntityType(member.Type);
            var entityDto = isEntity ? ResolveDto(member.Type, knownDtos) : null;

            members.Add(new DtoMember
            {
                Source = member,
                Name = name,
                IsEntity = isEntity,
                NestedDtoType = entityDto,
                DtoType = entityDto ?? SourceCodeGenerator.RenderType(member.Type),
                Projectable = !isEntity && member.Type.TypeKind != TypeKind.Interface
            });
        }

        if (classToGen.DtoIncludeProperties)
        {
            members.Add(new DtoMember
            {
                Name = "Properties",
                DtoType = "Dictionary<string, object>?",
                Projectable = false
            });
        }

        var dtoName = classToGen.DtoName;
        var entityName = classToGen.Name;

        var interfaces = trackChanges
            ? $", global::GoLive.Saturn.Data.ChangeTracking.ITrackable, global::GoLive.Saturn.Data.ChangeTracking.ITrackableMetadata, global::GoLive.Saturn.Data.Entities.ISuppressTracking"
            : string.Empty;

        source.AppendLine(2);
        source.AppendLine("[global::System.CodeDom.Compiler.GeneratedCode(\"Saturn.Generator.Entities\", \"1.0.0\")]");
        source.AppendLine($"public partial class {dtoName} : ICreatableFrom<{entityName}>, IUpdatableFrom<{entityName}>{interfaces}");
        source.AppendOpenCurlyBracketLine();

        source.AppendLine("public const string DtoSchemaVersion = \"1\";");
        source.AppendLine(2);
        source.AppendLine("public string? Id { get; set; }");
        source.AppendLine();

        foreach (var member in members)
        {
            if (!trackChanges)
            {
                source.AppendLine($"public {member.DtoType} {member.Name} {{ get; set; }}");
                continue;
            }

            var backing = Camel(member.Name);
            source.AppendLine($"private {member.DtoType} {backing};");
            source.AppendLine($"public {member.DtoType} {member.Name}");
            source.AppendOpenCurlyBracketLine();
            source.AppendLine($"get => {backing};");
            source.AppendLine("set");
            source.AppendOpenCurlyBracketLine();
            source.AppendLine($"if (global::System.Collections.Generic.EqualityComparer<{member.DtoType}>.Default.Equals({backing}, value))");
            source.AppendOpenCurlyBracketLine();
            source.AppendLine("return;");
            source.AppendCloseCurlyBracketLine();
            source.AppendLine($"var previous = {backing};");
            source.AppendLine($"{backing} = value;");
            source.AppendLine($"OnFieldChanged(\"{member.Name}\", previous, value);");
            source.AppendCloseCurlyBracketLine();
            source.AppendCloseCurlyBracketLine();
        }

        source.AppendLine(2);
        source.AppendLine("public static readonly global::System.Collections.Generic.HashSet<string> PatchableMembers = new(global::System.StringComparer.Ordinal)");

        source.AppendOpenCurlyBracketLine();

        foreach (var member in members.Where(member => member.Source?.ReadOnly != true))
        {
            source.AppendLine($"\"{member.Name}\",");
        }

        source.AppendCloseCurlyBracketLine();
        source.AppendLine(";");

        source.AppendLine(2);
        source.AppendLine($"public static implicit operator {dtoName}({entityName} source) => FromEntity(source);");
        source.AppendLine($"public static implicit operator {dtoName}?(Ref<{entityName}> source) => FromRef(source);");
        source.AppendLine();
        source.AppendLine($"public static {dtoName}? FromRef(Ref<{entityName}> source) => source?.Item is null ? null : FromEntity(source.Item);");
        source.AppendLine();
        source.AppendLine($"public static {dtoName} FromEntity({entityName} source)");
        source.AppendOpenCurlyBracketLine();
        source.AppendLine("if (source is null)");
        source.AppendOpenCurlyBracketLine();
        source.AppendLine("throw new ArgumentNullException(nameof(source));");
        source.AppendCloseCurlyBracketLine();
        source.AppendLine($"var dto = new {dtoName}();");
        source.AppendLine("dto.UpdateFrom(source);");
        source.AppendLine("return dto;");
        source.AppendCloseCurlyBracketLine();

        source.AppendLine();
        source.AppendLine($"public void UpdateFrom({entityName} source)");
        source.AppendOpenCurlyBracketLine();
        source.AppendLine("if (source is null)");
        source.AppendOpenCurlyBracketLine();
        source.AppendLine("return;");
        source.AppendCloseCurlyBracketLine();
        source.AppendLine(useFullId ? "Id = source.Id;" : "Id = source._shortId;");

        foreach (var member in members)
        {
            source.AppendLine($"{member.Name} = {FromEntityExpression(member)};");
        }

        source.AppendCloseCurlyBracketLine();

        source.AppendLine();
        source.AppendLine($"public static ICreatableFrom<{entityName}> Create({entityName} input) => FromEntity(input);");

        var writableMembers = members.Where(m => m.Source is null || !m.Source.ReadOnly).ToList();

        source.AppendLine();
        source.AppendLine($"public {entityName} ToEntity()");
        source.AppendOpenCurlyBracketLine();
        source.AppendLine($"var entity = new {entityName}();");
        source.AppendLine("ApplyTo(entity);");
        source.AppendLine("return entity;");
        source.AppendCloseCurlyBracketLine();

        source.AppendLine();
        source.AppendLine($"public void ApplyTo({entityName} target)");
        source.AppendOpenCurlyBracketLine();
        source.AppendLine("if (target is null)");
        source.AppendOpenCurlyBracketLine();
        source.AppendLine("throw new ArgumentNullException(nameof(target));");
        source.AppendCloseCurlyBracketLine();
        source.AppendLine("target.Id = Id;");

        foreach (var member in writableMembers)
        {
            source.AppendLine(ToEntityStatement(member));
        }

        source.AppendCloseCurlyBracketLine();

        var projectable = members.Where(m => m.Projectable).ToList();

        source.AppendLine();

        if (projectable.Count == 0)
        {
            source.AppendLine($"public static Expression<Func<{entityName}, {dtoName}>>? Selector => null;");
        }
        else
        {
            source.AppendLine($"public static Expression<Func<{entityName}, {dtoName}>>? Selector => source => new {dtoName}");
            source.AppendOpenCurlyBracketLine();
            source.AppendLine(useFullId ? "Id = source.Id," : "Id = source._shortId,");

            foreach (var member in projectable)
            {
                source.AppendLine($"{member.Name} = {SelectorExpression(member)},");
            }

            source.AppendCloseCurlyBracketLine();
            source.AppendLine(";");
        }

        if (trackChanges)
        {
            TrackingGenerator.EmitDtoTracking(source, members
                .Where(member => member.Source is not null)
                .Select(member => new TrackedMember
                {
                    Name = member.Name,
                    TypeText = member.DtoType,
                    IsValueType = !member.IsReference && !member.IsCollection && !member.IsEntity && member.Source.Type?.IsValueType == true,
                    IsCollection = member.IsCollection,
                    ElementTypeText = member.IsCollection ? ElementDtoType(member) : null,
                    Visibility = member.Source.WriteOnly ? "WriteOnly" : member.Source.ReadOnly ? "ReadOnly" : "ReadWrite"
                })
                .ToList(), TrackingGenerator.ModeName(classToGen));
        }

        source.AppendCloseCurlyBracketLine();
    }

    private static string Camel(string name)
        => string.IsNullOrEmpty(name) ? name : char.ToLowerInvariant(name[0]) + name.Substring(1);

    private static string FromEntityExpression(DtoMember member)
    {
        if (member.Source is null)
        {
            return "source.Properties is null ? null : new Dictionary<string, object>(source.Properties)";
        }

        if (member.IsCollection)
        {
            var elementFrom = ElementFromExpression(member);

            return $"source.{member.Name} is null ? new List<{ElementDtoType(member)}>() : source.{member.Name}.Select(item => {elementFrom}).ToList()";
        }

        if (member.IsReference)
        {
            if (!member.ExpandedRef)
            {
                return $"source.{member.Name}?.Id";
            }

            return member.NestedDtoType is null
                ? $"source.{member.Name}?.Item"
                : $"source.{member.Name}?.Item is null ? null : {member.NestedDtoType}.FromEntity(source.{member.Name}.Item)";
        }

        if (member.NestedDtoType is not null)
        {
            return $"source.{member.Name} is null ? null : {member.NestedDtoType}.FromEntity(source.{member.Name})";
        }

        return $"source.{member.Name}";
    }

    private static string SelectorExpression(DtoMember member)
        => member.IsReference ? $"source.{member.Name} != null ? source.{member.Name}.Id : null" : $"source.{member.Name}";

    private static string ElementFromExpression(DtoMember member)
    {
        if (member.IsReference)
        {
            return member.ExpandedRef ? (member.NestedDtoType is null ? "item?.Item" : $"item?.Item is null ? null : {member.NestedDtoType}.FromEntity(item.Item)") : "item?.Id";
        }

        return member.ElementDtoType is null ? "item" : $"item is null ? null : {member.ElementDtoType}.FromEntity(item)";
    }

    private static string ElementDtoType(DtoMember member)
    {
        var dtoType = member.DtoType;
        var open = dtoType.IndexOf('<');

        return dtoType.Substring(open + 1, dtoType.Length - open - 2);
    }

    private static string ToEntityStatement(DtoMember member)
    {
        var name = member.Name;

        if (member.Source is null)
        {
            return $"target.Properties = {name} is null ? null : new Dictionary<string, object>({name});";
        }

        if (member.IsCollection)
        {
            var reverse = ElementToExpression(member);
            var elementType = SourceCodeGenerator.RenderType(member.ElementType);

            return $"target.{name} = {name} is null ? new global::ObservableCollections.ObservableList<{elementType}>() : new global::ObservableCollections.ObservableList<{elementType}>({name}.Select(item => {reverse}));";
        }

        if (member.IsReference)
        {
            var target = SourceCodeGenerator.RenderType(member.ReferenceTarget);

            if (!member.ExpandedRef)
            {
                return $"target.{name} = {name} is null ? null : new Ref<{target}>({name});";
            }

            return $"target.{name} = {name} is null ? null : new Ref<{target}>({name}.Id);";
        }

        if (member.NestedDtoType is not null)
        {
            return $"target.{name} = {name} is null ? null : {name}.ToEntity();";
        }

        return $"target.{name} = {name};";
    }

    private static string ElementToExpression(DtoMember member)
    {
        if (member.IsReference)
        {
            var target = SourceCodeGenerator.RenderType(member.ReferenceTarget);

            return member.ExpandedRef ? $"new Ref<{target}>(item?.Id)" : $"new Ref<{target}>(item)";
        }

        return member.ElementDtoType is null ? "item" : "item?.ToEntity()";
    }

    private static string ResolveDto(ITypeSymbol type, IReadOnlyDictionary<string, string> knownDtos)
    {
        if (type is null || knownDtos is null)
        {
            return null;
        }

        var key = type.ToDisplayString();

        if (knownDtos.TryGetValue(key, out var dto))
        {
            return dto;
        }

        return knownDtos.TryGetValue(key.TrimEnd('?'), out dto) ? dto : null;
    }

    private static bool TryGetReferenceTarget(ITypeSymbol type, out ITypeSymbol target)
    {
        target = null;

        if (type is not INamedTypeSymbol { TypeArguments.Length: 1 } named)
        {
            return false;
        }

        var definition = named.OriginalDefinition.ToString();

        if (definition == "GoLive.Saturn.Data.Entities.Ref<T>" || definition == "GoLive.Saturn.Data.Entities.WeakRef<T>")
        {
            target = named.TypeArguments[0];
            return true;
        }

        return false;
    }

    private static bool IsEntityType(ITypeSymbol type)
    {
        var current = type as INamedTypeSymbol;

        while (current is not null)
        {
            if (current.ToDisplayString() == "GoLive.Saturn.Data.Entities.Entity")
            {
                return true;
            }

            current = current.BaseType;
        }

        return false;
    }
}
