using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GoLive.Saturn.Data.Entities;

namespace GoLive.Saturn.Data.Abstractions
{
    public class RepositoryOptions
    {
        public RepositoryOptions()
        {
            GetCollectionName = delegate(Type type)
            {
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(WrappedEntity<>))
                {
                    return $"{WrappedEntityPrefix}{type.GenericTypeArguments[0].Name}{WrappedEntityPostfix}";
                }
                return type.Name;
            };
        }
        
        public string ConnectionString { get; set; }

        public string WrappedEntityPrefix { get; set; } = "w_";
        public string WrappedEntityPostfix { get; set; }

        public bool DebugMode { get; set; }

        public Action<CommandStartedArgs> CommandStartedCallback { get; set; }

        public Action<CommandCompletedArgs> CommandCompletedCallback { get; set; }

        public Action<CommandFailedArgs> CommandFailedCallback { get; set; }

        public Func<IRepository, Task> InitCallback { get; set; }

        public Func<Type, string> GetCollectionName { get; set; }
        
        public Func<Type, string> TransparentScopeProvider { get; set; }

        public Func<Type, bool> ObjectSerializerConfiguration { get; set; } = type => true;

        public Dictionary<Type, Type> GenericSerializers { get; set; } = new();
        public Dictionary<Type, object> DiscriminatorConventions { get; set; } = new();
        public Dictionary<Type, object> Serializers { get; set; } = new();
    }
}