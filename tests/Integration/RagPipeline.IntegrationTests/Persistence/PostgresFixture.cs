namespace RagPipeline.IntegrationTests.Persistence;

using RagPipeline.IntegrationTests.Support;
using Xunit;

public class PostgresFixture : RagPipeline.IntegrationTests.Support.PostgresFixture
{
}

[CollectionDefinition("PostgresDatabase")]
public class PostgresCollection : ICollectionFixture<PostgresFixture>
{
}
