# MongoDB AOT Investigation — Empirical Findings

**Status:** Investigation complete (spike run)
**Author:** Axiom
**Date:** 2026-09-30
**Scope:** Determine, by running it, whether the official MongoDB .NET/C# driver can work under NativeAOT when Saturn supplies per-type serializers.
**Companion:** `docs/aot-serialization-generator-proposal.md` (this document is the evidence record; the proposal is the plan).

---

## 1. Verdict up front

The earlier proposal's Mongo verdict was *"entity IO fixable, full AOT unproven — spike only."* The spike now says, precisely:

- **Entity IO under NativeAOT: YES**, with a generated closed `IBsonSerializer<T>` per type **plus** a pre-registration/rooting harness for primitives and the dynamic-document serializer.
- **Mongo LINQ under NativeAOT: NO.** `Builders<T>.Filter.Where`, `AsQueryable().Where`, `.Project(expr)`, `.Sort(expr)` do runtime generic instantiation and client-side `Expression.Lambda`, which cannot be pre-registered in general.
- **Client connect + raw BSON query under NativeAOT: YES** (observed a real round-trip against localhost:27017).
- **Cost:** a Saturn "Mongo AOT mode" must be **expression-free** (raw `BsonDocument`/`FilterDefinition`/`ProjectionDefinition`/`SortDefinition`) and must ship a serializer-registration harness. That is a bounded but real API change, not a drop-in.

So: **per-type serializers are necessary and they work — they get Mongo entity IO and raw queries under AOT — but they do not rescue the LINQ query API, which is the bulk of Saturn's Mongo read path.** The proposal's verdict is upgraded from "unproven" to "achievable for IO + raw queries; LINQ must be disabled in AOT mode".

---

## 2. Environment and method

| Item | Value |
| --- | --- |
| Driver | `MongoDB.Driver` / `MongoDB.Bson` **3.12.0** (matches `Saturn.Data.MongoDb.csproj:31-32`) |
| SDK | .NET **10.0.303** |
| OS | Windows (win-x64) |
| Publish | `dotnet publish -c Release -r win-x64 -p:PublishAot=true -p:StripSymbols=true -p:TrimmerSingleWarn=false` |
| Server | local MongoDB on `localhost:27017` (present) |

Probe app: a console that (a) registers serializers, (b) serializes/deserializes a `Person` with a hand-written `IBsonSerializer<Person>` implementing `IBsonDocumentSerializer`, (c) renders filters, (d) connects and runs a query. Run both JIT (baseline) and NativeAOT.

Two independent techniques were used:
1. **ILC output analysis** at publish (`TrimmerSingleWarn=false` to break the per-assembly summaries into individual warnings).
2. **Runtime execution** of the published native binary, with each scenario in its own `try/catch` so one failure does not mask the rest.

---

## 3. Finding 1 — the driver ships zero AOT/trim annotations

Scanning the `net6.0` assemblies that net10.0 actually consumes:

| Symbol | `MongoDB.Bson.dll` | `MongoDB.Driver.dll` |
| --- | --- | --- |
| `RequiresUnreferencedCode` | 0 | 0 |
| `RequiresDynamicCode` | 0 | 0 |
| `RequiresAssemblyFiles` | 0 | 0 |
| `IsTrimmable` | 0 | 0 |

Consequence: a consumer library with `<IsAotCompatible>true</IsAotCompatible>` and `<EnableAotAnalyzer>true</EnableAotAnalyzer>` gets **0 warnings** for arbitrary MongoDB usage. Verified: the probe builds with **0 warnings** while calling `BsonClassMap.AutoMap`, `Builders<T>.Filter.Where`, `AsQueryable().Where`, `.Project`, `.Sort`, `BsonSerializer.Serialize`. The analyzer was proven live in the same project (an unrelated `Type.GetType(string)` produced `IL2057`).

**This is a false negative, and it matters:** a team could enable AOT "cleanly" (no warnings) and ship a binary that throws `MissingMethodException` on the first query. The absence of annotations is itself the finding.

---

## 4. Finding 2 — ILC sees the hazards the analyzers miss

At publish, ILC emitted (individual warnings, excerpted):

