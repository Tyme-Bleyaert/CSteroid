using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace CSteroid.Generator.Shared
{
    /// <summary>An order-sensitive immutable sequence with value equality.</summary>
    public readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IReadOnlyList<T>
        where T : IEquatable<T>
    {
        private readonly ImmutableArray<T> _items;

        public EquatableArray(ImmutableArray<T> items)
        {
            _items = items;
        }

        public EquatableArray(IEnumerable<T> items)
            : this(ImmutableArray.CreateRange(items))
        {
        }

        public int Count => _items.IsDefault ? 0 : _items.Length;
        public T this[int index] => _items[index];

        public ImmutableArray<T>.Enumerator GetEnumerator() =>
            (_items.IsDefault ? ImmutableArray<T>.Empty : _items).GetEnumerator();

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            for (int i = 0; i < Count; i++)
                yield return _items[i];
        }

        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<T>)this).GetEnumerator();

        public bool Equals(EquatableArray<T> other)
        {
            var left = _items;
            var right = other._items;
            if (left.IsDefaultOrEmpty)
                return right.IsDefaultOrEmpty;
            if (right.IsDefaultOrEmpty || right.Length != left.Length)
                return false;

            for (int i = 0; i < left.Length; i++)
            {
                if (!left[i].Equals(right[i]))
                    return false;
            }

            return true;
        }

        public override bool Equals(object obj) => obj is EquatableArray<T> other && Equals(other);

        public override int GetHashCode()
        {
            if (_items.IsDefaultOrEmpty)
                return 0;

            unchecked
            {
                int hash = 17;
                foreach (var item in _items)
                    hash = (hash * 31) + item.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(EquatableArray<T> left, EquatableArray<T> right) => left.Equals(right);
        public static bool operator !=(EquatableArray<T> left, EquatableArray<T> right) => !left.Equals(right);
    }
}
