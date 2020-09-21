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
        if (!CalcRelativeCoord(obj, node.Objects[sender], out relPos, out relRot))
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
        if (!CalcWorldCoord(obj, out worldPos, out worldRot))
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

    // Calculate world coordinate of an object
    // Note: when return value is false (failed), position and rotation will be null (because these are reference type)
    bool CalcWorldCoord(SyncObject obj, out Vec position, out Quat rotation, int depth = 0)
    {
        if (obj.TryGetField("position", out Vec pos) && obj.TryGetField("rotation", out Quat rot))
        {
            if (obj.TryGetField("parent", out ObjectRef parent))
            {
                // Recursively calculate world coord of parent
                // (with recursion depth limit)
                if (depth < 20 && CalcWorldCoord(node.Objects[parent.Id], out Vec parentPos, out Quat parentRot, depth + 1))
                {
                    position = parentPos + parentRot * pos;
                    rotation = parentRot * rot;
                    return true;
                }
                else
                {
                    position = default;
                    rotation = default;
                    return false;
                }
            }
            else
            {
                // No parent
                position = pos;
                rotation = rot;
                return true;
            }
        }
        else
        {
            position = default;
            rotation = default;
            return false;
        }
    }

    // Calculate coordinate of obj relative to refObj
    // Note: when return value is false (failed), position and rotation will be null (because these are reference type)
    bool CalcRelativeCoord(SyncObject obj, SyncObject refObj, out Vec position, out Quat rotation)
    {
        position = default;
        rotation = default;

        // Calculate world coordinate of obj
        Vec worldPos;
        Quat worldRot;
        if (!CalcWorldCoord(obj, out worldPos, out worldRot)) return false;

        // Calculate world coordinate of refObj
        Vec refWorldPos;
        Quat refWorldRot;
        if (!CalcWorldCoord(refObj, out refWorldPos, out refWorldRot)) return false;

        // Calculate relative coordinate
        position = refWorldRot.Conjugate() * (worldPos - refWorldPos);
        rotation = refWorldRot.Conjugate() * worldRot;
        return true;
    }
}