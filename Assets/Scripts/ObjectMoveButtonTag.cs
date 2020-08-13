using System;

public class ObjectMoveButtonTag
{
    SyncObject obj;
    SyncNode node;

    public void Initialize(SyncObject syncObject)
    {
        obj = syncObject;
        node = obj.Node;

        obj.RegisterEventHandler("collisionStart", OnCollisionStart);
        Logger.Debug("objectMoveButton", $"Registered event handlers");
    }

    void OnCollisionStart(uint sender, IValue[] args)
    {
        Logger.Debug("objectMoveButton", $"Touched by object {sender}");

        if (obj.TryGetField<ObjectRef>("objectToMove", out ObjectRef objToMove) &&
            obj.TryGetField<Vec>("destination", out Vec destination))
        {
            if (!node.Objects.ContainsKey(objToMove.Id)) return;    // something is wrong
            // FIXME: not working
            node.Objects[objToMove.Id].SetField("position", destination);
            node.Objects[objToMove.Id].SetField("velocity", new Vec(0.0f, 0.0f, 0.0f));
            node.Objects[objToMove.Id].SetField("angularVelocity", new Vec(0.0f, 0.0f, 0.0f));
        }
    }
}
