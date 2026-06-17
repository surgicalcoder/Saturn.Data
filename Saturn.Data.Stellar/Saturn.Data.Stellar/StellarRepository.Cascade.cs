using System.Linq;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Entities;
using Stellar.Collections;

namespace Saturn.Data.Stellar;

public partial class StellarRepository
{
    internal async Task<IQueryable<Entity>> QueryAsync<T>() where T : Entity
    {
        var collection = await database.GetCollectionAsync<EntityId, T>(collectionName: GetCollectionNameForType<T>());
        return collection.AsQueryable().Cast<Entity>();
    }
}
