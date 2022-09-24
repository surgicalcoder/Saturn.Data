using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace GoLive.Saturn.Data.Entities
{
    public class Ref<T> : IEquatable<Ref<T>> where T : Entity, new()
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

        public static implicit operator string(Ref<T> item)
        {
            if (item == null)
            {
                return null;
            }

            if (item.Item != null && !string.IsNullOrWhiteSpace(item.Item.Id))
            {
                return item.Item.Id;
            }
            
            if (!string.IsNullOrWhiteSpace(item.Id))
            {
                return item.Id;
            }

            return null;
        }

        public static implicit operator T(Ref<T> item)
        {
            if (item == null)
            {
                return null;
            }

            if (item.Item != null)
            {
                return item.Item;
            }

            return item.Id != null ? new T(){Id = item.Id} : null;
        }

        public static implicit operator Ref<T>(T item)
        {
            return item == default ? default : new Ref<T>() { Item = item };
        }

        public static implicit operator Ref<T>(string item)
        {
            return string.IsNullOrWhiteSpace(item) ? default : new Ref<T>(item);
        }

        public static implicit operator Entity(Ref<T> item)
        {
            return item?.Item;
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

        public void Fetch(IQueryable<T> items)
        {
            Item = items.FirstOrDefault(f => f.Id == _refId);
        }

        public void Fetch(IList<T> items)
        {
            Item = items.FirstOrDefault(f => f.Id == _refId);
        }

        public void Fetch(Func<string, T> expr)
        {
            this.Item = expr.Invoke(this.Id);
        }

        static bool IsSubclassOfRawGeneric(Type generic, Type toCheck)
        {
            while (toCheck != null && toCheck != typeof(object))
            {
                var cur = toCheck.IsGenericType ? toCheck.GetGenericTypeDefinition() : toCheck;

                if (generic == cur)
                {
                    return true;
                }

                toCheck = toCheck.BaseType;
            }
            return false;
        }

        public Ref()
        {
        }

        /*public string Type => typeof(T).FullName;*/

        public bool Equals(Ref<T> other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(_refId, other._refId);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((Ref<T>)obj);
        }

        public override int GetHashCode()
        {
            return (_refId != null ? _refId.GetHashCode() : 0);
        }

        public static bool operator ==(Ref<T> left, Ref<T> right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(Ref<T> left, Ref<T> right)
        {
            return !Equals(left, right);
        }
    }
}