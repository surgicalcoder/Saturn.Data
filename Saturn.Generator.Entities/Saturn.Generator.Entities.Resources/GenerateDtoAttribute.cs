using System;

namespace GoLive.Saturn.Generator.Entities.Resources;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class GenerateDtoAttribute : Attribute
{
    public string Name { get; set; }

    public bool IncludeProperties { get; set; }

    public bool TrackChanges { get; set; }

    public bool ExpandRefs { get; set; }

    public bool UseFullId { get; set; }
}
