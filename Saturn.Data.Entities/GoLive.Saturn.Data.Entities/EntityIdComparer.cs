using System.Collections.Generic;

namespace GoLive.Saturn.Data.Entities
{
    public class EntityIdComparer : IEqualityComparer<Entity>
    {
        public bool Equals(Entity x, Entity y)
        {
            if (x == null || y == null || x.Id == null || y.Id == null)
            {
                return false;
            }

            return x.Id.Equals(y.Id);
        }

        public int GetHashCode(Entity obj)
        {
            return obj.GetHashCode();
        }

        public static IEqualityComparer<Entity> Default = new EntityIdComparer();
    }
}