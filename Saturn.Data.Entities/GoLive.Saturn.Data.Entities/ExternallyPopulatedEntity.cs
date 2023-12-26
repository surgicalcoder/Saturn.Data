namespace GoLive.Saturn.Data.Entities;

public abstract class ExternallyPopulatedEntity : Entity
{
    public virtual string ExternalId { get; set; }
    public virtual string ExternalPopulator { get; set; }

    public virtual bool Disabled { get; set; }
}