- `IL3053` / `IL2104`: *"Assembly 'MongoDB.Driver' produced AOT/trim analysis warnings"* and the same for `MongoDB.Bson`.
- **Reflective serializer construction**
  - `MongoDB.Bson.Serialization.BsonSerializationProviderBase.CreateSerializer(Type,IBsonSerializerRegistry)`
    - `System.Type.GetConstructor(...)` — needs `PublicConstructors` / `PublicParameterlessConstructor`
    - `System.Type.MakeGenericType(Type[])` — `RequiresDynamicCode`
  - `BsonSerializationProviderBase.CreateGenericSerializer(...)` — `MakeGenericType`
  - `BsonClassMap.LookupClassMap(Type)` — `MakeGenericType`
  - `CollectionsSerializationProvider.GetCollectionSerializer/GetReadOnlyDictionarySerializer/GetImplementedInterfaces` — `MakeGenericType`, `GetInterfaces`
  - `Serializers.EnumSerializer.Create`, `NullableSerializer.Create`, `DowncastingSerializer.Create`, `IOrderedEnumerableSerializer.Create` — `MakeGenericType`
- **LINQ3 pipeline (the important ones)**
  - `MongoDB.Driver.Linq.Linq3Implementation.Misc.PartialEvaluator.SubtreeEvaluator.Evaluate(Expression)` — `System.Linq.Expressions.Expression.Lambda(...)` with `RequiresDynamicCode` (*"Delegate creation requires dynamic code generation"*). This is client-side partial evaluation, i.e. it **compiles a lambda**.
  - `...Misc.TypeExtensions.TryGetGenericInterface/ImplementsInterface` — `Type.GetInterfaces()`
  - `...Translate.ClrCompatExpressionRewriter`, `InjectMethodToFilterTranslator`, `ContainsMethodToFilterTranslator`, `ListMethod.IsExistsMethod`, `ToArray/Average/NewList/NewHashSet…ExpressionToAggregationExpressionTranslator` — `MakeGenericType` / `MethodInfo.MakeGenericMethod`
  - `...Serializers.{UnknowableSerializer, IgnoreNodeSerializer, ConvertEnumToIntegralTypeSerializer, ICollectionSerializer, IEnumerableSerializer, DictionaryKey/ValueCollectionSerializer}` — `MakeGenericType`
- `MongoDB.Driver.FieldValueSerializerHelper.ConvertIfPossibleSerializer.TryConvertValue` — `System.ComponentModel.TypeDescriptor.GetConverter(Type)` (`RequiresUnreferencedCode`).

Interpretation: the driver resolves serializers by reflection and the LINQ provider synthesizes generic serializers at runtime. Neither is annotated, so none of this surfaces at build time.

---

## 5. Finding 3 — runtime results, by scenario

Each row is a separate probe run. `OK`/`FAIL` is the observed runtime outcome of the native binary.

### v1 — default usage (no custom serializer), `BsonClassMap.AutoMap` + serialize

```
Unhandled exception. System.MissingMethodException: No suitable constructor found for serializer type:
'MongoDB.Bson.Serialization.Serializers.ObjectIdSerializer'.
   at MongoDB.Bson.Serialization.BsonSerializationProviderBase.CreateSerializer(Type, IBsonSerializerRegistry)
   at MongoDB.Bson.Serialization.Conventions.StringObjectIdIdGeneratorConvention.PostProcess(BsonClassMap)
   at MongoDB.Bson.Serialization.BsonClassMap.AutoMapClass()
```

Even the most basic class-map path dies: the convention runner asks the reflective provider for a serializer whose constructor was trimmed.

### v2 — custom `IBsonSerializer<Person>` only, no primitive pre-registration

| Scenario | Result |
| --- | --- |
| Serialize `Person` with custom serializer | **OK** (correct BSON produced) |
| Deserialize `Person` | FAIL — `MissingMethodException: ExpandoObjectSerializer` |
| `BsonDocument` filter render | FAIL — `MissingMethodException: StringSerializer` |
| LINQ `Where` filter render | FAIL — `TypeInitializationException` |
| `BsonClassMap.AutoMap` | OK (after serializer warmed the registry) |
| `MongoClient` connect | FAIL — `TypeInitializationException` |

### v3 — + pre-register primitives (`string`, `ObjectId`, `int`, `long`, `bool`, `double`)

| Scenario | Result |
| --- | --- |
| Serialize `Person` | **OK** |
| Deserialize `Person` | FAIL — `ExpandoObjectSerializer` |
| `BsonDocument` filter render | **OK** |
| LINQ `Where` filter render | FAIL — `NotSupportedException: 'ArraySerializer<Boolean>' is missing native code or metadata` |
| Raw `BsonDocument` filter for typed `Find` | **OK** |
| `MongoClient` connect | FAIL — connection handshake reached `ReplyMessageBinaryEncoder.ReadMessage` → `BsonDeserializationContext.Builder` → `BsonDefaults.DynamicDocumentSerializer` → `ExpandoObjectSerializer` → `MissingMethodException` |

