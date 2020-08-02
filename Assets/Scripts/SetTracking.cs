using UnityEngine;
using UnityEngine.XR;

public class SetTracking : MonoBehaviour
{
    public void Start()
    {
        // Force stationary tracking mode.
        // (For old built-in XR system)
        XRDevice.SetTrackingSpaceType(TrackingSpaceType.Stationary);
        InputTracking.Recenter();
    }
}