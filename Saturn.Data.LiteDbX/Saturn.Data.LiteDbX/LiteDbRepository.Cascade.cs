using GoLive.Saturn.Data.Entities;
using LiteDbX;

namespace Saturn.Data.LiteDbX;

public partial class LiteDbRepository
{
    internal ILiteCollection<T> CollectionFor<T>() where T : Entity
        => database.GetCollection<T>(GetCollectionNameForType<T>());

    internal static bool IsSoftDeletable<T>() where T : Entity
        => typeof(ISoftDeletable).IsAssignableFrom(typeof(T));

    internal static bool IsArchivable<T>() where T : Entity
        => typeof(IArchivable).IsAssignableFrom(typeof(T));
}
