using System.Linq;
using System.Text;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;

namespace GoLive.Saturn.Data.Conventions
{
    public class IdConvention : ConventionBase, IClassMapConvention
    {
        public void Apply(BsonClassMap classMap)
        {
            var enumerable = classMap.AllMemberMaps.Select(x => x.ElementName).ToList();

            var items = enumerable.Where(x => enumerable.Contains(x.ToLower() + "id"));

            foreach (var val in items)
            {
                classMap.UnmapProperty(val);
            }
        }
    }
}
