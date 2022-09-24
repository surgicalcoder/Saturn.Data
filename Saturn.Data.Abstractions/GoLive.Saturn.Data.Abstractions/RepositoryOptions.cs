using System;
using System.Collections.Generic;

namespace GoLive.Saturn.Data.Abstractions
{
    public class RepositoryOptions
    {
        public RepositoryOptions()
        {
            GenericSerializers = new Dictionary<Type, Type>();
            DiscriminatorConventions = new Dictionary<Type, object>();
            Serializers = new Dictionary<Type, object>();
            WrappedEntityPrefix = "w_";
        }

        public string ConnectionString { get; set; }

        public string WrappedEntityPrefix { get; set; }

        public bool DebugMode { get; set; }

        public Action<CommandStartedArgs> CommandStartedCallback { get; set; }

        public Action<CommandCompletedArgs> CommandCompletedCallback { get; set; }

        public Action<CommandFailedArgs> CommandFailedCallback { get; set; }

        public TimeSpan InitDuration { get; set; }

        public Action<IRepository> InitCallback { get; set; }

        public Func<string, string> CollectionNameOverride { get; set; }

        public Dictionary<Type, Type> GenericSerializers { get; set; }
        public Dictionary<Type, Object> DiscriminatorConventions { get; set; }
        public Dictionary<Type, Object> Serializers { get; set; }

    }
}