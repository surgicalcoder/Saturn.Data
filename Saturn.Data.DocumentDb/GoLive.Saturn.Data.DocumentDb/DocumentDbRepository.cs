using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;
using Shiny.DocumentDb;

namespace Saturn.Data.DocumentDb;

public partial class DocumentDbRepository : IDisposable
{
    private readonly RepositoryOptions options;
    private readonly DocumentDbRepositoryOptions documentDbOptions;
    private readonly IDocumentStore store;
    private readonly IDatabaseProvider databaseProvider;
    private readonly bool ownsStore;
    private readonly SemaphoreSlim initializationLock = new(1, 1);

    private DocumentDbCapabilities capabilities;
    private bool initialized;
    private bool disposed;

    public DocumentDbRepository(RepositoryOptions options, DocumentDbRepositoryOptions documentDbOptions)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.documentDbOptions = documentDbOptions ?? throw new ArgumentNullException(nameof(documentDbOptions));

        if (documentDbOptions.Store is not null)
        {
            store = documentDbOptions.Store;
            ownsStore = false;
        }
        else if (documentDbOptions.DatabaseProvider is not null)
        {
            var storeOptions = new DocumentStoreOptions
            {
                DatabaseProvider = documentDbOptions.DatabaseProvider,
                TableName = documentDbOptions.DefaultTableName
            };

            documentDbOptions.ConfigureStore?.Invoke(storeOptions);

            if (!documentDbOptions.UseReflectionFallback)
            {
                storeOptions.UseReflectionFallback = false;
            }

            databaseProvider = storeOptions.DatabaseProvider;
            store = new DocumentStore(storeOptions);
            ownsStore = true;
        }
        else
        {
            throw new InvalidOperationException("DocumentDbRepositoryOptions requires either Store or DatabaseProvider.");
        }
    }

    internal RepositoryOptions Options => options;

    internal DocumentDbRepositoryOptions DocumentDbOptions => documentDbOptions;

    internal IDocumentStore Store => store;

    internal DocumentDbCapabilities Capabilities => capabilities;

    protected string GetCollectionNameForType<TItem>() where TItem : Entity => options.GetCollectionName(typeof(TItem));

    internal static bool SupportsSoftDelete<TItem>() where TItem : Entity => typeof(ISoftDeletable).IsAssignableFrom(typeof(TItem));

    internal static bool SupportsArchivable<TItem>() where TItem : Entity => typeof(IArchivable).IsAssignableFrom(typeof(TItem));

    protected static string NormalizeId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        return Entity.TryParseId(id, out var normalized) && normalized is not null ? normalized : null;
    }

    protected static List<string> NormalizeEntityIds(IEnumerable<string> ids)
    {
        var result = new List<string>();

        if (ids is null)
        {
            return result;
        }

        foreach (var id in ids)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            if (Entity.TryParseId(id, out var normalized) && normalized is not null && !result.Contains(normalized))
            {
                result.Add(normalized);
            }
        }

        return result;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (initialized)
        {
            return;
        }

        await initializationLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (initialized)
            {
                return;
            }

            var backendName = documentDbOptions.BackendName
                              ?? databaseProvider?.GetType().Name
                              ?? store.GetType().Name;

            capabilities = DocumentDbCapabilities.Probe(store, databaseProvider, backendName);
            initialized = true;
        }
        finally
        {
            initializationLock.Release();
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        initializationLock.Dispose();

        if (ownsStore && store is IDisposable disposable)
        {
            disposable.Dispose();
        }

        GC.SuppressFinalize(this);
    }
}
