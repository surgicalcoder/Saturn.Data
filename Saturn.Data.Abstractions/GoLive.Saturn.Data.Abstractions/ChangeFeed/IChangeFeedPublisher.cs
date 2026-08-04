using System.Threading;
using System.Threading.Tasks;

namespace GoLive.Saturn.Data.Abstractions.ChangeFeed;

public interface IChangeFeedPublisher
{
    ValueTask PublishAsync(DataChangeEvent change, CancellationToken ct);
}
