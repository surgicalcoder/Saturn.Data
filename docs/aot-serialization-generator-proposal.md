# Proposal: AOT-Enabling Saturn.Data with Generated Serialization Metadata

**Status:** Draft for review — feasibility verdict first, phases later
**Author:** Axiom
**Date:** 2026-09-30
**Scope:** Decide whether consumer-facing serialization generators can turn every Saturn.Data provider — MongoDb, LiteDbX, Stellar, Sqlite, DocumentDb — into NativeAOT/trim-safe deployments, and design the work if so.
**Companion:** `docs/aot-mongodb-investigation.md` — empirical NativeAOT spike results for MongoDB driver 3.12.0 (the evidence behind the Mongo verdict).

---

## 1. Executive summary — the direct answer

**Can we register a custom serializer per type for LiteDbX and MongoDB? Yes — for both, and that is the right mechanism.**

- **LiteDB / LiteDbX:** `BsonMapper.RegisterType<T>(serialize, deserialize)` (and the non-generic overload) is a *full per-type override*. `BsonMapper.Serialize` checks the custom map **before** `SerializeObject`, and `Deserialize` checks it **before** any document/`EntityMapper` path (`BsonMapper.Serialize.cs:47-51`, `BsonMapper.Deserialize.cs:88-92`). A registered entity type therefore never routes through `EntityMapper` reflection for IO.
- **MongoDB:** `BsonSerializer.RegisterSerializer(typeof(X), serializer)` / `[BsonSerializer(typeof(X))]` is likewise per-type. A registered `IBsonSerializer<X>` bypasses `BsonClassMap` for `X`, and implementing `IBsonDocumentSerializer` keeps LINQ member resolution working (the pattern your `RefSerializer.TryGetMemberSerializationInfo` already uses, `RefSerializer.cs:70-93`).

**But per-type serializers solve entity IO, not the query layer or engine internals.** That residue is the real dividing line:

1. **Our serialization layer** (entity graph IO, refs, crypto strings, `Properties`, discriminators). Per-type custom serializers + generated metadata eliminate almost all reflection and dynamic code. ✅ Fixable by us.
2. **Query/engine internals.** For **LiteDbX** this is *mostly our code*: `BsonMapper.GetExpression` → `LinqExpressionVisitor` resolves field names via `EntityMapper`/`MemberInfo` reflection (`BsonMapper.cs:186-205`), and our provider calls it (`LiteDbRepository.cs:133,169,411`). We can replace that with raw `BsonExpression`/`Query` built from generated field names — no fork. ✅ Largely fixable by us, needs a spike of the unannotated LiteDB core.
   For **MongoDB** the spike (companion doc) splits the verdict: the **cluster/connection layer works under AOT once serializers are pre-registered**, and raw `BsonDocument` queries work, but the **LINQ provider does not** — it performs runtime generic instantiation (`MakeGenericType`, e.g. `ArraySerializer<bool>`) and `Expression.Lambda` partial evaluation. ⛔ LINQ is not replaceable from outside; ✅ entity IO + raw BSON queries are.
3. **Saturn's cross-cutting runtime** (cascade discovery, scope model, projection, live-type dispatch). Generated metadata replaces it. ✅ Fixable by us, but it is a *second* generator job, not the serialization one.

The corrected provider verdict:

