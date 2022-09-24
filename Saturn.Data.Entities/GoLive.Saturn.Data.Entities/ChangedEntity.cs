namespace GoLive.Saturn.Data.Entities
{
    public class ChangedEntity<T> where T : Entity
    {
        public T FullDocument { get; set; }

    }
}