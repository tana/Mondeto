using Mondeto.Core;

namespace Mondeto
{

public class ObjectMoveButtonTag : SimpleTag
{
    SyncObject obj;
    SyncNode node;

    public override void Setup(SyncObject syncObject)
    {
        obj = syncObject;
        node = obj.Node;

        obj.RegisterEventHandler("collisionStart", OnCollisionStart);
        obj.WriteDebugLog("objectMoveButton", $"Registered event handlers");
    }

    void OnCollisionStart(uint sender, IValue[] args)
    {
        obj.WriteDebugLog("objectMoveButton", $"Touched by object {sender}");

        if (obj.TryGetField<ObjectRef>("objectToMove", out ObjectRef objToMoveRef))
        {
            if (!node.Objects.ContainsKey(objToMoveRef.Id)) return;    // something is wrong
            SyncObject objToMove = node.Objects[objToMoveRef.Id];

            if (obj.TryGetField<Vec>("destination", out Vec destination))
                objToMove.SetField("position", destination);
            
            if (obj.TryGetField<Quat>("destRotation", out Quat destRotation))
                objToMove.SetField("rotation", destRotation);

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

    public override void Cleanup(SyncObject syncObject)
    {
        obj.DeleteEventHandler("collisionStart", OnCollisionStart);
    }
}

}   // end namespace