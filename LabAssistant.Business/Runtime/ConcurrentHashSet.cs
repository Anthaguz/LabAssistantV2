using System.Collections;
using System.Collections.Concurrent;

namespace LabAssistant.Business.Runtime;

/// <summary>
/// A thread-safe <see cref="ISet{T}"/> backed by a <see cref="ConcurrentDictionary{TKey, TValue}"/>.
/// </summary>
/// <remarks>
/// The V2 runtime records executed node ids from executors that run concurrently: the graph scheduler
/// dispatches ready nodes in parallel, and the legacy pipeline fans out with <c>Task.WhenAll</c>.
/// <see cref="HashSet{T}"/> is not safe for concurrent mutation, so the executed-id collection is backed
/// by this type instead. On the concurrent hot path only <see cref="Add(T)"/> and enumeration are used;
/// the set-algebra members exist for interface completeness and are not intended for concurrent use.
/// </remarks>
internal sealed class ConcurrentHashSet<T> : ISet<T>, IReadOnlyCollection<T>
    where T : notnull
{
    private readonly ConcurrentDictionary<T, byte> _items;
    private readonly IEqualityComparer<T> _comparer;

    /// <summary>Creates an empty set that compares elements with <paramref name="comparer"/>.</summary>
    public ConcurrentHashSet(IEqualityComparer<T> comparer)
    {
        _comparer = comparer;
        _items = new ConcurrentDictionary<T, byte>(comparer);
    }

    /// <inheritdoc />
    public int Count => _items.Count;

    /// <inheritdoc />
    public bool IsReadOnly => false;

    /// <summary>Adds an item, returning <see langword="true"/> only if it was not already present.</summary>
    public bool Add(T item) => _items.TryAdd(item, 0);

    void ICollection<T>.Add(T item) => _items.TryAdd(item, 0);

    /// <inheritdoc />
    public bool Remove(T item) => _items.TryRemove(item, out _);

    /// <inheritdoc />
    public void Clear() => _items.Clear();

    /// <inheritdoc />
    public bool Contains(T item) => _items.ContainsKey(item);

    /// <inheritdoc />
    public void CopyTo(T[] array, int arrayIndex) => _items.Keys.CopyTo(array, arrayIndex);

    /// <inheritdoc />
    public IEnumerator<T> GetEnumerator() => _items.Keys.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc />
    public void ExceptWith(IEnumerable<T> other)
    {
        foreach (var item in other)
        {
            _items.TryRemove(item, out _);
        }
    }

    /// <inheritdoc />
    public void IntersectWith(IEnumerable<T> other)
    {
        var keep = new HashSet<T>(other, _comparer);
        foreach (var key in _items.Keys)
        {
            if (!keep.Contains(key))
            {
                _items.TryRemove(key, out _);
            }
        }
    }

    /// <inheritdoc />
    public void UnionWith(IEnumerable<T> other)
    {
        foreach (var item in other)
        {
            _items.TryAdd(item, 0);
        }
    }

    /// <inheritdoc />
    public void SymmetricExceptWith(IEnumerable<T> other)
    {
        foreach (var item in other)
        {
            if (!_items.TryRemove(item, out _))
            {
                _items.TryAdd(item, 0);
            }
        }
    }

    /// <inheritdoc />
    public bool IsSubsetOf(IEnumerable<T> other) => Snapshot().IsSubsetOf(other);

    /// <inheritdoc />
    public bool IsSupersetOf(IEnumerable<T> other) => Snapshot().IsSupersetOf(other);

    /// <inheritdoc />
    public bool IsProperSubsetOf(IEnumerable<T> other) => Snapshot().IsProperSubsetOf(other);

    /// <inheritdoc />
    public bool IsProperSupersetOf(IEnumerable<T> other) => Snapshot().IsProperSupersetOf(other);

    /// <inheritdoc />
    public bool Overlaps(IEnumerable<T> other) => Snapshot().Overlaps(other);

    /// <inheritdoc />
    public bool SetEquals(IEnumerable<T> other) => Snapshot().SetEquals(other);

    private HashSet<T> Snapshot() => new(_items.Keys, _comparer);
}
