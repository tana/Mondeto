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
        Logger.Debug("grabbable", $"Registered event handlers");
    }

    void OnGrab(uint sender, IValue[] args)
    {
        // Become a child of sender
        obj.SetField("parent", node.Objects[sender].GetObjectRef());
        // Set relative coordinate
        // TODO: specify grabbing point (relative coordinate) using fields
        //       (or keep relative position/rotation at the time of grabbing?)
        obj.SetField("position", new Vec());    // (0,0,0)
        obj.SetField("rotation", new Quat());   // identity

        Logger.Debug("grabbable", $"Grabbed by object {sender}");
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

        Logger.Debug("grabbable", $"Ungrabbed by object {sender}");
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
}