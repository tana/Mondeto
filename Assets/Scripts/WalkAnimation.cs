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

    public void SetAnimationParameters(float forward, float turn)
    {
        if (Mathf.Abs(forward) > 0.01f)
        {
            animator.SetFloat("Speed", AnimForwardCoeff * forward);
        }
        else
        {
            // FIXME: Currently using forward walking animation for pure turn
            animator.SetFloat("Speed", AnimTurnCoeff * Mathf.Abs(turn));
        }
    }
}
