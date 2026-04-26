namespace GoLive.Saturn.Data.Entities;

public interface ISecondScopedById : IScopedById
{
    string SecondScopeId { get; set; }
}

