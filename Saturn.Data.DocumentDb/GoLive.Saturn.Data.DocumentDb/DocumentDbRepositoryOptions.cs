using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Saturn.Data.DocumentDb.Serialization;
using Shiny.DocumentDb;

namespace Saturn.Data.DocumentDb;

public enum UnsupportedPredicateBehaviour
{
    Throw,
    FallbackToClient
}

public enum ChangeFeedMode
{
    Outbox,
    Native
}

public sealed class DocumentDbRepositoryOptions
{
    public IDocumentStore Store { get; set; }

    public IDatabaseProvider DatabaseProvider { get; set; }

    public Action<DocumentStoreOptions> ConfigureStore { get; set; }

    public string BackendName { get; set; }

    public string DefaultTableName { get; set; } = "documents";

    public bool HonorCollectionNames { get; set; }

    public UnsupportedPredicateBehaviour UnsupportedPredicateBehaviour { get; set; } = UnsupportedPredicateBehaviour.Throw;

    public ChangeFeedMode ChangeFeedMode { get; set; } = ChangeFeedMode.Outbox;

    public JsonSerializerContext JsonSerializerContext { get; set; }

    public IJsonTypeInfoResolver AdditionalTypeInfoResolver { get; set; }

    public bool UseReflectionFallback { get; set; } = true;

    public Action<string> OnUnsupportedIndexOption { get; set; }

    public Action<string> OnClientSideFallback { get; set; }

    public EntityJsonSerializerOptions Serializer { get; set; } = new();
}