### v4 — + pre-register `System.Dynamic.ExpandoObject` → `ExpandoObjectSerializer`

| Scenario | Result |
| --- | --- |
| Serialize `Person` (custom serializer) | **OK** |
| Deserialize `Person` (custom serializer) | **OK** |
| `BsonDocument` filter render | **OK** |
| **LINQ `Where` filter render (typed)** | **FAIL** — `'ArraySerializer<Boolean>' is missing native code or metadata` |
| Raw `BsonDocument` filter for typed `Find` (no LINQ) | **OK** |
| `MongoClient` connect + query against `localhost:27017` | **OK** |

JIT baseline (non-AOT) for the same code still works, including LINQ translation (`{ "Name" : "x", "Age" : { "$gt" : 3 } }`), so v4's LINQ failure is AOT-specific, not a code error.

---

## 6. Failure taxonomy

| Class | Mechanism | Triggered by | Fixable? |
| --- | --- | --- | --- |
| **A. Reflective serializer construction** | `BsonSerializationProviderBase.CreateSerializer` → `Type.GetConstructor` + `Activator` | primitive serializers (`StringSerializer`, `ObjectIdSerializer`, …); dynamic-document serializer (`ExpandoObjectSerializer`) | **Yes** — pre-register every serializer (`BsonSerializer.TryRegisterSerializer`) or root them, incl. primitives + `ExpandoObject` |
| **B. Runtime generic instantiation in LINQ3** | `MakeGenericType` / `MakeGenericMethod` synthesizing serializers/translators (`ArraySerializer<bool>`, enum/collection/dictionary serializers) | any `Builders<T>.Filter.Where`, `AsQueryable()`, `.Project(expr)`, `.Sort(expr)`, aggregation/`Watch` | **No (practically)** — the set of instantiations is unbounded; rooting them all is not maintainable |
| **C. Dynamic code generation in client-side evaluation** | `Expression.Lambda(...)` in `PartialEvaluator.SubtreeEvaluator` (`RequiresDynamicCode`) | LINQ queries with closed-over/local subexpressions | **No** — delegate creation needs dynamic code |
| **D. Untrim-safe conversions** | `TypeDescriptor.GetConverter(Type)` (`RequiresUnreferencedCode`) | some value conversions/mappings | Partial; avoid the conversion path |

Class **A** is a registration/rooting problem and is solvable with a generated harness. Classes **B** and **C** are intrinsic to the LINQ3 provider and are the reason `Builders<T>` / `IMongoQueryable` cannot be an AOT story.

---

## 7. What this means for Saturn's Mongo provider

Saturn's Mongo read path is built on exactly the APIs that fail:

- `Builders<TItem>.Filter.Where(predicate)` — `MongoDbRepository.ReadonlyRepository.cs:28,58,88,136,292,316,341`
- `GetCollection<TItem>().AsQueryable().Where(...)` — `MongoDbRepository.ReadonlyRepository.cs:179`, `SecondScopedReadonly.cs:182`, `WeakSecondScopedReadonly.cs:197`
- `.Project(selector)` / `.Sort(...)` — `ReadonlyRepository.cs:296,320,345,374,421`
- `.Aggregate(...)` / `.Watch(...)` — `ReadonlyRepository.cs:450`, watch sink
- `RefExpressionRewriter` already rewrites expression trees (`MakeGenericMethod`) — `RefExpressionRewriter.cs:131,178`

To make Mongo AOT-viable, the provider needs a **parallel, expression-free query mode**:

1. **Generated serializers:** a closed `IBsonSerializer<T>` (with `IBsonDocumentSerializer`) for every entity, view, DTO, `Ref<T>`, `WeakRef<T>`, and the collections they contain — the same deliverable as the main proposal §6.3.
2. **Generated registration harness:** emit `BsonSerializer.TryRegisterSerializer(...)` for primitives, `ExpandoObject`, every closed generic serializer used, and every entity; plus `DynamicDependency`/`TrimmerRootAssembly` for anything the driver constructs by name. This is what turns Class A failures into passes.
3. **Expression-free queries:** a Mongo AOT API surface that takes `BsonDocument`/`FilterDefinition<T>`/`ProjectionDefinition<T>`/`SortDefinition<T>` built from generated element names — no `Expression<Func<…>>`. Consumers on the AOT path lose `repo.Many(o => o.X == …)` and use a BSON predicate builder instead.
4. **Pin the dynamic-document serializer** (`BsonDefaults.DynamicDocumentSerializer`) so the connection handshake never performs a reflective lookup.
5. **Forbid LINQ by configuration:** throw at startup (or SATURN103) if `Builders<T>.Where`/`AsQueryable` is reached in AOT mode, so failures are loud, not silent.

