using System;

public class GrabbableTag
{
    SyncObject obj;
    SyncNode node;

    public void Initialize(SyncObject syncObject)
    {
        obj = syncObject;
        node = obj.Node;

        obj.RegisterEventHandler("grab", OnGrab);
        obj.RegisterEventHandler("ungrab", OnUngrab);
        obj.WriteDebugLog("grabbable", $"Registered event handlers");
    }

    void OnGrab(uint sender, IValue[] args)
    {
        // Calculate coordinate of this object relative to sender
        Vec relPos;
        Quat relRot;
        if (!obj.CalcRelativeCoord(node.Objects[sender], out relPos, out relRot))
        {
            // When calculation failed
            relPos = new Vec();
            relRot = new Quat();
        }

        // Become a child of sender
        obj.SetField("parent", node.Objects[sender].GetObjectRef());
        // Set relative coordinate
        // Keep relative position/rotation at the time of grabbing
        obj.SetField("position", relPos);
        obj.SetField("rotation", relRot);

        obj.WriteDebugLog("grabbable", $"Grabbed by object {sender}");
    }

    void OnUngrab(uint sender, IValue[] args)
    {
        // Calculate current world coordinate of this object
        Vec worldPos;
        Quat worldRot;
        if (!obj.CalcWorldCoord(out worldPos, out worldRot))
        {
            // When world coordinate calculation failed
            worldPos = new Vec();
            worldRot = new Quat();
        }

        // Become a child of World Object
        // TODO: more consideration about hierarchy (e.g. restore parent). probably as an option.
        obj.SetField("parent", node.Objects[SyncNode.WorldObjectId].GetObjectRef());
        // Keep current world coordinate
        obj.SetField("position", worldPos);
        obj.SetField("rotation", worldRot);

        obj.WriteDebugLog("grabbable", $"Ungrabbed by object {sender}");
    }
}