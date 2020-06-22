using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(WalkAnimation))]
public class PlayerMovement : MonoBehaviour
{
    public float MaxSpeed = 1.0f;
    public float MaxAngularSpeed = 60.0f;

    Vector3 initPos;
    Quaternion initRot;

    // Start is called before the first frame update
    void Start()
    {
        initPos = transform.position;
        initRot = transform.rotation;
    }

    // Update is called once per frame
    void Update()
    {
        var characterController = GetComponent<CharacterController>();

        var forward = MaxSpeed * Input.GetAxis("Vertical");
        var turn = MaxAngularSpeed * Input.GetAxis("Horizontal");

        var velocity = new Vector3(0, 0, forward);
        var angularVelocity = new Vector3(0, turn, 0);
        transform.rotation *= Quaternion.Euler(angularVelocity * Time.deltaTime);
        characterController.SimpleMove(transform.rotation * velocity);

        var walkAnimation = GetComponent<WalkAnimation>();
        walkAnimation.SetAnimationParameters(forward, turn);
    }
}
