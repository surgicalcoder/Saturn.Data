namespace GoLive.Saturn.Data.Entities
{
    public abstract class ExternallyPopulatedEntity : Entity
    {
        public string ExernalId { get; set; }
        public string ExternalPopulator { get; set; }

        public bool Disabled { get; set; }
    }
}