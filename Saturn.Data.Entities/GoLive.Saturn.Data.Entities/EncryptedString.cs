namespace GoLive.Saturn.Data.Entities
{
    public sealed class EncryptedString : IUpdatableFrom<EncryptedString>
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

        public void UpdateFrom(EncryptedString input)
        {
            if (input == null || input.Populated == false)
            {
                Decoded = null;
                Encoded = null;
                Hash = null;
                Salt = null;

                return;
            }

            if (string.IsNullOrEmpty(input.Decoded))
            {
                return;
            }

            Decoded = input.Decoded;
            Encoded = null;
            Hash = null;
            Salt = null;
        }
    }
}