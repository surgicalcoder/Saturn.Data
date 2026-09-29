using Saturn.Data.Testing.Shared.Cascade;

namespace Saturn.Data.DocumentDb.Tests;

public class CascadeTests(CascadeTestFixture fixture)
    : CascadeContractTests<CascadeTestFixture, UnitTestableDocumentDbRepository>(fixture), IClassFixture<CascadeTestFixture>;
