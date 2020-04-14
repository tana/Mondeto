using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Tasks;

public class BlobStorage
{
    ConcurrentDictionary<BlobHandle, TaskCompletionSource<Blob>> dict = new ConcurrentDictionary<BlobHandle, TaskCompletionSource<Blob>>();

    public void Write(BlobHandle handle, Blob blob)
    {
        Logger.Write($"Blob {handle} writing");

        var tcs = dict.GetOrAdd(handle, new TaskCompletionSource<Blob>());
        if (tcs.Task.IsCompleted)
        {
            Logger.Write($"Trying to overwrite {handle}. Ignoring");
            return;
        }
        tcs.SetResult(blob);
    }

    public Task<Blob> Read(BlobHandle handle)
    {
        return dict.GetOrAdd(handle, new TaskCompletionSource<Blob>()).Task;
    }
}