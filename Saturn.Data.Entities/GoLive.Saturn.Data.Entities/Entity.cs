using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GoLive.Saturn.Data.Entities
{
    public abstract class Entity : IEquatable<Entity>, INotifyPropertyChanged
    {
        private string id;
        private long? version;

        public virtual string Id
        {
            get => id;
            set => SetField(ref id, value);
        }

        public virtual long? Version
        {
            get => version;
            set => SetField(ref version, value);
        }

        public virtual Dictionary<string, object> Changes { get; set; } = new();

        public virtual bool EnableChangeTracking { get; set; }
        
        public virtual Dictionary<string, dynamic> Properties { get; set; } = new Dictionary<string, object>();

        protected Entity()
        { }

        public virtual bool Equals(Entity other)
        {
            return other != null && Id == other.Id;
        }

        public virtual event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected virtual bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;

            field = value;
            OnPropertyChanged(propertyName);

            return true;
        }
    }
}
