using Xunit;

namespace Saturn.Data.LiteDbX.Tests;

[CollectionDefinition("ChangeFeed")]
public class ChangeFeedCollectionDefinition : ICollectionFixture<ChangeFeedTestFixture>
{
}
