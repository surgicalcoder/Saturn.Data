using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace GoLive.Saturn.Generator.Entities;

public static class SourceCodeGenerator
{
    public static void Generate(SourceStringBuilder source, ClassToGenerate classToGen)
    {
        source.AppendLine("using System.ComponentModel;");
        source.AppendLine("using System.Runtime.CompilerServices;");
        source.AppendLine("using GoLive.Generator.Saturn.Resources;");
        source.AppendLine("using GoLive.Saturn.Data.Entities;");
        source.AppendLine("using System.Collections.Specialized;");
        source.AppendLine("using FastMember;");

        source.AppendLine($"namespace {classToGen.Namespace};");

        source.AppendLine($"public partial class {classToGen.Name} : INotifyPropertyChanged");
        
        var updatableFromChildren = classToGen.Members.SelectMany(e => e.LimitedViews.Where(f => f.TwoWay).Select(f => f.Name)).Distinct().ToList();
        if (updatableFromChildren.Any())
        {
            foreach (var s in updatableFromChildren)
            {
                source.AppendLine($", IUpdatableFrom<{classToGen.Name}_{s}>");
                source.AppendLine($", ICreatableFrom<{classToGen.Name}_{s}>");
            }
        }
        
        source.AppendLine("{");
        source.AppendIndent();
            
        source.AppendLine($"public {classToGen.Name}()");
        source.AppendOpenCurlyBracketLine();
            
        foreach (var coll in classToGen.Members.Where(f=>f.IsCollection))
        {
            var collTargetName = coll.Name.FirstCharToUpper();
            source.AppendLine($"{collTargetName} = new();");
            
            source.AppendLine($"{collTargetName}.CollectionChanged += (in ObservableCollections.NotifyCollectionChangedEventArgs<{coll.CollectionType.ToDisplayString()}> eventArgs) => Changes.Upsert($\"{collTargetName}.{{eventArgs.NewStartingIndex}}\", eventArgs.NewItem);");
        }
        source.AppendCloseCurlyBracketLine();
            
        /*List<ITypeSymbol> typeAccessorsToCreate = new();

        foreach (var s in classToGen.Members.Where(f => !f.IsCollection).Select(f => f.Type).Distinct())
        {
            if (!typeAccessorsToCreate.Contains(s))
            {
                typeAccessorsToCreate.Add(s);
            }
        }

        foreach (var s in classToGen.Members.Where(f => f.CollectionType != null).Select(f => f.CollectionType).Distinct())
        {
            if (!typeAccessorsToCreate.Contains(s))
            {
                typeAccessorsToCreate.Add(s);
            }
        }

        bool addedRefAccessor = false;
            
        foreach (var s in typeAccessorsToCreate)
        {
            if (s.Name == "Ref" && ((INamedTypeSymbol)s).ConstructedFrom.ToString() == "GoLive.Saturn.Data.Entities.Ref<T>")
            {
                if (!addedRefAccessor)
                {
                    source.AppendLine($"TypeAccessor RefTypeAccessor = TypeAccessor.Create(typeof(GoLive.Saturn.Data.Entities.Ref<>));");
                    source.AppendLine();
                    addedRefAccessor = true;
                }
                continue;
            }
            var collTargetName = s.Name.FirstCharToUpper();
            source.AppendLine($"TypeAccessor {collTargetName}TypeAccessor = TypeAccessor.Create(typeof({s}));");
            source.AppendLine();
        }*/

        foreach (var member in classToGen.Members.Where(f=>!f.UseOnlyForLimited))
        {
            if (member.IsCollection)
            {
                GenerateCollectionMember(source, member);
            }
            else
            {
                GenerateNormalMember(source, member);
            }
                
        }
        

        if (updatableFromChildren.Any())
        {
            foreach (var s in updatableFromChildren)
            {
                source.AppendLine($"public void UpdateFrom({classToGen.Name}_{s} input) => input.UpdateParent(this);");
                source.AppendLine($"public static ICreatableFrom<{classToGen.Name}_{s}> Create({classToGen.Name}_{s} input )");
                source.AppendOpenCurlyBracketLine();
                source.AppendLine($"var item = new {classToGen.Name}();");
                source.AppendLine($"item.UpdateFrom(input);");
                source.AppendLine("");
                source.AppendLine("if (input is IUniquelyIdentifiable inputWithId)");
                source.AppendOpenCurlyBracketLine();
                source.AppendLine("item.Id = inputWithId.Id;");
                source.AppendCloseCurlyBracketLine();
                source.AppendLine("return item;");
                source.AppendCloseCurlyBracketLine();
            }
        }
        
        
        
        source.AppendLine("}");

        source.DecreaseIndent();
        source.AppendLine(2);
            
            
        foreach (var item in classToGen.Members.Where(r => r.LimitedViews.Any()).SelectMany(f => f.LimitedViews.Select(r => new { classDef = f, LimitedView = r }))
                     .GroupBy(e => e.LimitedView.Name))
        {
            
            source.AppendLine($"public partial class {classToGen.Name}_{item.Key} : IUpdatableFrom<{classToGen.Name}>, ICreatableFrom<{classToGen.Name}>");

            if (item.Any(e => e.LimitedView.TwoWay))
            {
                source.AppendLine($", IUpdatableFrom<{classToGen.Name}_{item.Key}>");
            }
            
            if (classToGen.ParentItemToGenerate is { Count: > 0 }  && (classToGen.ParentItemToGenerate.Any(r => r.ViewName == item.Key) || classToGen.ParentItemToGenerate.Any(r => r.ViewName == "*"))  )
            {
                bool addedUniquelyIdentifiable = false;
                foreach (var toGenerate in classToGen.ParentItemToGenerate.Where(r => r.ViewName == item.Key && r.InheritFromIUniquelyIdentifiable ))
                {
                    source.AppendLine(", IUniquelyIdentifiable");
                    addedUniquelyIdentifiable = true;
                    break;
                }

                if (!addedUniquelyIdentifiable)
                {
                    foreach (var toGenerate in classToGen.ParentItemToGenerate.Where(r=>r.ViewName == "*" && r.InheritFromIUniquelyIdentifiable))
                    {
                        source.AppendLine(", IUniquelyIdentifiable");
                        addedUniquelyIdentifiable = true;
                        break;
                    }
                }
            }
            
            source.AppendOpenCurlyBracketLine();

            foreach (var v1 in item)
            {
                var classDef = v1.classDef;
                outputAttributes(source, classDef);

                var initString = string.IsNullOrWhiteSpace(v1.LimitedView.Initializer) ? string.Empty : $" = {v1.LimitedView.Initializer};";
                
                if (string.IsNullOrWhiteSpace(v1.LimitedView.OverrideReturnTypeToUseLimitedView))
                {
                    source.AppendLine($"public {classDef.Type} {classDef.Name.FirstCharToUpper()} {{get;set;}} {initString}");
                }
                else
                {
                    if (classDef.Type is INamedTypeSymbol { IsGenericType: true } nts && nts.OriginalDefinition.ToDisplayString() == "GoLive.Saturn.Data.Entities.Ref<T>"  )
                    {
                        source.AppendLine($"public {nts.TypeArguments.FirstOrDefault().ToDisplayString()}_{v1.LimitedView.OverrideReturnTypeToUseLimitedView} {classDef.Name.FirstCharToUpper()} {{get;set;}} {initString}");
                    }
                    else
                    {
                        source.AppendLine($"public {classDef.Type}_{v1.LimitedView.OverrideReturnTypeToUseLimitedView} {classDef.Name.FirstCharToUpper()} {{get;set;}} {initString}");
                    }
                }
            }

            if (classToGen.ParentItemToGenerate is { Count: > 0 } && (classToGen.ParentItemToGenerate.Any(r=>r.ViewName == item.Key) || classToGen.ParentItemToGenerate.Any(r=>r.ViewName == "*") ))
            {
                source.AppendLine(2);

                foreach (var toGenerate in classToGen.ParentItemToGenerate.Where(r=>r.ViewName == item.Key))
                {
                    source.AppendLine($"public {toGenerate.Property.Type.ToDisplayString()} {toGenerate.ChildPropertyName} {{get;set;}}");
                }
                
                foreach (var toGenerate in classToGen.ParentItemToGenerate.Where(r=>r.ViewName == "*"))
                {
                    source.AppendLine($"public {toGenerate.Property.Type.ToDisplayString()} {toGenerate.ChildPropertyName} {{get;set;}}");
                }
            }
                
            source.AppendLine(2);

            outputViewUpdateFromMethod(source, classToGen, item.Key, item.Select(r => (r.classDef, r.LimitedView )) );
            outputViewUpdateFromSelfMethod(source, classToGen, item.Key, item.Select(r => (r.classDef, r.LimitedView )) );
            outputViewGenerateMethod(source, classToGen, item.Key);

            outputViewTwoWayMethod(source, classToGen, item.Key, item.Select(r => (r.classDef, r.LimitedView )));
            
            source.AppendLine($"public static ICreatableFrom<{classToGen.Name}> Create({classToGen.Name} input) => Generate(input);");
            
            source.AppendCloseCurlyBracketLine();
        }
    }

