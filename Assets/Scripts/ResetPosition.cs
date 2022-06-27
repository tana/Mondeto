using UnityEngine;

namespace Mondeto
{

public class ResetPosition : MonoBehaviour
{
    // Update is called once per frame
    void Update()
    {
        if (Input.GetKey(KeyCode.Z))
		{
			// CharacterController overwrites transform.position.
			// Therefore we have to disable before resetting position.
			//  https://forum.unity.com/threads/does-transform-position-work-on-a-charactercontroller.36149/
			GetComponent<CharacterController>().enabled = false;
			transform.position = Vector3.zero;
			GetComponent<CharacterController>().enabled = true;
			Debug.Log("Position reset");
		}
    }
}

}   // end namespace