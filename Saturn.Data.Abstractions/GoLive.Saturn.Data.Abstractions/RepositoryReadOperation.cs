namespace GoLive.Saturn.Data.Abstractions;

public enum RepositoryReadOperation
{
    All,
    ById,
    ByIds,
    Count,
    ExistsById,
    ExistsByPredicate,
    Queryable,
    Many,
    One,
    Random
}

