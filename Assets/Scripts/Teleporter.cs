using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public class Teleporter : MonoBehaviour
{
    // Parabola is defined by two parameters.
    // Two parameters are named in analogy with launching something towards sky.
    public float InitialVelocity = 10;
    public float Gravity = 10;

    // Approximation parameters
    public float Step = 0.3f;
    public float MaxDescent = 10;

    public Transform Controller;

    public LineRenderer ParabolaRenderer;

    enum TeleportState
    {
        Idle, Aiming, Moving
    }

    TeleportState state = TeleportState.Idle;
    bool teleportable = false;
    Vector3 target;

    InputDevice? inputDevice = null;

    bool lastButtonValue = false;

    // Start is called before the first frame update
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
        if (!inputDevice.HasValue)
        {
            // FIXME: currently, cannot find controller during Start().
            // Search left controller
            //  See https://docs.unity3d.com/ja/2019.4/Manual/xr_input.html
            var devices = new List<InputDevice>();
            InputDevices.GetDevicesWithCharacteristics(
                InputDeviceCharacteristics.Left | InputDeviceCharacteristics.Controller,
                devices
            );
            if (devices.Count != 0)
                inputDevice = devices[0];
        }

        if (inputDevice.HasValue && inputDevice.Value.TryGetFeatureValue(CommonUsages.primaryButton, out bool buttonValue))
        {
            if (!lastButtonValue && buttonValue) ButtonDown();
            else if (lastButtonValue && !buttonValue) ButtonUp();
            lastButtonValue = buttonValue;
        }

        if (state == TeleportState.Aiming)
        {
            CastParabola(Controller.forward);
        }
        else if (state == TeleportState.Moving)
        {
            // CharacterController must be temporally disabled for teleporting
            // (See https://forum.unity.com/threads/does-transform-position-work-on-a-charactercontroller.36149/#post-4132021 )
            GetComponent<CharacterController>().enabled = false;
            transform.position = target;
            GetComponent<CharacterController>().enabled = true;
            state = TeleportState.Idle;
        }
    }

    void ButtonDown()
    {
        if (state != TeleportState.Idle) return;
        state = TeleportState.Aiming;
        ParabolaRenderer.enabled = true;
    }

    void ButtonUp()
    {
        if (state != TeleportState.Aiming) return;
        if (teleportable)
        {
            state = TeleportState.Moving;
        }
        else
        {
            state = TeleportState.Idle;
        }
        ParabolaRenderer.enabled = false;
    }

    void CastParabola(Vector3 launchDir) // launchDir has to be normalized
    {
        Vector3 localDir = Quaternion.Inverse(transform.rotation) * launchDir;
        Vector2 localDir2D = (new Vector2(localDir.z, localDir.y)).normalized;

        // x = vx*t, y = vy*t - g*t^2
        float vx = InitialVelocity * localDir2D.x;
        float vy = InitialVelocity * localDir2D.y;
        float dt = Step / vx;
        // vy*t-g*t^2 = y
        // -g*t^2 + vy*t - y = 0
        // -vy ± sqrt(vy^2 - 4*g*y)
        float maxT = -vy + Mathf.Sqrt(vy * vy - 4 * Gravity * (-MaxDescent));
        int divs = (int)Mathf.Ceil(maxT / dt);

        Vector3[] points = new Vector3[divs];
        points[0] = Controller.position;
        int count = 0;
        for (int i = 1; i < divs; i++)
        {
            float t = dt * i;
            float x = vx * t;
            float y = vy * t - Gravity * t * t;
            points[i] = Controller.position + transform.TransformVector(
                localDir.x * x, y, localDir.y * x
            );
            count++;

            // hit test
            RaycastHit hit;
            if (Physics.Linecast(points[i - 1], points[i], out hit))
            {
                target = hit.point;
                teleportable = true;
                points[i] = hit.point;
                break;
            }
        }

        ParabolaRenderer.positionCount = count;
        ParabolaRenderer.SetPositions(points);
    }
}
