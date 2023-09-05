using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;

namespace GoLive.Saturn.Data.Entities
{
    public partial class Ref<T> : IEquatable<Ref<T>>, INotifyPropertyChanged where T : Entity, new()
    {
        public Ref(string refId)
        {
            _refId = refId;
        }

        public Ref(T item)
        {
            Item = item;

            if (item != null && !String.IsNullOrWhiteSpace(item.Id))
            {
                Id = item.Id;
            }
        }

        private string _refId;

        public T Item { get; set; }

        public string Id
        {
            get
            {
                if (Item != null && !string.IsNullOrWhiteSpace(Item.Id))
                {
                    _refId = Item.Id;
                    return Item.Id;
                }

                return _refId;
            }
            set { _refId = value; }
        }

        public override string ToString()
        {
            return Id;
        }
        
        public Ref()
        {
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;

            field = value;
            OnPropertyChanged(propertyName);

            return true;
        }
    }
}