    private static void outputViewTwoWayMethod(SourceStringBuilder source, ClassToGenerate classToGen, string itemKey, IEnumerable<(MemberToGenerate classDef, LimitedViewToGenerate LimitedView)> item)
    {
        if (item.Any(e => e.LimitedView.TwoWay))
        {
            source.AppendLine($"public void UpdateParent({classToGen.Name} parent)");
            source.AppendOpenCurlyBracketLine();

            foreach (var v1 in item)
            {
                if (string.IsNullOrWhiteSpace(v1.LimitedView.OverrideReturnTypeToUseLimitedView))
                {
                    source.AppendLine($"parent.{v1.classDef.Name.FirstCharToUpper()} = this.{v1.classDef.Name.FirstCharToUpper()};");
                }
                else
                {
                    source.AppendLine($"parent.{v1.classDef.Name.FirstCharToUpper()} =  this.{v1.classDef.Name.FirstCharToUpper()}.Id;");
                    // TODO need to do this at one point, maybe with some interfaces
                }
            }
                
            source.AppendCloseCurlyBracketLine();
        }
    }
    private static void outputViewUpdateFromSelfMethod(SourceStringBuilder source, ClassToGenerate classToGen, string itemKey, IEnumerable<(MemberToGenerate classDef, LimitedViewToGenerate LimitedView)> item)
    {
        if (item.Any(e => e.LimitedView.TwoWay))
        {
            source.AppendLine($"public void UpdateFrom({classToGen.Name}_{itemKey} self)");
            source.AppendOpenCurlyBracketLine();

            foreach (var v1 in item)
            {
                source.AppendLine($"this.{v1.classDef.Name.FirstCharToUpper()} = self.{v1.classDef.Name.FirstCharToUpper()};");
            }
                
            source.AppendCloseCurlyBracketLine();
        }
    }

