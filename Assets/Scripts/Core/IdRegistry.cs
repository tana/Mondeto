using System;
using System.Collections.Generic;

public class IdRegistry
{
    uint lastId;
    LinkedList<uint> deleted = new LinkedList<uint>();

    object lockObject = new object();

    public IdRegistry(uint min)
    {
        lastId = min - 1;
    }

    public uint Create()
    {
        lock (lockObject)
        {
            // TODO reuse
            lastId++;
        }
        return lastId;
    }

    public void Delete(uint id)
    {
        deleted.AddLast(id);
    }
}