That is a real, bounded engineering effort — but it is a *different query API*, not a serialization toggle.

---

## 8. Residual risks / not yet verified

The spike proves a happy path (anonymous-free local connection, one typed read). It does **not** yet cover:

- **Authentication** (SCRAM-SHA-256), **TLS**, replica sets / sharded topologies, server selection.
- **Sessions/transactions** (`ClientSession`), retryable writes.
- **Change streams** (`Watch`) and **aggregations** — both are LINQ-adjacent and expected to hit Class B.
- **Projections**: server-side `$project` via `ProjectionDefinition` (raw BSON) should behave like raw filters, but the LINQ `Project(expr)` path does not.
- **Breadth of generic instantiations:** v4 rooted only the primitives + `ExpandoObject`; a full entity graph (enums, `List<>`, `Dictionary<>`, nullable, arrays, `Ref<>`) will surface more Class-A instantiations to register. This is mechanical but must be generated and tested.
- **`System.ComponentModel.TypeDescriptor` conversion path** (Class D) for exotic member types.
- **Packages `MongoDB.Driver.Core.Extensions.DiagnosticSources` / `.OpenTelemetry`** (referenced by `Saturn.Data.MongoDb.csproj:33-34`) — not exercised.
- **Longevity:** the workaround depends on internal behaviours (`DynamicDocumentSerializer`, provider-chain ordering) that MongoDB has never committed to for AOT and could change between minor versions. This is the single biggest strategic risk.

---

## 9. Recommendation

- **Keep the main proposal's framing**, but change the Mongo line from "partial/unproven" to: **entity IO + raw-BSON queries are achievable under AOT in a dedicated, expression-free mode; the LINQ query API must be disabled on that path.** Evidence: v4.
- **Do not** advertise "Mongo AOT" as a general capability. Ship it, if at all, as `SaturnAot=Mongo` with (a) generated serializers, (b) a generated registration/rooting harness, (c) a BSON-predicate query surface, (d) hard failure on any LINQ API under AOT, and (e) a published smoke test that includes auth and a non-trivial entity graph.
- **Add SATURN103 semantics:** referencing `GoLive.Saturn.Data.MongoDb` with `SaturnAot=true` should produce a warning stating that LINQ APIs are unavailable and only the BSON-predicate API is supported.
- **Re-run this spike against the driver version used at implementation time** before committing to the work. The failure classes above are version-sensitive.

---

## 10. Reproduction

```powershell
# from a scratch dir
dotnet new console -n mongo-aot-probe
# probe.csproj: net10.0, <IsAotCompatible>true</IsAotCompatible>, <EnableAotAnalyzer>true</EnableAotAnalyzer>,
#               <EnableTrimAnalyzer>true</EnableTrimAnalyzer>, <TrimmerSingleWarn>false</TrimmerSingleWarn>,
#               PackageReference MongoDB.Driver 3.12.0
# Program.cs: register primitives + ExpandoObject + a hand-written IBsonSerializer<Person>;
#             then serialize/deserialize, render a BsonDocument filter and a LINQ filter, and connect.

dotnet build   -c Release -r win-x64                       # note: 0 analyzer warnings (Finding 1)
dotnet publish -c Release -r win-x64 -p:PublishAot=true `
               -p:StripSymbols=true -p:TrimmerSingleWarn=false
# run the produced probe.exe and read the OK/FAIL lines (Finding 3)
```

Key probe lines that flip the result:

```csharp
BsonSerializer.TryRegisterSerializer(typeof(string), new StringSerializer());
BsonSerializer.TryRegisterSerializer(typeof(ObjectId), new ObjectIdSerializer());
BsonSerializer.TryRegisterSerializer(typeof(int), new Int32Serializer());
BsonSerializer.TryRegisterSerializer(typeof(long), new Int64Serializer());
BsonSerializer.TryRegisterSerializer(typeof(bool), new BooleanSerializer());
BsonSerializer.TryRegisterSerializer(typeof(double), new DoubleSerializer());
BsonSerializer.TryRegisterSerializer(typeof(System.Dynamic.ExpandoObject), new ExpandoObjectSerializer());
BsonSerializer.TryRegisterSerializer(typeof(Person), new PersonSerializer());
```
