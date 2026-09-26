# Phase B — DTO Generation and Mapping

**Goal:** Add a first-class `[GenerateDto]` option that emits `{Entity}Dto` transport classes with recursive, null-safe, reflection-free mapping to and from the entity, plus translation-safe projection selectors.

**Prerequisite:** Phase A complete and its tests green.

**Exit criteria:**
- `[GenerateDto] public partial class Order : Entity { ... }` emits `OrderDto`, and an assembly-level `SaturnGenerateDtos=true` generates DTOs by default with `[NoGenerateDto]` opting out.
- `OrderDto.FromEntity` / `ToEntity` / `ApplyTo` round-trip scalars, `Ref<T>`, embedded entities, and collections.
- DTO identity uses `_shortId` by default; `[GenerateDto(UseFullId = true)]` uses the 24-hex id.
- `Ref<T>` maps to an id string by default; `[GenerateDto(ExpandRefs = true)]`/`[Embedded]` auto-expands a nested DTO for display.
- `Selector` compiles and, when non-null, is translation-safe (no user-defined conversions inside the expression tree).
- Mapping is null-safe: null entity, null collection, unpopulated ref, and populated ref all behave predictably.
- Attributes and nullability from Phase A are preserved on DTO members.
- DTOs emit tracking hooks when opted in (`[GenerateDto(TrackChanges = true)]` or `[ChangeTracking]`); full behavior lands in Phase C.

---

## Task B.1 — Add DTO attributes

Create `D:\Work\Saturn.Data\Saturn.Generator.Entities\Saturn.Generator.Entities.Resources\GenerateDtoAttribute.cs`:

```csharp
using System;

namespace GoLive.Saturn.Generator.Entities.Resources;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class GenerateDtoAttribute : Attribute
{
    public string Name { get; set; }

    public bool IncludeProperties { get; set; }

    public bool TrackChanges { get; set; }

    public bool ExpandRefs { get; set; }

    public bool UseFullId { get; set; }
}
```

Create `NoGenerateDtoAttribute.cs` (opt-out for the assembly-level default):

```csharp
using System;

namespace GoLive.Saturn.Generator.Entities.Resources;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class NoGenerateDtoAttribute : Attribute { }
```

Create `ExcludeFromDtoAttribute.cs`:

```csharp
using System;

namespace GoLive.Saturn.Generator.Entities.Resources;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public class ExcludeFromDtoAttribute : Attribute { }
```

Create `EmbeddedAttribute.cs`:

```csharp
using System;

namespace GoLive.Saturn.Generator.Entities.Resources;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public class EmbeddedAttribute : Attribute { }
```

---

## Task B.2 — Extend the model

`MemberToGenerate` gains:

```csharp
public MemberCategory Category { get; set; }
public string ElementTypeName { get; set; }
public bool IsEmbedded { get; set; }
public bool ExcludeFromDto { get; set; }
```

```csharp
public enum MemberCategory
{
    Scalar,
    String,
    Enum,
    Reference,
    EmbeddedEntity,
    Collection
}
```

`ClassToGenerate` gains:

```csharp
public bool GenerateDto { get; set; }
public bool GenerateDtosByDefault { get; set; }
public string DtoName { get; set; }
public bool DtoTrackChanges { get; set; }
public bool DtoExpandRefs { get; set; }
public bool DtoUseFullId { get; set; }
public bool DtoIncludeProperties { get; set; }
```

Populate in `Scanner.ConvertToMapping` / `ConvertToMembers`:
- Read `[GenerateDto]` on the class symbol.
- Default `DtoName` to `{ClassName}Dto`.
- Mark `ExcludeFromDto` when the member carries `[ExcludeFromDto]` or `[DoNotTrackChanges]` or is `[WriteOnly]`.
- Classify `Category`:
  - `string` → `String`
  - `enum` → `Enum`
  - `Ref<>` / `WeakRef` / `WeakRef<>` → `Reference`
  - derives from `Entity` → `EmbeddedEntity`
  - implements `IEnumerable<T>` and is not `string` → `Collection` (record `ElementTypeName`)
  - otherwise → `Scalar`
- Never include `Changes`, `EnableChangeTracking`, `Properties`, get-only members, or `[ExcludeFromDto]` in the DTO.

### Resolution: assembly default + per-class override (decision 3)

`GenerateDto` is resolved as:

| Assembly default (`SaturnGenerateDtos`) | Class attribute | Result |
| --- | --- | --- |
| absent / `false` | `[GenerateDto]` | generate |
| absent / `false` | none | do not generate |
| `true` | `[NoGenerateDto]` | do not generate |
| `true` | `[GenerateDto]` or none | generate |