    private static void outputViewUpdateFromMethod(SourceStringBuilder source, ClassToGenerate classToGen, string itemKey, IEnumerable<(MemberToGenerate classDef, LimitedViewToGenerate LimitedView)> item)
    {
        source.AppendLine($"public void UpdateFrom({classToGen.Name} source)");
        source.AppendOpenCurlyBracketLine();
        foreach (var v1 in item)
        {
            if (string.IsNullOrWhiteSpace(v1.LimitedView.OverrideReturnTypeToUseLimitedView))
            {
                source.AppendLine($"this.{v1.classDef.Name.FirstCharToUpper()} = source.{v1.classDef.Name.FirstCharToUpper()};");
            }
            else
            {
                
                if (v1.classDef.Type is INamedTypeSymbol { IsGenericType: true } nts && nts.OriginalDefinition.ToDisplayString() == "GoLive.Saturn.Data.Entities.Ref<T>"  )
                {
                    source.AppendLine($"this.{v1.classDef.Name.FirstCharToUpper()} = {nts.TypeArguments.FirstOrDefault().ToDisplayString()}_{v1.LimitedView.OverrideReturnTypeToUseLimitedView}.Generate(source.{v1.classDef.Name.FirstCharToUpper()}); ");
                }
                else
                {
                    source.AppendLine($"this.{v1.classDef.Name.FirstCharToUpper()} = {v1.classDef.Type}_{v1.LimitedView.OverrideReturnTypeToUseLimitedView}.Generate(source.{v1.classDef.Name.FirstCharToUpper()}); ");
                }
            }
        }
        
        if (classToGen.ParentItemToGenerate is { Count: > 0 } && (classToGen.ParentItemToGenerate.Any(r=>r.ViewName == itemKey) || classToGen.ParentItemToGenerate.Any(r=>r.ViewName == "*") ))
        {
            source.AppendLine(2);

            foreach (var toGenerate in classToGen.ParentItemToGenerate.Where(r=>r.ViewName == itemKey))
            {
                source.AppendLine($"this.{toGenerate.ChildPropertyName} = source.{toGenerate.PropertyName};");
            }
            
            foreach (var toGenerate in classToGen.ParentItemToGenerate.Where(r=>r.ViewName == "*"))
            {
                source.AppendLine($"this.{toGenerate.ChildPropertyName} = source.{toGenerate.PropertyName};");
            }
        }
        
        source.AppendLine();
        source.AppendCloseCurlyBracketLine();
    }
    
