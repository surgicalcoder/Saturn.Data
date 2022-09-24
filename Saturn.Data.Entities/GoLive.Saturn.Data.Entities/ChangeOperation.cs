namespace GoLive.Saturn.Data.Entities
{
    public enum ChangeOperation
    {
        /// <summary>An insert operation type.</summary>
        Insert,
        /// <summary>An update operation type.</summary>
        Update,
        /// <summary>A replace operation type.</summary>
        Replace,
        /// <summary>A delete operation type.</summary>
        Delete,
        /// <summary>An invalidate operation type.</summary>
        Invalidate,
    }
}