Read the MSBuild property in the generator pipeline:

```csharp
private static bool ReadBoolOption(AnalyzerConfigOptionsProvider provider, string name, bool fallback)
{
    return provider.GlobalOptions.TryGetValue($"build_property.{name}", out var value)
           && bool.TryParse(value, out var parsed)
        ? parsed
        : fallback;
}
```

Set `GenerateDtosByDefault = ReadBoolOption(provider, "SaturnGenerateDtos", false)` on the model. The consumer opts in assembly-wide via:

```xml
<PropertyGroup>
    <SaturnGenerateDtos>true</SaturnGenerateDtos>
</PropertyGroup>
```

Note: `[ChangeTracking]` (on the DTO or its entity) lives in `GoLive.Saturn.Data.ChangeTracking` (decision 1). DTO change tracking is emitted in Phase C; `[GenerateDto(TrackChanges = true)]` or `[ChangeTracking]` selects it.

---

## Task B.3 — Emit `{Entity}Dto`

In `SourceCodeGenerator`, add an emission method invoked when `ClassToGenerate.GenerateDto` is true. Target output:

```csharp
// <auto-generated/>
#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using GoLive.Saturn.Data.Entities;
using GoLive.Saturn.Generator.Entities.Resources;

namespace Sample;

public partial class OrderDto : INotifyPropertyChanged, IUpdatableFrom<Order>, ICreatableFrom<Order>, IUniquelyIdentifiable
{
    private string? customerName;

    public string Id { get; set; }

    public string? CustomerName
    {
        get => customerName;
        set
        {
            if (EqualityComparer<string?>.Default.Equals(customerName, value))
            {
                return;
            }

            customerName = value;
            OnPropertyChanged(nameof(CustomerName));
        }
    }

    public decimal Total { get; set; }

    public List<OrderLineDto> Lines { get; set; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public static implicit operator OrderDto(Order source)
    {
        return FromEntity(source);
    }

    public static implicit operator OrderDto?(Ref<Order> source)
    {
        return source?.Item is null ? null : FromEntity(source.Item);
    }

    public static OrderDto FromEntity(Order source)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        var dto = new OrderDto
        {
            Id = source._shortId,
            CustomerName = source.CustomerName,
            Total = source.Total,
            Lines = source.Lines is null ? new List<OrderLineDto>() : source.Lines.Select(OrderLineDto.FromEntity).ToList(),
        };

        return dto;
    }

    public static OrderDto? FromRef(Ref<Order>? source)
    {
        return source?.Item is null ? null : FromEntity(source.Item);
    }

    public static ICreatableFrom<Order> Create(Order input)
    {
        return FromEntity(input);
    }

    public Order ToEntity()
    {
        var entity = new Order
        {
            Id = Id,
            CustomerName = CustomerName,
            Total = Total,
            Lines = Lines is null ? new List<OrderLine>() : Lines.Select(line => line.ToEntity()).ToList(),
        };

        return entity;
    }

    public void ApplyTo(Order target)
    {
        if (target is null)
        {
            throw new ArgumentNullException(nameof(target));
        }

        target.CustomerName = CustomerName;
        target.Total = Total;
        target.Lines = Lines is null ? new List<OrderLine>() : Lines.Select(line => line.ToEntity()).ToList();
    }

    public void UpdateFrom(Order source)
    {
        if (source is null)
        {
            return;
        }

        Id = source._shortId;
        CustomerName = source.CustomerName;
        Total = source.Total;
        Lines = source.Lines is null ? new List<OrderLineDto>() : source.Lines.Select(OrderLineDto.FromEntity).ToList();
    }
}
```

Rules for the emitter:

1. **Id:** always emit `public string Id { get; set; }`; map to `source._shortId` by default, `source.Id` when `UseFullId` is set. `ToEntity`/`ApplyTo` assign `Id` (the `Entity.Id` setter accepts 16-char base64url and 24-char hex).
2. **Scalar / String / Enum / DateTime:** direct assignment.
3. **Embedded entity** (`Category == EmbeddedEntity` or `[Embedded]`): if the member type has `[GenerateDto]`, map to `{MemberType}Dto` recursively; otherwise map the entity reference directly.
4. **Reference (`Ref<T>`/`WeakRef`):**
   - Default: DTO property is `string?`; map `source.X.Id` (write) and `new Ref<T>(value)` / implicit string assignment (read back).
   - `DtoExpandRefs` (or `[Embedded]`): DTO property is `{T}Dto?`; map `source.X?.Item is null ? null : {T}Dto.FromEntity(source.X.Item)`.
