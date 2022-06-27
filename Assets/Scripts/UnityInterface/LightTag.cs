using UnityEngine;
using Mondeto.Core;

namespace Mondeto
{

public class LightTag : MonoBehaviour, ITag
{
    public void Setup(SyncObject obj)
    {
        // https://docs.unity3d.com/ja/2019.4/Manual/Lighting.html
        var light = gameObject.AddComponent<Light>();

        light.shadows = LightShadows.Soft;  // TODO:

        // TODO: real-time sync
        if (obj.TryGetField("color", out Vec colorVec))
        {
            light.color = new Color(colorVec.X, colorVec.Y, colorVec.Z);
        }
        // https://docs.unity3d.com/ja/2019.4/Manual/Lighting.html
        if (obj.TryGetFieldPrimitive("lightType", out string lightType))
        {
            switch (lightType)
            {
                case "directional":
                    light.type = LightType.Directional;
                    break;
                case "point":
                    light.type = LightType.Point;
                    break;
                // TODO
            }
        }
    }

    public void Cleanup(SyncObject obj)
    {
    }
}

}   // end namespace