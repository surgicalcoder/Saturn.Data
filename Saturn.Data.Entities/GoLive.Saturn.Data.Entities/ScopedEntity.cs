namespace GoLive.Saturn.Data.Entities
{
    public abstract class ScopedEntity<T> : Entity where T : Entity, new()
    {
        public virtual Ref<T> Scope { get; set; }
    }
}