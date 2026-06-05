using System.Collections.Generic;

namespace GoLive.Saturn.Data.Entities;

public interface ITaggable
{
    IList<string> Tags { get; set; }
}

