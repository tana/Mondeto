using System.Collections.Generic;
using System.Threading.Tasks;

class CompletionNotifier<Key, Result>
{
    Dictionary<Key, TaskCompletionSource<Result>> dict = new Dictionary<Key, TaskCompletionSource<Result>>();
    public Task<Result> Wait(Key key)
    {
        var tcs = new TaskCompletionSource<Result>();
        lock (dict)
        {
            dict[key] = tcs;
        }

        return tcs.Task;
    }

    public bool IsWaiting(Key key)
    {
        lock (dict)
        {
            return dict.ContainsKey(key);
        }
    }

    public void Notify(Key key, Result result)
    {
        TaskCompletionSource<Result> tcs;
        lock (dict)
        {
            tcs = dict[key];
            dict.Remove(key);
        }
        tcs.SetResult(result);
    }
}