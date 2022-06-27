using System.Collections.Generic;

namespace Mondeto.Core
{

public class BidirectionalDictionary<TKey, TValue>
{
    Dictionary<TKey, TValue> forwardDict = new Dictionary<TKey, TValue>();
    Dictionary<TValue, TKey> backwardDict = new Dictionary<TValue, TKey>();

    public IReadOnlyDictionary<TKey, TValue> Forward
    {
        get => forwardDict;
    }

    public IReadOnlyDictionary<TValue, TKey> Backward
    {
        get => backwardDict;
    }

    public void Add(TKey key, TValue value)
    {
        forwardDict[key] = value;
        backwardDict[value] = key;
    }

    public bool Remove(TKey key)
    {
        if (!forwardDict.ContainsKey(key)) return false;

        var value = forwardDict[key];
        forwardDict.Remove(key);
        backwardDict.Remove(value);

        return true;
    }
}

} // end namespace