namespace GoLive.Saturn.Data.Entities
{
    public sealed class HashedString : Entity
    {
        public string Decoded { get; set; }
        public string Salt { get; set; }
        public string Hash { get; set; }

        public static implicit operator HashedString(string Decoded)
        {
            return new HashedString { Decoded = Decoded };
        }
    }
}