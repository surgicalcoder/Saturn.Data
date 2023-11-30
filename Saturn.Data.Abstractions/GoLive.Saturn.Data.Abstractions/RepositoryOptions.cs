using System;
using System.Collections.Generic;

namespace GoLive.Saturn.Data.Abstractions
{
    public class RepositoryOptions
    {
        public string ConnectionString { get; set; }

        public string WrappedEntityPrefix { get; set; } = "w_";
        public string WrappedEntityPostfix { get; set; }

        public bool DebugMode { get; set; }

        public Action<CommandStartedArgs> CommandStartedCallback { get; set; }

        public Action<CommandCompletedArgs> CommandCompletedCallback { get; set; }

        public Action<CommandFailedArgs> CommandFailedCallback { get; set; }

        public TimeSpan InitCheckDuration { get; set; }

        public Action<IRepository> InitCheckCallback { get; set; }

        public Func<Type, string> GetCollectionName { get; set; } = type => type.Name;
        
        public Func<Type, string> TransparentScopeProvider { get; set; }

        public Dictionary<Type, Type> GenericSerializers { get; set; } = new();
        public Dictionary<Type, object> DiscriminatorConventions { get; set; } = new();
        public Dictionary<Type, object> Serializers { get; set; } = new();
    }
}