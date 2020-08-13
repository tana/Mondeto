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

        if (obj.TryGetField<ObjectRef>("objectToMove", out ObjectRef objToMoveRef) &&
            obj.TryGetField<Vec>("destination", out Vec destination))
        {
            if (!node.Objects.ContainsKey(objToMoveRef.Id)) return;    // something is wrong
            SyncObject objToMove = node.Objects[objToMoveRef.Id];
            objToMove.SetField("position", destination);

            // FIXME: velocity/angularVelocity setting is unstable

            if (obj.TryGetField<Vec>("launchVelocity", out Vec launchVel))
                objToMove.SetField("velocity", launchVel);
            else
                objToMove.SetField("velocity", new Vec(0, 0, 0));

            if (obj.TryGetField<Vec>("launchAngularVelocity", out Vec launchAngVel))
                objToMove.SetField("angularVelocity", launchAngVel);
            else
                objToMove.SetField("angularVelocity", new Vec(0, 0, 0));
        }
    }
}
