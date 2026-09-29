namespace Saturn.Data.DocumentDb.ChangeFeed;

internal sealed class ChangeFeedCounterRow
{
    public string Id { get; set; } = "";

    public long Seq { get; set; }
}

internal sealed class ChangeFeedOutboxRow
{
    public string Id { get; set; } = "";

    public long Seq { get; set; }

    public string Source { get; set; } = "";

    public string PayloadJson { get; set; } = "";
}