    private static void outputViewGenerateMethod(SourceStringBuilder source, ClassToGenerate classToGen, string itemKey)
    {
        source.AppendLine($"public static {classToGen.Name}_{itemKey} Generate({classToGen.Name} source)");
        source.AppendOpenCurlyBracketLine();
        source.AppendLine($"var retr = new {classToGen.Name.FirstCharToUpper()}_{itemKey}();");
        source.AppendLine("retr.UpdateFrom(source);");
        source.AppendLine("return retr;");
        source.AppendCloseCurlyBracketLine();
    }

    private static void outputAttributes(SourceStringBuilder source, MemberToGenerate classDef)
    {
        if (classDef.AdditionalAttributes.Count <= 0)
        {
            return;
        }

        foreach (var attr in classDef.AdditionalAttributes)
        {
            var builder = new StringBuilder();
            builder.Append($"[{attr.Name}(");

            if (attr.ConstructorParameters.Count > 0)
            {
                foreach (var attrConstructorParameter in attr.ConstructorParameters)
                {
                    builder.Append($"{attrConstructorParameter},");
                }
            }

            if (attr.NamedParameters.Count > 0)
            {
                foreach (var attrNamedParameter in attr.NamedParameters)
                {
                    builder.Append($"{attrNamedParameter.Key}={attrNamedParameter.Value},");
                }
            }

            builder.AppendLine(")]");
                
            source.AppendLine(builder.ToString().Replace(",)]", ")]")); // TODO need to fix
        }
    }

    private static void GenerateCollectionMember(SourceStringBuilder source, MemberToGenerate item)
    {
        var itemName = item.Name;
        source.AppendLine($"public ObservableCollections.ObservableList<{item.CollectionType}> {itemName.FirstCharToUpper()}");
        source.AppendOpenCurlyBracketLine();
        source.AppendLine($"get => {itemName};");
        source.AppendLine($"set => SetField(ref {itemName}, value);");
        source.AppendCloseCurlyBracketLine();
    }

    private static void GenerateNormalMember(SourceStringBuilder source, MemberToGenerate item)
    {
        var itemName = item.Name;
        outputAttributes(source, item);
        source.AppendLine($"public {item.Type} {itemName.FirstCharToUpper()}");
        source.AppendOpenCurlyBracketLine();
            
        if (!item.WriteOnly)
        {
            source.AppendLine($"get => {itemName};");
        }

        if (item.IsScoped)
        {
            source.AppendLine($@"set
        {{
            if (value != null && !string.IsNullOrWhiteSpace(value.Id))
            {{
                if ({itemName} != null && !string.IsNullOrWhiteSpace({itemName}.Id) && Scopes.Contains({itemName}.Id) && {itemName}.Id != value.Id )
                {{
                    Scopes.Remove({itemName}.Id);
                }}

                if (!Scopes.Contains(value.Id))
                {{
                    Scopes.Add(value.Id);
                }}

                SetField(ref {itemName}, value.Id);
            }}
            else
            {{
                if ({itemName} != null && !string.IsNullOrWhiteSpace({itemName}.Id) && Scopes.Contains({itemName}.Id))
                {{
                    Scopes.Remove({itemName}.Id);
                    SetField(ref {itemName}, string.Empty);
                }}
            }}
        }}");
        } else if (!item.ReadOnly)
        {
            source.AppendLine($"set => SetField(ref {itemName}, value);");
        }
            
        source.AppendCloseCurlyBracketLine();
    }
}