using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Mondeto.Core
{

// TODO name
public class Runner<T>
{
    Queue<(Func<T>, TaskCompletionSource<T>)> queue = new Queue<(Func<T>, TaskCompletionSource<T>)>();

    public void Run()
    {
        while (queue.Count > 0)
        {
            lock (queue)
            {
                var (func, tcs) = queue.Dequeue();
                tcs.SetResult(func());
            }
        }
    }

    public Task<T> Schedule(Func<T> func)
    {
        var tcs = new TaskCompletionSource<T>();
        lock (queue)
        {
            queue.Enqueue((func, tcs));
        }
        return tcs.Task;
    }
}

} // end namespace