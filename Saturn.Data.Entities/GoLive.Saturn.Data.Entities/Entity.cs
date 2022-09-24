using System;
using System.Collections.Generic;

namespace GoLive.Saturn.Data.Entities
{
    public abstract class Entity : IEquatable<Entity>
    {
        public virtual string Id { get; set; }
        public virtual long? Version { get; set; }
        public virtual Dictionary<string, dynamic> Properties { get; set; } = new Dictionary<string, object>();

        protected Entity()
        { }

        public bool Equals(Entity other)
        {
            return other != null && Id == other.Id;
        }
    }
}
