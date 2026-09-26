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
    }

    public static void Generate(SourceStringBuilder source, ClassToGenerate classToGen, bool expandRefs, bool useFullId, string namespaceName)
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
                var dtoElement = elementReference ? (expandRefs ? SourceCodeGenerator.RenderType(referenceTarget) : "string?") : SourceCodeGenerator.RenderType(element);

                members.Add(new DtoMember
                {
                    Source = member,
                    Name = name,
                    IsCollection = true,
                    IsReference = elementReference,
                    ElementType = element,
                    ReferenceTarget = referenceTarget,
                    DtoType = $"List<{dtoElement}>",
                    Projectable = false
                });

                continue;
            }

            if (TryGetReferenceTarget(member.Type, out var target))
            {
                members.Add(new DtoMember
                {
                    Source = member,
                    Name = name,
                    IsReference = true,
                    ReferenceTarget = target,
                    DtoType = expandRefs ? SourceCodeGenerator.RenderType(target) : "string?",
                    Projectable = !expandRefs
                });

                continue;
            }

            members.Add(new DtoMember
            {
                Source = member,
                Name = name,
                IsEntity = IsEntityType(member.Type),
                DtoType = SourceCodeGenerator.RenderType(member.Type),
                Projectable = !IsEntityType(member.Type) && member.Type.TypeKind != TypeKind.Interface
            });
        }

        var dtoName = classToGen.DtoName;
        var entityName = classToGen.Name;

        source.AppendLine(2);
        source.AppendLine("[global::System.CodeDom.Compiler.GeneratedCode(\"Saturn.Generator.Entities\", \"1.0.0\")]");
        source.AppendLine($"public partial class {dtoName} : ICreatableFrom<{entityName}>, IUpdatableFrom<{entityName}>");
        source.AppendOpenCurlyBracketLine();

        source.AppendLine("public const string DtoSchemaVersion = \"1\";");
        source.AppendLine(2);
        source.AppendLine("public string? Id { get; set; }");
        source.AppendLine();

        foreach (var member in members)
        {
            source.AppendLine($"public {member.DtoType} {member.Name} {{ get; set; }}");
        }

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

        var writableMembers = members.Where(m => !m.Source.ReadOnly).ToList();

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

        source.AppendCloseCurlyBracketLine();
    }

    private static string FromEntityExpression(DtoMember member)
    {
        if (member.IsCollection)
        {
            var elementFrom = ElementFromExpression(member);

            return $"source.{member.Name} is null ? new List<{ElementDtoType(member)}>() : source.{member.Name}.Select(item => {elementFrom}).ToList()";
        }

        if (member.IsReference)
        {
            return member.DtoType == "string?" ? $"source.{member.Name}?.Id" : $"source.{member.Name}?.Item";
        }

        return $"source.{member.Name}";
    }

    private static string SelectorExpression(DtoMember member)
        => member.IsReference ? $"source.{member.Name} != null ? source.{member.Name}.Id : null" : $"source.{member.Name}";

    private static string ElementFromExpression(DtoMember member)
        => member.IsReference ? (member.DtoType.StartsWith("List<string") ? "item?.Id" : "item?.Item") : "item";

    private static string ElementDtoType(DtoMember member)
    {
        var dtoType = member.DtoType;
        var open = dtoType.IndexOf('<');

        return dtoType.Substring(open + 1, dtoType.Length - open - 2);
    }

    private static string ToEntityStatement(DtoMember member)
    {
        var name = member.Name;

        if (member.IsCollection)
        {
            var reverse = ElementToExpression(member);
            return $"target.{name} = {name} is null ? new global::ObservableCollections.ObservableList<{SourceCodeGenerator.RenderType(member.ElementType)}>() : new global::ObservableCollections.ObservableList<{SourceCodeGenerator.RenderType(member.ElementType)}>({name}.Select(item => {reverse}));";
        }

        if (member.IsReference)
        {
            var target = SourceCodeGenerator.RenderType(member.ReferenceTarget);

            return member.DtoType == "string?"
                ? $"target.{name} = {name} is null ? null : new Ref<{target}>({name});"
                : $"target.{name} = {name} is null ? null : new Ref<{target}>({name}.Id);";
        }

        return $"target.{name} = {name};";
    }

    private static string ElementToExpression(DtoMember member)
        => member.IsReference ? (member.DtoType.StartsWith("List<string") ? $"new Ref<{SourceCodeGenerator.RenderType(member.ReferenceTarget)}>(item)" : $"new Ref<{SourceCodeGenerator.RenderType(member.ReferenceTarget)}>(item?.Id)") : "item";

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
