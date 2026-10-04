namespace GoLive.Saturn.Data.Migrations;

public interface IMigrationModule
{
    void Register(MigrationRunner runner);
}
