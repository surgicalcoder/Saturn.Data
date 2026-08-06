namespace GoLive.Saturn.Data.Abstractions.ChangeFeed;

public sealed class ChangeFeedBehaviorOptions
{
    public bool Enabled { get; set; } = false;

    public FeedPayloadMode PayloadMode { get; set; } = FeedPayloadMode.Item;

    public bool FeedPartialOps { get; set; } = true;
}