| Provider | Per-type serializer works? | Remaining blockers | Verdict |
| --- | --- | --- | --- |
| **Stellar** (MessagePack / FastDB) | Yes — MessagePack source-gen formatters | Ours (drop `ContractlessStandardResolver`, `dynamic`) | **Achievable** |
| **Sqlite** (System.Text.Json) | Yes — generated `JsonConverter<T>` | All ours | **Achievable** |
| **DocumentDb** (Shiny.DocumentDb) | Yes for our side | Library needs per-type `JsonTypeInfo<T>`/`ConfigureDocument<T>` | **Achievable with constraints** |
| **LiteDbX** (LiteDB fork) | Yes — `BsonMapper.RegisterType<T>` bypasses `EntityMapper` for IO | Query/index member resolution reflects (ours to fix) + unannotated LiteDB core | **Achievable for IO; full AOT gated on a query rewrite + spike** |
| **MongoDb** (official .NET/C# driver 3.12) | Yes — `BsonSerializer.RegisterSerializer` + `IBsonDocumentSerializer` bypasses `BsonClassMap` for IO, given a serializer pre-registration harness | LINQ query pipeline does runtime generic instantiation + `Expression.Lambda`; unfixable. Cluster/connection layer and raw BSON queries work once serializers are pre-registered | **Partial — achievable in an expression-free AOT mode (generated serializers + registration harness + raw BSON queries); LINQ must be disabled. Proven by spike (companion doc)** |

The honest headline: **"AOT everywhere" is not a serialization feature; it is a per-provider compatibility programme.** Per-type serializers are necessary and supported by both BSON vendors; whether they are *sufficient* depends on whether the remaining reflection is in code we own (LiteDbX → yes, fixable) or in a vendor's code (Mongo → no, spike-gated). Stellar/Sqlite/DocumentDb are fully ours and achievable now.

The remainder of this document justifies that verdict with concrete call sites, then designs the generator and the migration.

---

## 2. What "AOT enabled" actually forbids

NativeAOT (`<PublishAot>true</PublishAot>`) plus `<IsAotCompatible>true</IsAotCompatible>` and the trim/AOT analyzers permit no runtime code generation and almost no unrooted reflection. The practical hard constraints:

| Forbidden at runtime | Why it matters here |
| --- | --- |
| `Reflection.Emit` / `DynamicMethod` | Backing for `Expression.Compile()` |
| `Expression.Compile()` (without `preferInterpretation:true`) | Any LINQ provider that compiles lambdas |
| `System.Reflection` member access not rooted by annotations or source-gen | `GetProperties`, `GetProperty`, `GetFields`, `GetMethod`, `GetCustomAttribute`, … |
| `Type.GetType(string)` | Polymorphic discriminator resolution |
| `Activator.CreateInstance` on unrooted types | Open-generic refs, mapper construction |
| Unrooted `MakeGenericType` / `MakeGenericMethod` | Open-generic `Ref<>`, cascade dispatch |
| `dynamic` / DLR | `Entity.Properties`, `ObjectSerializer`, Stellar cascade |
| STJ `DefaultJsonTypeInfoResolver` reflection fallback, and `JsonConverterFactory` creating closed converters via reflection | JSON providers |
| `BsonClassMap.AutoMap()` and any non-registered class map; LiteDB `EntityMapper` | The MongoDB and LiteDB mappers — **but a registered per-type serializer bypasses both** |
| Assembly scanning (`AppDomain.CurrentDomain.GetAssemblies`) | The generator itself (!)—see §10 R8 |

**What a source generator can fix:** per-type serializers and their registrations for types the consumer's build can see; per-type dispatch tables (cascade/scope/collection names); typed converters that replace runtime resolution.

**What a source generator cannot fix:** reflection and code generation performed inside a *vendor assembly at runtime* (Mongo's LINQ/cluster layer; the LiteDB core). If the vendor calls `Expression.Compile()` or `Type.GetType()` on a path we cannot avoid, no amount of generated metadata on our side prevents it.

This distinction is the whole answer.

---

## 3. Where the blockers are today (evidence)

### 3.1 The generator currently emits no serialization metadata

`SaturnGenerator` (`Saturn.Generator.Entities/Saturn.Generator.Entities/SaturnGenerator.cs:11-67`) runs `Scanner` → `ClassToGenerate` → `SourceCodeGenerator.Generate`. It emits partials, backing fields, limited views, DTOs (`DtoGenerator.cs`), and tracking (`TrackingGenerator.cs`) — and a grep for `JsonSerializable|JsonSerializerContext|Bson|IBsonSerializer` across the generator tree returns **nothing**. The only serialization-adjacent behaviour is that arbitrary non-Saturn attributes are copied onto generated members (`Scanner.IsCopyableAttribute`, `Scanner.cs:511-543, 613-634`).

So today every provider's serialization is reflection-driven at runtime.

### 3.2 MongoDb

| Blocker | Location |
| --- | --- |
| Class-map automation (reflection over members) | `MongoDbRepository.RegisterConventions` → `BsonClassMap.RegisterClassMap<Entity>(…map.AutoMap()…)` (`MongoDbRepository.cs:437-452`) |
| `dynamic` properties bag | `map.MapProperty(e => e.Properties).SetSerializer(new DictionaryInterfaceImplementerSerializer<Dictionary<string, dynamic>>(...))` (`MongoDbRepository.cs:447`); `new ObjectSerializer(...)` (`:390`) |
| Open-generic serializer registration → runtime `MakeGenericType` | `BsonSerializer.RegisterGenericSerializerDefinition(typeof(Ref<>), typeof(RefSerializer<>))` and same for `WeakRef<>` (`MongoDbRepository.cs:399-400`) |
| Reflection over struct fields/properties at IO time | `BasicStructSerializer<T>` — `nominalType.GetFields/GetProperties` (`BasicStructSerializer.cs:15-16`, `58-69`) |
| `Type.GetType(string)` for polymorphism | `FullTypeNameDiscriminatorConvention.GetActualType` (`FullTypeNameDiscriminatorConvention.cs:33`) |
| `Activator.CreateInstance` in conventions | `IgnoreEmptyArraysConvention` (`IgnoreEmptyArraysConvention.cs:50-62`) |
| Runtime generic method construction in query rewriting | `RefExpressionRewriter` — `MakeGenericMethod`, `GetMethod` (`RefExpressionRewriter.cs:131,164-178`) |
| Runtime generic method construction in cascade | `MongoCascadeExecutor` — `GetMethod(...).MakeGenericMethod(...)` (`MongoCascadeExecutor.cs:37-38,83`) |
| Driver-internal LINQ translation + serializer resolution | `Builders<TItem>.Filter`, `IMongoCollection<T>.Find`, projections; not in our source |
| **Per-type custom serializers ARE supported** (the fix) | `BsonSerializer.RegisterSerializer(typeof(X), serializer)` / `[BsonSerializer(typeof(X))]`; a registered `IBsonSerializer<X>` bypasses `BsonClassMap` for `X`; implementing `IBsonDocumentSerializer` keeps LINQ member resolution working (pattern already in `RefSerializer.cs:70-93`) |
| Driver has no AOT support | No AOT/trim page in docs; only AOT effort `CSHARP-3218` **abandoned in 2020**; assemblies carry **0** `RequiresUnreferencedCode`/`RequiresDynamicCode`/`IsTrimmable` markers, so Roslyn analyzers stay silently green |
| **AOT spike result (driver 3.12.0, .NET 10, `PublishAot`)** | `AutoMap`/primitive serializer lookups throw `MissingMethodException`; **with all serializers pre-registered, entity serialize/deserialize, raw `BsonDocument` filters and `MongoClient` connect+query work under AOT**, but `Builders<T>.Filter.Where` fails with `'ArraySerializer<Boolean>' is missing native code or metadata`. Full detail: `docs/aot-mongodb-investigation.md` |

### 3.3 LiteDbX

| Blocker | Location |
| --- | --- |
| Open-generic `Ref<>` mapper built by reflection | `CustomEntityMapper` — `type.GetProperty(...)`, `Activator.CreateInstance(type)`, `property.SetValue` (`EntityMapper.cs:19-55`) |
| **Per-type custom serializers ARE supported** (the fix) | `BsonMapper.RegisterType<T>(serialize, deserialize)` / `RegisterType(Type, …)` (`BsonMapper.cs:116-126`); `Serialize` checks the custom map *before* `SerializeObject` (`BsonMapper.Serialize.cs:47-51`) and `Deserialize` checks it before any document/`EntityMapper` path (`BsonMapper.Deserialize.cs:88-92`) — a registered type bypasses `EntityMapper` entirely |
| Query/index member resolution (remains) | `BsonMapper.GetExpression` → `LinqExpressionVisitor` resolves member names through `EntityMapper`/`MemberInfo` (`BsonMapper.cs:186-205`); called by `BsonMapper.Global.GetExpression(predicate)` (`LiteDbRepository.cs:133,169,411`) |
| LiteDB core is not trim/AOT-annotated | `EntityMapper` initialisation, `Reflection.CreateInstance` in `DeserializeList`, anonymous-type handling (`BsonMapper.Deserialize.cs`) |
| Cascade reflection | `LiteDbCascadeExecutor` — `GetMethod("CollectionFor").MakeGenericMethod(step.ChildType)` (`LiteDbCascadeExecutor.cs:33-34`) |

### 3.4 Stellar

- MessagePack configured with `ContractlessStandardResolver.Instance` (`StellarRepository.cs:30-39`) — the contractless resolver is reflection-based.
- Custom resolvers/formatters (`RefResolver`, `WeakRefResolver`, `CryptoStringResolver`, `ValueTupleResolver`, …) are hand-written and mostly fine; the contractless fallback is the problem.
- `dynamic` in cascade dispatch: `StellarCascadeExecutor.RemoveBulkAsync(dynamic collection, …)` (`StellarCascadeExecutor.cs:136`).
- JSON round-trip via `JsonSerializer.Serialize/Deserialize<TItem>` without a context (`StellarRepository.Scoped.cs:73`).
- MessagePack-CSharp ships its own source generator (`[MessagePackObject]` + generated resolver) — the escape hatch, and why Stellar is the easiest provider.

### 3.5 Sqlite / DocumentDb (JSON)

| Blocker | Location |
| --- | --- |
| Open-generic JSON converters built by reflection | `RefJsonConverter`/`WeakRefJsonConverter` — `Activator.CreateInstance(typeof(RefJsonConverter<>).MakeGenericType(...))` (`Sqlite/Serialization/RefJsonConverter.cs:13-14`, `Sqlite/Serialization/WeakRefJsonConverter.cs:16-17`; identical in DocumentDb) |
| STJ reflection fallback resolver | `EntityJsonSerializer` options (Sqlite/DocumentDb `Serialization/`) |
| Reflection in predicates/patch/indexes | `SqliteRepository.ReadonlyRepository.cs:189`, `DocumentDbRepository.ReadonlyRepository.cs:271`, `DocumentDbRepository.Patch.cs:154,182`, `DocumentDbRepository.Increment.cs:57`, `DocumentDbRepository.Indexes.cs:48-63` |
| Query expression translation/compile | provider query layers (`Sqlite/Query/*`) |

### 3.6 Shared Saturn runtime (all providers)

- `ScopeModelHelper` — `GetProperty`, `Activator.CreateInstance(propertyType, scopeId)`, `SetValue` (`ScopeModelHelper.cs:57,80,94,127`).
- `CascadeEngine` — reflects a static `__Cascade` member and its `.For` property (`CascadeEngine.cs:99-101`).
- `CascadeRelationResolver` (Sqlite/DocumentDb) — `GetProperties`, `GetCustomAttribute`, `GetProperty("Scope")` (`Sqlite/Cascade/CascadeRelationResolver.cs:36-84`, `DocumentDb/Cascade/CascadeRelationResolver.cs:36-84`).
- `SyncHelper` — `GetProperties` (`SyncHelper.cs:19-21`).
- 147 reflection/dynamic hits across Abstractions, Entities, ChangeTracking, MongoDb, LiteDbX, Sqlite, DocumentDb (inventory command in §13).

---

## 4. What the serialization generator can and cannot fix

### 4.1 It fully removes (our code)

- Reflection-based entity graph IO for JSON (`RefJsonConverterFactory` → closed generated converters).
- **Per-type custom serializer registration for BSON** — the primary mechanism, supported by both vendors: `BsonSerializer.RegisterSerializer`/`[BsonSerializer]` (Mongo) and `BsonMapper.RegisterType<T>` (LiteDbX). A registered type bypasses `BsonClassMap.AutoMap`/`EntityMapper` for entity IO (§6.3).
- `FullTypeNameDiscriminatorConvention`'s `Type.GetType` — replaced by a generated discriminator registry.
- `BasicStructSerializer`'s runtime member enumeration — replaced by generated field writers/readers.
- Open-generic `Ref<>`/`WeakRef<>` serializer registration — replaced by closed generated serializers per referenced `T`.
- `ScopeModelHelper` reflection — replaced by generated scope accessors/predicate builders (the generator already knows which members carry `[AddRefToScope]`; `MemberToGenerate.IsScoped`/`RefType`).
- Cascade relation discovery — replaced by a generated relation table (same deliverable as `docs/cascade-deletion-proposal.md`).
- Per-type live dispatch (`MakeGenericMethod` for collections/materialisation/cascade) — replaced by generated dispatch tables or a generated `IEntityDispatch` interface.

### 4.2 It partially fixes, or cannot fix (vendor code)

- **MongoDB.Driver**: **entity IO and raw BSON queries are achievable under AOT — proven by the spike** — using per-type `IBsonSerializer<T>` (§4.1) plus a pre-registration harness for primitives and the dynamic-document serializer (`ExpandoObjectSerializer`); without that harness even `AutoMap` throws `MissingMethodException`. What is **not** achievable is the **LINQ query pipeline**: `Builders<T>.Filter.Where`, `AsQueryable()`, `.Project(expr)`, `.Sort(expr)` perform runtime `MakeGenericType` and `Expression.Lambda` partial evaluation. The driver carries no AOT/trim annotations (analyzers are silently green) and its only AOT effort, `CSHARP-3218`, was abandoned in 2020. → "IO + raw queries yes; LINQ no."
- **LiteDB/LiteDbX**: entity IO *is* solvable via per-type `BsonMapper.RegisterType<T>` (§4.1). What remains is **query/index translation**: `BsonMapper.GetExpression` → `LinqExpressionVisitor` resolves field names through `EntityMapper`/`MemberInfo` reflection (`BsonMapper.cs:186-205`). That is fixable **in our provider** — build raw `BsonExpression`/`Query` predicates from generated field names instead of LiteDB LINQ. The residual risk is the unannotated LiteDB core (mapper initialisation, `Reflection.CreateInstance`), which the spike must clear. → "Achievable overall, spike-gated."
- **Shiny.DocumentDb** (DocumentDb): the library is AOT-*capable*, but its AOT entry points are the optional `JsonTypeInfo<T>` parameters and `ConfigureDocument<T>` per-type registration, which must be supplied before the store is built. Our generic `TItem : Entity` repository cannot supply them for lazily discovered types without a generated per-type registration surface.
- **STJ itself**: `DefaultJsonTypeInfoResolver` and `JsonConverterFactory` reflection remain if we leave reflection fallback on.

### 4.3 The `object` shape: `Entity.Properties` (and why `Changes` is fine)

`Entity` has two `Dictionary<string, object>` members: `Changes` (`Entity.cs:212`) and `Properties` (`Entity.cs:216`). Their AOT exposure is very different.

- **`Changes` is never persisted — this is a hard invariant, not a serialization detail.** Today each provider drops it via its own hand-maintained mechanism: Mongo `UnmapProperty(f => f.Changes)` + `UnmapProperty(f => f.EnableChangeTracking)` (`MongoDbRepository.cs:444-445`), LiteDbX `.Ignore(e => e.Changes)` (`EntityMapper.cs:15-16`), Sqlite/DocumentDb string `IgnoredMembers` (`EntityJsonTypeInfoResolver.cs:7-12`), Stellar `EntityIgnoreFormatter.IgnoredProperties` (`EntityIgnoreFormatter.cs:7`). Those are five duplicated lists, and the **generated AOT serializers would have to honour them too** — so the invariant must come from one source of truth, not convention. At runtime `Changes` only receives boxed values in `SetField` (`Entity.cs:246-249`), which is AOT-safe. → not an AOT *shape* concern; an AOT *serializer* requirement (§6.7).
- **`Properties` is persisted**, so it is the real one:
  - **Sqlite / DocumentDb already solve it**: a hand-written, reflection-free `PropertiesJsonConverter : JsonConverter<Dictionary<string, object>>` covers `null`/`string`/`bool`/`int`/`long`/`double`/`decimal`/`DateTime`/`JsonElement` and falls back to `Convert.ToString` (`Sqlite/Serialization/PropertiesJsonConverter.cs:46-107`). That converter **is** the "closed allow-list converter" and is already AOT-clean.
  - **Mongo** maps it as `Dictionary<string, dynamic>` with `ObjectSerializer` (`MongoDbRepository.cs:447,390`) — dynamic + reflective serializer, AOT-hostile. Needs a closed BSON converter over the same scalar set (plus `BsonValue`/`BsonDocument`).
  - **Stellar** sends `Properties` through contractless MessagePack (`EntityIgnoreFormatter` ignores only `Changes`/`EnableChangeTracking`) — needs a generated formatter, or add `Properties` to the ignore list.
  - **LiteDbX ignores it** (`.Ignore(e => e.Properties)`), already safe.

So the shape is acceptable **provided each provider routes `Properties` through a closed converter** (JSON: done; Mongo/Stellar: to do). Values outside the allow-list degrade — JSON stringifies via `Convert.ToString`, BSON/MessagePack fail — so bound them or emit SATURN101. **No change to `GoLive.Saturn.Data.Entities` is required.** The optional "principled" alternative is a typed `PropertyBag` value (a closed scalar union), which is breaking and unnecessary.

Legacy options retained for reference: **(A)** exclude `Properties` from persistence in AOT mode; **(B)** the closed converter described above; **(C)** move to `Dictionary<string, JsonElement>` (breaking). Preference remains **(B)** where a converter is missing, which is now only Mongo and Stellar.

### 4.4 The entity model needs no AOT changes

`GoLive.Saturn.Data.Entities` is already reflection-free: the only `GetType()` calls are an equality check (`Ref.operators.cs:68`) and a message (`Ref.cs:75`); there is no `Activator`, `MakeGenericType`, `Expression`, `dynamic`, or member reflection anywhere in the assembly. `Ref<T>`/`WeakRef<T>`/`WeakRef`/`WrappedEntity<T>`/`ChangedEntity<T>` are open generics that need **closed serializers** (generator output), not type changes. `EntityIdGenerator`, `Timestamp`, `HashedString`, `EncryptedString` and the scoped bases are plain and AOT-safe. All AOT *shape* work therefore lives in the serializers/providers, not in this project. The one recommended entity change is an enforcement one: add `[JsonIgnore]` + a Saturn `[NotPersisted]` attribute to `Changes`/`EnableChangeTracking`/`_shortId` so the §6.7 invariant is structural rather than per-provider convention.

Two unrelated nits found while reviewing it (not AOT): `HashedStringJsonConverter` references `nameof(EncryptedString.Decoded)`/`nameof(EncryptedString.Populated)` by copy-paste (works only because the member names coincide), and because `Saturn.Data.Entities.JsonConverters` depends on `GoLive.Saturn.Data.Entities`, `[JsonConverter]` attributes cannot be placed on the entity types — registration must stay explicit/generated.

---

## 5. Feasibility matrix (expanded)

Legend: ✅ ours/straightforward · ⚠️ partial/vendor-dependent · ⛔ vendor blocker.

| Requirement | Stellar | Sqlite | DocumentDb | LiteDbX | MongoDb |
| --- | --- | --- | --- | --- | --- |
| Generated entity serializers (per type) | ✅ MessagePack source-gen | ✅ generated `JsonConverter<T>` | ✅ generated converters | ✅ `BsonMapper.RegisterType<T>` bypasses `EntityMapper` | ✅ `BsonSerializer.RegisterSerializer` + `IBsonDocumentSerializer` + registration harness |
| Remove runtime generic instantiation (ours) | ✅ | ✅ | ✅ | ✅ | ⚠️ ours yes; driver LINQ does it regardless |
| Remove discriminator `Type.GetType` | ✅ (MessagePack union) | ✅ generated registry | ✅ generated registry | ✅ generated `_type` binder | ✅ generated registry |
| Remove `dynamic` (ours) | ✅ | ✅ | ✅ | ✅ | ⚠️ `Properties`/`ObjectSerializer` (closed converter) |
| Query/index member resolution reflection | ✅ | ✅ (own translator) | ⚠️ library visitor | ⚠️ ours: replace LiteDB LINQ with raw `BsonExpression` | ⛔ driver LINQ (raw BSON queries only) |
| Engine/driver internals AOT-safe | ✅ | ✅ | ⚠️ needs per-type `JsonTypeInfo<T>` registration | ⚠️ LiteDB core not annotated (spike) | ⚠️ connect + IO work with serializer pre-registration; no annotations, analyzers silent |
| Verdict | **Achievable** | **Achievable** | **Achievable with constraints** | **Achievable for IO; full AOT gated on query rewrite + spike** | **Achievable for IO + raw BSON queries; LINQ must be disabled (spike-proven)** |

---

## 6. Proposed design

### 6.1 Packaging and opt-in

- **Extend `Saturn.Generator.Entities`** rather than ship a parallel analyzer: `ClassToGenerate` already carries `Members`, `Type`, `RefType`/`IsScoped`, DTO and view sets — almost all the input the serializers need. Add an `AotGeneration` pipeline (new files: `AotJsonGenerator.cs`, `AotBsonGenerator.cs`, `AotMetadataGenerator.cs`). One analyzer avoids version-skew and double symbol walking. Keep the package id, bump major.
- **Opt-in switch:** `<SaturnAot>true</SaturnAot>` (default `false`); `<SaturnAotProviders>Json;Bson</SaturnAotProviders>` selects adapters (default: infer from referenced provider packages). The analyzer stays packed to `analyzers/dotnet/cs` exactly as today (`Saturn.Generator.Entities.csproj:8-16,46-48`).

### 6.2 JSON/STJ emission (Sqlite, DocumentDb, Stellar-JSON)

**Reliable primary: per-entity generated `JsonConverter<T>` + a generated registry.**

```csharp
internal sealed class XJsonConverter : JsonConverter<X>
{
    public override X Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o) { /* typed member reads */ }
    public override void Write(Utf8JsonWriter w, X v, JsonSerializerOptions o) { /* typed member writes */ }
}

public static class SaturnGeneratedJsonConverters
{
    public static void Register(JsonSerializerOptions options)
    {
        options.Converters.Add(new XJsonConverter());
        options.Converters.Add(new RefJsonConverter<Customer>());   // closed only
    }
}
```

Why converters and not `[JsonSerializable]`:

- **Fully AOT-clean and deterministic**: statically typed member access; no `DefaultJsonTypeInfoResolver`, no `JsonConverterFactory` reflection, no `MakeGenericType`.
- Mirrors the proven BSON scalar approach in `Saturn.Data.MongoDb.EntitySerializers` (`RefSerializer<T>`, `WeakRefSerializer`, `HashedStringSerializer`, `EncryptedStringSerializer`, `TimestampSerializer`).
- Sidesteps a known Roslyn limitation (V1 below).
- **Omits transient members** (`Changes`, `EnableChangeTracking`, `_shortId`) by construction — see §6.7. The generated converter must never write them.

**Open generics (`Ref<T>`, `WeakRef<T>`, `WeakRef`):** the generator knows every referenced `X` from `MemberToGenerate.RefType`/`Type`. Emit a closed `RefJsonConverter<X>` and register it. Never emit a `JsonConverterFactory` (it instantiates closed converters via reflection — exactly what `RefJsonConverter.cs:13-14` does today).

**`Properties`:** route through a closed converter over the scalar allow-list — the existing `PropertiesJsonConverter` (`Sqlite/Serialization/PropertiesJsonConverter.cs:46-107`) is the reference implementation.
**Enums:** emit a closed `JsonStringEnumConverter<T>` registration (generic form is AOT-friendly); mirrors Mongo's `AlwaysSerializeEnumsConvention` for parity.
**Collections:** direct typed array writes; no polymorphic `object` serializer.

**Optional secondary: `[JsonSerializable]` partial context.** Emit `partial class SaturnJsonContext : JsonSerializerContext` with `[JsonSerializable(typeof(X))]` if we want STJ fast-path metadata. **Caveat (V1):** Roslyn source generators do not reliably see other generators' output, so the STJ generator may not process a context *we* emitted. Do not make the AOT story depend on it; treat the generated-converter registry as the contract.

### 6.3 BSON emission (MongoDb, LiteDbX)

**MongoDb** — per entity `X`, emit a closed `IBsonSerializer<X>` that also implements `IBsonDocumentSerializer` (so LINQ member resolution works without class maps — pattern at `RefSerializer.cs:70-93`):

```csharp
internal sealed class XBsonSerializer : SerializerBase<X>, IBsonDocumentSerializer
{
    public override void Serialize(BsonSerializationContext c, BsonSerializationArgs a, X v)
    {
        c.Writer.WriteStartDocument();
        c.Writer.WriteName("_id"); ObjectIdSerializer.Instance.Serialize(c, a, v.Id);
        c.Writer.WriteName("Name"); c.Writer.WriteString(v.Name);   // Ref<T> members: write the id scalar directly
        c.Writer.WriteEndDocument();
    }
    public override X Deserialize(...) { /* typed reads */ }
    public bool TryGetMemberSerializationInfo(string member, out BsonSerializationInfo info) { /* one entry per element */ }
}

public static class SaturnGeneratedBsonRegistry
{
    public static void Register()
    {
        BsonSerializer.TryRegisterSerializer(typeof(Customer), new CustomerBsonSerializer());
        BsonSerializer.TryRegisterSerializer(typeof(Ref<Customer>), new RefSerializer<Customer>());
    }
}
```

Key points:
- Register closed serializers for every entity and every referenced closed `Ref<X>`/`WeakRef<X>` — this replaces `RegisterGenericSerializerDefinition(typeof(Ref<>), …)` (`MongoDbRepository.cs:399-400`) and runtime `MakeGenericType`.
- Discriminators: replace `FullTypeNameDiscriminatorConvention.Type.GetType` with a generated `IReadOnlyDictionary<string, Type>` registry.
- `AutoMap`: if a generated serializer is registered for `X`, the driver uses it and `BsonClassMap.AutoMap()` is bypassed for `X`; verify (V2/V12) that hierarchies/`_id` do not still force a class map.
- **Transient members**: never emit `Changes`/`EnableChangeTracking`/`_shortId` in any BSON serializer (§6.7).
- **Mongo's residue**: per-type serializers alone are not enough — the driver still needs a generated pre-registration/rooting harness (primitives + `ExpandoObject`), and LINQ cannot run under AOT at all. See §7.5 and `docs/aot-mongodb-investigation.md`.

**LiteDbX** — no `IBsonSerializer<T>` concept, but `BsonMapper.RegisterType<T>` is a **full per-type override**, not scalar-only. The generator emits, per entity `X`:

```csharp
mapper.RegisterType<X>(
    x => SaturnBson.ForX(x),      // BsonDocument built with direct member access
    b => SaturnBson.ToX(b));      // typed reads, constructs X directly
```

Because our lambdas read/write the whole graph, no LiteDB reflection runs for entity IO. Remaining LiteDB reflection is confined to the **query layer** (`GetExpression`/`LinqExpressionVisitor`), which our provider bypasses with raw `BsonExpression`/`Query`. Verify (V4) that the LiteDbX fork exposes the non-generic `RegisterType(Type, Func<object,BsonValue>, Func<BsonValue,object>)` overload (upstream does, `BsonMapper.cs:121-126`) and that `LiteCollection<T>` never re-maps a registered type through `EntityMapper`.

### 6.4 Entity metadata emission (cascade, scope, collection names, dispatch)

```csharp
public static class SaturnEntityMetadata
{
    public static readonly IReadOnlyDictionary<Type, EntityMetadata> ByType = …;
}

public sealed record EntityMetadata(
    string CollectionName,
    IReadOnlyList<ScopeBinding> Scopes,        // member → (kind: string|Ref|WeakRef, generic arg)
    IReadOnlyList<CascadeRelation> Cascades,   // property type, scope/child-field, mode
    Func<object, string> GetId);
```

Replaces:
- `ScopeModelHelper` reflection (`ScopeModelHelper.cs:57-134`) with generated `SetScope`/`BuildScopePredicate` per type.
- `CascadeEngine`/`CascadeRelationResolver` reflection (`CascadeEngine.cs:99-101`, `Sqlite/DocumentDb/Cascade/CascadeRelationResolver.cs`).
- `MongoCascadeExecutor`/`LiteDbCascadeExecutor`/`DocumentDbRepository.Cascade` `MakeGenericMethod` dispatch (`MongoCascadeExecutor.cs:37-38`, `DocumentDbRepository.Cascade.cs:35-53`).
- `SyncHelper.GetProperties` (`SyncHelper.cs:19-21`).

### 6.5 Runtime opt-in API

- Provider options gain: `JsonSerializerContext? JsonSerializerContext` (DocumentDb already has one, `DocumentDbRepositoryOptions.cs:38`), `Action<JsonSerializerOptions>? ConfigureSerialization` (or `IReadOnlyList<JsonConverter>`), and `bool AotMode` (default `false`).
- In `AotMode`, providers (a) do not install reflection fallback resolvers, (b) fail fast on unregistered types, (c) call `SaturnGeneratedBsonRegistry.Register()` / `SaturnGeneratedJsonConverters.Register(options)` / `mapper.RegisterType<X>(…)`.
- Prefer explicit `AddSaturnAotSerialization()` over a generated `[ModuleInitializer]` (predictable ordering).

### 6.6 Diagnostics

| Id | Severity | Meaning |
| --- | --- | --- |
| SATURN100 | Error | `[SaturnAot]` on but member type `Y` has no generated serializer (e.g. `object`, unregistered interface, unconstrained generic) |
| SATURN101 | Warning | `Properties` used in AOT mode; only the scalar allow-list round-trips |
| SATURN102 | Info | Open generic `X<>` member encountered; closed serializers emitted for: `…` |
| SATURN103 | Warning | Provider/engine path is not AOT-capable in this configuration (Mongo `Builders<T>`/`Find(expr)`; LiteDbX query layer before the rewrite) |
| SATURN104 | Error | Polymorphic base-typed member without `[BsonKnownTypes]`/derived-type registry |
| SATURN105 | Error | A generated serializer would map a `[NotPersisted]` member (`Changes`, `EnableChangeTracking`, `_shortId`); these must never persist |

### 6.7 Transient-member enforcement (`Changes` must never persist)

`Entity.Changes`, `Entity.EnableChangeTracking` and `Entity._shortId` are runtime-only and must never appear in a persisted document. Today that holds only via five duplicated provider lists; once generated serializers exist, convention is not enough. Enforce from one source of truth:

1. In `GoLive.Saturn.Data.Entities`, mark the transient members with `[JsonIgnore]` (in-box, zero-dependency — covers the STJ path) **and** a Saturn `[NotPersisted]` attribute (the provider-neutral truth). This is the one small entity change worth making: it does not alter the shape, it makes the invariant structural.
2. The generator reads `[NotPersisted]` and **omits those members from every generated serializer/converter/formatter** (JSON, BSON, MessagePack, LiteDbX). This keeps the invariant true on the AOT path, where generated code replaces the provider conventions.
3. Emit **SATURN105 (error)** if `SaturnAot=true` and a generated serializer would otherwise map a `[NotPersisted]` member — fail the build, not the data.
4. Add a **cross-provider invariant test** asserting `Changes`/`EnableChangeTracking`/`_shortId` are absent from the persisted representation. Sqlite and DocumentDb already assert this (`Sqlite/.../ProviderSpecificTests.cs:26-27`, `DocumentDb/.../ProviderSpecificTests.cs:27-28`); **Mongo, LiteDbX and Stellar do not** and must be added.

---

## 7. Provider-specific plans

### 7.1 Stellar (easiest — proof first)
1. Emit `XFormatter : IMessagePackFormatter<X>` + a generated `SaturnMessagePackResolver : IFormatterResolver`; swap `ContractlessStandardResolver` (`StellarRepository.cs:37`).
2. Replace `dynamic` cascade dispatch (`StellarCascadeExecutor.cs:136`) with generated typed dispatch.
3. Give `StellarRepository.Scoped.cs:73`'s JSON round-trip a generated converter.
4. **Exit:** `PublishAot` console round-trips a scoped graph + refs with zero IL2026/IL3050 in Saturn assemblies.

### 7.2 Sqlite
1. Generated JSON converters + registry (§6.2); drop reflection fallback in `EntityJsonSerializer`.
2. Replace `RefJsonConverterFactory`/`WeakRefJsonConverterFactory` (`Sqlite/Serialization/RefJsonConverter.cs:13-14`).
3. Replace reflection in `SqliteRepository.ReadonlyRepository.cs:189` with generated metadata.
4. **Exit:** full Sqlite contract suite green under the `PublishAot` smoke harness.

### 7.3 DocumentDb
1. Same JSON converter registry as Sqlite.
2. Supply context/converters to `DocumentDbRepositoryOptions`; set `UseReflectionFallback=false` (`DocumentDbRepository.cs:29-30`, `DocumentDbRepositoryOptions.cs:38`).
3. **Constraint:** the library AOT path needs per-type `JsonTypeInfo<T>`/`ConfigureDocument<T>` before store construction. Emit `SaturnDocumentDbTypeRegistry.Register(storeOptions)` from the entity set.
4. Replace `MakeGenericMethod` in indexes (`DocumentDbRepository.Indexes.cs:48-63`) and `GetProperty` in patch/increment with generated metadata.
5. **Exit:** AOT smoke test with reflection fallback disabled; residual library constraints documented (extend `README.md:164`).

### 7.4 LiteDbX (achievable for IO; full AOT gated on a query rewrite + spike)
1. Generator emits `mapper.RegisterType<X>(serialize, deserialize)` per entity from closed `BsonDocument` writers/readers (§6.3), replacing the reflective `Ref<>` mapper in `EntityMapper.cs:19-55`.
2. Stop using LiteDB LINQ: replace `BsonMapper.Global.GetExpression(predicate)` (`LiteDbRepository.cs:133,169,411`) with `BsonExpression`/`Query` built from generated element names, so field resolution never touches `EntityMapper`/`MemberInfo`.
3. Replace `LiteDbCascadeExecutor`'s `MakeGenericMethod` (`LiteDbCascadeExecutor.cs:33-34`) with generated metadata dispatch (§6.4).
4. **Spike required:** LiteDB core is not trim/AOT-annotated; confirm `LiteDatabase`/`LiteCollection<T>`/`BsonExpression` run under `PublishAot` when the entity mapper is never invoked. If a core path reflects unconditionally, fall back to `GetCollection<BsonDocument>` with mapping entirely in our serializers.
5. **Exit:** LiteDbX contract suite green under `PublishAot`. If step 4 fails, downgrade to SATURN103 and document.

### 7.5 MongoDb (AOT mode is expression-free; spike already run — see companion doc)
The spike is **done** (`docs/aot-mongodb-investigation.md`). Results against driver 3.12.0 / .NET 10:
- Without a serializer harness: `BsonClassMap.AutoMap` and primitive serializer lookups throw `MissingMethodException` (`ObjectIdSerializer`, `StringSerializer`, `ExpandoObjectSerializer`).
- With all serializers pre-registered (`TryRegisterSerializer` for primitives + `System.Dynamic.ExpandoObject` + generated entity/ref serializers): **serialize, deserialize, raw `BsonDocument` filters, and `MongoClient` connect + query all work under AOT.**
- `Builders<T>.Filter.Where(...)` (and the rest of LINQ3) **fails**: `'ArraySerializer<Boolean>' is missing native code or metadata`, because LINQ3 instantiates generic serializers at runtime and `PartialEvaluator` calls `Expression.Lambda` (dynamic code).

Plan for a Mongo AOT mode:
1. Generated closed `IBsonSerializer<T>` (with `IBsonDocumentSerializer`) for every entity/view/DTO/`Ref<T>`/`WeakRef<T>` and the collections they use (§6.3).
2. Generated **registration + rooting harness**: `TryRegisterSerializer` for primitives, `ExpandoObject`, every closed generic serializer, and every entity; pin `BsonDefaults.DynamicDocumentSerializer`; use `DynamicDependency`/root for anything the driver constructs by name.
3. **Expression-free query surface**: `BsonDocument`/`FilterDefinition<T>`/`ProjectionDefinition<T>`/`SortDefinition<T>` built from generated element names. Consumers on the AOT path lose `repo.Many(o => o.X == …)`; provide a BSON predicate builder instead.
4. **Forbid LINQ by configuration**: throw at startup (SATURN103) if `Builders<T>.Where`/`AsQueryable`/`Project(expr)`/`Sort(expr)` is reached in AOT mode, so failures are loud, not silent.
5. Extend the smoke test with auth (SCRAM) and a non-trivial entity graph before claiming support; re-run the spike against the driver version at implementation time (the workaround depends on undocumented internals — see R1).

**Alternative route — Shiny.DocumentDb.MongoDb (recommended to evaluate first).** `Shiny.DocumentDb.MongoDb` 14.0.0 depends on `MongoDB.Driver` 3.10.0, yet Shiny claims full AOT/trimming for it (verified in-repo by `samples/Sample.Aot` with `PublishAot=true`). It can do so because it treats Mongo as a raw **BSON document store**: it renders filters itself (its `ToQueryString` "returns the rendered BSON filter"), uses a query IR that is "free of `Expression.Compile()`", and therefore never walks the driver's reflective class-map/LINQ3 paths — which is exactly the expression-free shape this section recommends. Since `GoLive.Saturn.Data.DocumentDb` already sits on Shiny.DocumentDb and advertises "every database Shiny.DocumentDb supports", an AOT MongoDB provider may be reachable by registering the Shiny Mongo backend **rather than** our direct-driver `Saturn.Data.MongoDb`. Trade-offs to weigh: JSON-document-in-BSON storage converted from STJ (not BSON-native mapping, so format/migration implications), Shiny's mandatory scalar `Id` model (a direct fit — our `Id` is already a single `string` normalized by `TryParseId`, and `_shortId` is only its derived hex projection via `GetIdAsHex`, not a second identifier), and our entity shapes (`Ref<T>`, crypto strings, `Properties`, discriminators) still needing generated `JsonTypeInfo`/converters. Verification item (V14): publish `GoLive.Saturn.Data.DocumentDb` with `Shiny.DocumentDb.MongoDb` under `PublishAot` against our entity graph, and confirm round-trip fidelity with existing documents.

---

## 8. Consumer experience (target)

```xml
<PropertyGroup>
  <PublishAot>true</PublishAot>
  <SaturnAot>true</SaturnAot>
</PropertyGroup>
<ItemGroup>
  <PackageReference Include="GoLive.Saturn.Generator.Entities" Version="8.0.0" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
  <PackageReference Include="GoLive.Saturn.Data.Sqlite" Version="8.0.0" />
</ItemGroup>
```

```csharp
var options = new SqliteRepositoryOptions { ConnectionString = "…" };
options.AotMode = true;   // no reflection fallback; fail fast on unregistered types
```

Generated artifacts (names illustrative): `XJsonConverter`, `SaturnGeneratedJsonConverters`, `XBsonSerializer`, `SaturnGeneratedBsonRegistry`, `SaturnEntityMetadata`, `SaturnDocumentDbTypeRegistry`.

---

## 9. Testing and CI

**AOT smoke harness (new):** `Saturn.Data.AotSmokeTest` — a console per provider with `<PublishAot>true</PublishAot>`, `<IsAotCompatible>true</IsAotCompatible>`, `<EnableTrimAnalyzer>true</EnableTrimAnalyzer>`, `<EnableAotAnalyzer>true</EnableAotAnalyzer>`, `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`. Runs a fixed round-trip (insert, read, scoped read, refs, crypto strings, DTO, patch) and exits non-zero on failure. The only honest gate for "AOT enabled" is **published, not just built**.

**Analyzer gate:** add `IsAotCompatible=true` to each *achievable* provider; the build then fails on new IL2026/IL3050. Start with Stellar/Sqlite/DocumentDb; leave MongoDb/LiteDbX unmarked until their spikes pass.

**Transient-member invariant (must fail loudly):** per provider, assert that `Changes`, `EnableChangeTracking` and `_shortId` never appear in the persisted form — including under the generated serializers. Sqlite (`Sqlite/.../ProviderSpecificTests.cs:26-27`) and DocumentDb (`DocumentDb/.../ProviderSpecificTests.cs:27-28`) already assert it; Mongo, LiteDbX and Stellar need the assertion added.

**Parity tests (critical):** assert generated serializer output is byte-for-byte identical to the current reflection mapper output (Mongo: `ToBson()`; JSON: canonical JSON). Catches drift against conventions (`IgnoreIfDefaultConvention`, `AlwaysSerializeEnumsConvention`, `_v`, `_p`, `_id` as ObjectId, `Unmap` of `Changes`/`EnableChangeTracking`). Generate goldens once, then pin them. This is the top correctness risk because the generated serializer hard-codes what conventions produce today (`MongoDbRepository.cs:377-435`).

---

## 10. Risks

| # | Risk | Impact | Mitigation |
| --- | --- | --- | --- |
| R1 | **MongoDB.Driver LINQ is intrinsically non-AOT** (`MakeGenericType` + `Expression.Lambda`), and the IO workaround relies on undocumented internals (`DynamicDocumentSerializer`, provider-chain ordering) that MongoDB has never committed to | AOT Mongo is an expression-free mode with long-term version risk | Generated serializers + registration harness; forbid LINQ under AOT (SATURN103); re-run the spike per driver version; see `docs/aot-mongodb-investigation.md` |
| R2 | **LiteDB core is not AOT-annotated** | IO fixable via `RegisterType<T>`; core paths may still reflect | Query-layer rewrite + spike; `GetCollection<BsonDocument>` bypass as fallback |
| R3 | Generated mapping drifts from provider conventions | Silent data corruption | Byte-for-byte golden parity tests |
| R4 | `Entity.Properties`/`dynamic`/`ObjectSerializer` unfixable in general | AOT gaps | Scalar allow-list converter (§4.3-B) + SATURN101 |
| R5 | STJ generator may not observe our emitted `JsonSerializerContext` | Metadata missing | Depend on generated `JsonConverter<T>`s, not the context; verify V1 |
| R6 | Open-generic `Ref<T>` in MessagePack/STJ | Missing serializers | Closed converters/formatters per referenced `T`; never factories |
| R7 | Polymorphism/discriminators | Runtime `Type.GetType` fallback | Generated discriminator registry + SATURN104 |
| R8 | The entity generator uses `AppDomain.CurrentDomain.GetAssemblies` (`Scanner.cs:597-610`) | Analyzer perf/correctness | Replace with `Compilation.GetTypeByMetadataName` only |
| R9 | Provider packages pull non-AOT dependencies transitively | Publish fails or warns | `IsAotCompatible` per project; suppress only vetted BCL warnings |
| R10 | Scope creep: "AOT" pulls in query translation, cascade, patch, change feed | Programme never lands | Scope AOT to serialization + metadata first; query layers are separate workstreams |
| R11 | Two generators version-skew if split | Confusing breakages | Extend `Saturn.Generator.Entities` (§6.1) |

---

## 11. Alternatives considered

| Alternative | Why not (as the primary) |
| --- | --- |
| Do nothing; document "not AOT" | Valid only for Mongo's vendor residue; leaves Stellar/Sqlite/DocumentDb/LiteDbX on the table |
| `IsTrimmable` + `[UnconditionalSuppressMessage]` everywhere | Hides the problem; trimming still strips reflected members at runtime |
| Ship STJ-context-only (no generated converters) | Depends on the STJ generator seeing our output (V1) and on `JsonConverterFactory` reflection for refs |
| Replace Mongo's data path with raw `BsonDocument` IO | Only viable if the spike shows the driver core works; likely driver-internal blockers remain |
| **Fork LiteDB to expose a serializer seam** | **Not needed** — `RegisterType<T>` already overrides entity IO; only the query layer needs changing, and that is ours |
| Register serializers by hand (no generator) | Works for one consumer but is per-app boilerplate; the generator is what makes it scale and keeps parity |
| Wait for vendor AOT support | No committed Mongo timeline; reasonable fallback, not a plan |

---

## 12. Phased plan

### Phase 0 — Gate and harness (small)
- Build `Saturn.Data.AotSmokeTest` for the most AOT-friendly provider (Stellar) with `PublishAot`; run the §7.4 LiteDbX core spike. (The Mongo spike is already done — see `docs/aot-mongodb-investigation.md`; extend it with auth + a full entity graph.)
- Baseline `IsAotCompatible` warning counts per project.
- **Exit:** spikes report go/no-go; smoke harness runs.

### Phase 1 — Generator AOT core (medium)
- Add `SaturnAot` option to `SaturnGenerator` (`:61-63` pattern) and a serialization output pipeline over `ClassToGenerate`/`MemberToGenerate`.
- Emit JSON converters + registry + `PropertyBagJsonConverter`; emit SATURN100/101/102/104.
- **Exit:** generated converters compile and pass golden parity.

### Phase 2 — Sqlite AOT (medium)
- Wire registry; remove reflection fallback and converter factories.
- **Exit:** Sqlite contract suite green under `PublishAot`.

### Phase 3 — Stellar AOT (medium)
- MessagePack generated formatters/resolver; remove `dynamic` + contractless resolver.
- **Exit:** Stellar contract suite green under `PublishAot`.

### Phase 4 — DocumentDb AOT (medium–high)
- Generated converters + `SaturnDocumentDbTypeRegistry`; disable reflection fallback; replace index/patch/increment reflection.
- **Exit:** DocumentDb tests green under `PublishAot` with `UseReflectionFallback=false`.

### Phase 5 — BSON emission + metadata + LiteDbX rewrite (high)
- Emit `SaturnEntityMetadata`; migrate `ScopeModelHelper`, cascade, dispatch.
- Emit `IBsonSerializer<T>` + `SaturnGeneratedBsonRegistry` + discriminator registry for Mongo; emit `RegisterType<T>` for LiteDbX; rewrite the LiteDbX query layer off LiteDB LINQ.
- **Exit:** generated-serializer byte-parity passes; LiteDbX suite passes *without* AOT; LiteDbX AOT publication contingent on its spike; Mongo ships an expression-free AOT mode gated on the extended spike (auth + full graph).

### Phase 6 — Decision and documentation (small)
- Encode per-provider verdicts in README/diagnostics; publish both spike results; update `README.md:160-171`.
- **Exit:** docs state exactly which providers are AOT-enabled (Stellar/Sqlite/DocumentDb), IO-only (LiteDbX), or IO + raw BSON with LINQ disabled (MongoDb), with reasons.

---

## 13. Verification checklist before implementation

1. **V1:** Does the STJ incremental generator process a `JsonSerializerContext` emitted by another source generator in the same compilation? (Decides context vs converters-only.)
2. **V2:** With a generated `IBsonSerializer<T>` registered, does MongoDB.Driver bypass `BsonClassMap.AutoMap()` for `T` entirely (incl. Id/discriminator)?
3. **V3 (Mongo spike) — DONE.** See `docs/aot-mongodb-investigation.md`: entity IO + raw BSON queries + `MongoClient` connect/query work under AOT once serializers are pre-registered; LINQ3 fails. Still to verify: auth (SCRAM), TLS, sessions/transactions, aggregations/`Watch`, and the breadth of generic instantiations.
4. **V4 (LiteDbX):** Confirm the fork's `BsonMapper.RegisterType(Type, Func<object,BsonValue>, Func<BsonValue,object>)` exists, that a registered entity is *never* routed through `EntityMapper` by `LiteCollection<T>` for IO, and that the query layer can be driven by raw `BsonExpression` without `GetExpression`.
5. **V5:** Does MessagePack-CSharp's source generator handle Saturn's shapes (ObjectId-as-string, `Ref<T>`, `Properties`), and can `FastDBOptions.MessagePackOptions` take a generated `IFormatterResolver`?
6. **V6:** Does Shiny.DocumentDb require `JsonTypeInfo<T>` per operation for true AOT, or does a combined generated `IJsonTypeInfoResolver` suffice?
7. **V7:** Which `Expression.Compile()` calls execute in the JSON/Stellar query paths at runtime? Convert to `preferInterpretation:true` or generated delegates.
8. **V8:** Exact `IsAotCompatible` warning counts per project today (baseline).
9. **V9:** Does `BsonSerializer.RegisterGenericSerializerDefinition` appear in the AOT warning set, and can closed registration fully replace it?
10. **V10:** Confirm `Entity.Properties` value shapes actually used by consumers (decides §4.3-A vs -B).
11. **V11 (Mongo):** Which driver runtime paths execute at startup for `MongoClient` + `IMongoCollection<BsonDocument>` (serializer registry, class-map lookup, expression compile)? Enumerate to size the risk.
12. **V12 (Mongo):** Does `BsonSerializer.RegisterSerializer` + `IBsonDocumentSerializer` fully satisfy the LINQ translator's member resolution, or does it still author class maps for hierarchies/discriminators? *(Note: V3 shows LINQ3 fails under AOT regardless, so this is now only relevant for the non-AOT path.)*
13. **V13 (Mongo):** Enumerate the closed generic serializer instantiations a full Saturn entity graph needs (enums, `List<>`, `Dictionary<>`, nullable, arrays, `Ref<T>`/`WeakRef<T>`) and confirm the generated registration harness roots them all.

---

## 14. Recommendation

Proceed, but **reframe the goal**. "Provide a serialization generator so everything can be AOT" is the right *mechanism* and the wrong *promise*. The mechanism is real and reusable: **extend `Saturn.Generator.Entities` to emit per-type serializers plus entity metadata**, because Saturn owns the entity shapes and the mapping conventions, and the existing scalar serializers and DTO/view/tracking emissions prove the generator can carry this weight.

To answer the direct question squarely: **yes, register a custom serializer per type for both LiteDbX and MongoDB** — that is exactly the generator's core output, and it removes the entity-IO reflection in both. The dividing line is what per-type serializers *don't* cover:

- **Stellar, Sqlite, DocumentDb:** AOT-enabled, gated by a published smoke harness and golden byte-parity tests. Start here; Stellar is the proof.
- **LiteDbX:** AOT-enabled for IO via generated `BsonMapper.RegisterType<T>` serializers, plus a query-layer rewrite that stops using LiteDB LINQ. Gated on the §7.4 spike clearing the unannotated LiteDB core.
- **MongoDb:** AOT-enabled **only in an expression-free mode** — generated per-type `IBsonSerializer<T>` plus a pre-registration/rooting harness make entity IO and raw BSON queries work under AOT (spike-proven), but LINQ (`Builders<T>.Where`, `AsQueryable`, `Project(expr)`, `Sort(expr)`) must be disabled, with SATURN103 on any LINQ path.

Do Phase 0 first: the two spikes and the STJ-generator-visibility check (V1) are cheap and they decide whether the broader design holds. Everything after is additive and testable through the existing generator harness and shared contract suites.
