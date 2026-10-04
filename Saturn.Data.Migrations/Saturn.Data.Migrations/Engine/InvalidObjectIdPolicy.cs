namespace GoLive.Saturn.Data.Migrations;

public enum InvalidObjectIdPolicy
{
    Fail,
    SkipDocument,
    LeaveUnchanged,
    RemoveField,
    GenerateNewId
}
