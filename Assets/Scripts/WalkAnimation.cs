using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Controls animation of an avatar.
// For UnityChanLocomotions animation controller of "Unity-Chan" animation data.
[RequireComponent(typeof(Animator))]
public class WalkAnimation : MonoBehaviour
{
    public float AnimForwardCoeff = 0.3f;
    public float AnimTurnCoeff = 0.003f;

    Animator animator;

    // Start is called before the first frame update
    void Start()
    {
        animator = GetComponent<Animator>();
    }

    public void SetAnimationParameters(float forward, float sideways, float turn)
    {
        // TODO: side walking animation
        float speedSign = (forward >= 0.0f) ? 1 : -1;   // Pure side walking is currently considered as forward
        float speed = Mathf.Sqrt(forward * forward + sideways * sideways) * speedSign;
        if (Mathf.Abs(speed) > 0.01f)
        {
            animator.SetFloat("Speed", AnimForwardCoeff * speed);
        }
        else
        {
            // FIXME: Currently using forward walking animation for pure turn
            animator.SetFloat("Speed", AnimTurnCoeff * Mathf.Abs(turn));
        }
    }
}
