using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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

    public string ButtonName = "Fire1";

    public LineRenderer parabolaRenderer;

    enum TeleportState
    {
        Idle, Aiming, Moving
    }

    TeleportState state;
    bool teleportable = false;
    Vector3 target;

    // Start is called before the first frame update
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetButtonDown(ButtonName)) ButtonDown();
        if (Input.GetButtonUp(ButtonName)) ButtonUp();

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
        parabolaRenderer.enabled = true;
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
        parabolaRenderer.enabled = false;
    }

    void CastParabola(Vector3 launchDir) // launchDir has to be normalized
    {
        Vector3 localDir = Quaternion.Inverse(transform.rotation) * launchDir;
        //float elevation = Mathf.Asin(localDir.y);

        // x = vx*t, y = vy*t - g*t^2
        float vx = InitialVelocity * Mathf.Sqrt(1 - localDir.y * localDir.y);//Mathf.Cos(elevation);
        float vy = InitialVelocity * localDir.y;//Mathf.Sin(elevation);
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

        parabolaRenderer.positionCount = count;
        parabolaRenderer.SetPositions(points);
    }
}
