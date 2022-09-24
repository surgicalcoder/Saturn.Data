namespace GoLive.Saturn.Data.Entities
{
    public sealed class EncryptedString : Entity
    {
        public string Decoded { get; set; }
        public string Encoded { get; set; }
        public string Salt { get; set; }
        public string Hash { get; set; }
        public bool Populated { get; set; }

        public static implicit operator EncryptedString(string Decoded)
        {
            return new EncryptedString { Decoded = Decoded };
        }
    }
}