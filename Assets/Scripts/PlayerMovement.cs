using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float speed = 1.0f;
    public float angularSpeed = 60.0f;

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

        var velocity = speed * (new Vector3(0, 0, Input.GetAxis("Vertical")));
        var angularVelocity = angularSpeed * (new Vector3(0, Input.GetAxis("Horizontal"), 0));
        transform.rotation *= Quaternion.Euler(angularVelocity * Time.deltaTime);
        characterController.SimpleMove(transform.rotation * velocity);
    }
}
