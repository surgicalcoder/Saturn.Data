using System;

namespace GoLive.Saturn.Data.ChangeTracking;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class ChangeTrackingAttribute : Attribute
{
    public ChangeTrackingMode Mode { get; set; } = ChangeTrackingMode.Journal;

    public bool TrackRefItemChanges { get; set; }
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class NoChangeTrackingAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public class CollectionTrackingAttribute : Attribute
{
    public CollectionStrategy Strategy { get; set; } = CollectionStrategy.WholeArray;

    public bool Instrument { get; set; }
}