5. **Collection:** DTO property is `List<{ElementDto}>`; map with `is null ? new List<...>() : source.X.Select(...).ToList()`. For element types that are scalars, emit `source.X.ToList()` directly.
6. **HashedString / EncryptedString:** keep the runtime type in the DTO (do not flatten to string) so crypto semantics survive.
7. **ApplyTo / ToEntity** ignore `Id`, `Version`, and members the DTO did not expose.
8. Emit `INotifyPropertyChanged` for all non-auto properties if Phase F tracking will be enabled; Phase C wires tracking.

---

## Task B.4 — Translation-safe `Selector`

Emit `Selector` only when every DTO member mapping translates to an expression the providers accept:

```csharp
public static Expression<Func<Order, OrderDto>>? Selector => source => new OrderDto
{
    Id = source._shortId,
    CustomerName = source.CustomerName,
    Total = source.Total,
};
```

Rules:

- Include only `Scalar`, `String`, `Enum`, embedded scalar paths, and reference **id** projections (`source.X.Id`).
- Exclude collection expansions, nested `{T}Dto` conversions, and any user-defined conversion from the expression.
- If any member is excluded, still emit a `Selector` for the projectable subset **only if** the DTO can be constructed without the excluded members (they must have settable defaults). Otherwise emit `Selector => null` and document it.
- Never call `FromEntity`/`ToEntity` inside `Selector`.

Add a generator test asserting the emitted `Selector` contains no `FromEntity`/implicit-conversion calls.

---

## Task B.5 — Naming, versioning, and collision safety

- `DtoName` default `{ClassName}Dto`; override via `[GenerateDto(Name = "...")]`.
- Identity: `public string Id { get; set; }` maps `source._shortId` by default (decision 4); `[GenerateDto(UseFullId = true)]` maps `source.Id`.
- Emit `public const string DtoSchemaVersion = "1";` so consumers can detect contract changes.
- If a type named `{ClassName}Dto` already exists in the compilation (user-defined), do not emit; assume the user supplied it. Detect via semantic model in the transform step and set a flag.

---

## Task B.6 — Tests

Add to `Saturn.Generator.Entities.Tests`:

- `Dto_Is_Generated_For_GenerateDto_Entity`
- `Dto_Is_Generated_By_Assembly_Default`
- `NoGenerateDto_Opts_Out_Of_Assembly_Default`
- `Dto_Maps_Unpopulated_Ref_Without_Throwing` (maps to null id)
- `Dto_Maps_Populated_Ref_As_Id_By_Default`
- `Dto_Expands_Ref_When_ExpandRefs_Set`
- `Dto_Uses_ShortId_By_Default_And_FullId_When_Configured`
- `Dto_Maps_Collection_Recursively`
- `Dto_Preserves_Nullable_And_Attributes`
- `Dto_Selector_Contains_No_Conversions`
- `Dto_Emits_Tracking_Hooks_When_Opted_In`
- `Dto_RoundTrip_Preserves_Values` (generate + compile + invoke via reflection in the test, or unit-test the emitted text)

For runtime round-trip tests, compile the generated source with `GeneratorTestHarness.CreateCompilation`, emit to a `MemoryStream`, load the assembly, and invoke `FromEntity`/`ToEntity` via reflection against a fixture entity. Keep this in a separate `CompilationTests` class.

---

## Task B.7 — Build and test

```powershell
dotnet build "D:\Work\Saturn.Data\Saturn.Data.slnx" -c Debug
dotnet build "D:\Work\Saturn.Data\Saturn.Generator.Entities\Saturn.Generator.Entities.Playground\Saturn.Generator.Entities.Playground.csproj"
dotnet test "D:\Work\Saturn.Data\Saturn.Generator.Entities\Saturn.Generator.Entities.Tests\Saturn.Generator.Entities.Tests.csproj"
```

Add a `[GenerateDto]` example to the playground and inspect `obj\Generated`.

---

## Do NOT

- Do not modify or re-emit existing `{Entity}_{View}` classes; they stay as-is.
- Do not use reflection in generated mapping.
- Do not put `FromEntity`/`ToEntity`/implicit conversions inside `Selector`.
- Do not expose `Changes`, `EnableChangeTracking`, `Properties`, or `[WriteOnly]` members on the DTO.
- Do not add comments to emitted code beyond `// <auto-generated/>`.
