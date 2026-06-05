namespace GoLive.Saturn.Data.Entities;

public interface IAuditable
{
    string CreatedBy { get; set; }
    string LastModifiedBy { get; set; }
}

