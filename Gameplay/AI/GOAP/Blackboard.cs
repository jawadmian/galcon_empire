using System.Collections.Concurrent;
using System.Collections.Generic;

public class Blackboard
{
    // Using a ConcurrentDictionary to ensure thread safety when agents plan on background threads
    private readonly ConcurrentDictionary<string, object> _data = new ConcurrentDictionary<string, object>();

    public void SetValue(string key, object value)
    {
        _data[key] = value;
    }

    public T GetValue<T>(string key, T defaultValue = default)
    {
        if (_data.TryGetValue(key, out object value) && value is T typedValue)
        {
            return typedValue;
        }
        return defaultValue;
    }

    public bool HasKey(string key)
    {
        return _data.ContainsKey(key);
    }

    public void RemoveKey(string key)
    {
        _data.TryRemove(key, out _);
    }

    // Creates a shallow clone of the blackboard state.
    // This is vital for the GOAP Planner's A* search, which needs to modify temporary states.
    public Blackboard Clone()
    {
        var clone = new Blackboard();
        foreach (var kvp in _data)
        {
            clone.SetValue(kvp.Key, kvp.Value);
        }
        return clone;
    }

    public override bool Equals(object obj)
    {
        if (obj is not Blackboard other) return false;
        
        if (_data.Count != other._data.Count) return false;

        foreach (var kvp in _data)
        {
            if (!other._data.TryGetValue(kvp.Key, out object otherVal)) return false;
            if (!Equals(kvp.Value, otherVal)) return false;
        }

        return true;
    }

    public override int GetHashCode()
    {
        int hash = 0;
        foreach (var kvp in _data)
        {
            int kvpHash = (kvp.Key.GetHashCode() * 397) ^ (kvp.Value?.GetHashCode() ?? 0);
            hash ^= kvpHash; // XOR is order-independent
        }
        return hash;
    }
}
