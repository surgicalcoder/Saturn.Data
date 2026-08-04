namespace GoLive.Saturn.Data.Abstractions.ChangeFeed;

public sealed class ChangeFeedCounter
{
    public string Id { get; set; }

    public long Sequence { get; set; }
}