using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Tooltip("An array of transforms respresenting camera positions")]
    [SerializeField] Transform[] povs;
    [Tooltip("The speed at which the camera follows the plane")]
    [SerializeField] float speed;

    private int index = 0;
    private Vector3 target;
    private Quaternion targetRotation;

    private void Update()
    {
        // Cycle through POVs
        if (Input.GetButtonDown("CameraCycle"))
        {
            index++;
        }
        if (index >= povs.Length)
        {
            index = 0;
        }

        // Set our target to the relevant POV
        target = povs[index].position;
        targetRotation = povs[index].rotation;
    }

    private void FixedUpdate()
    {
        // Move camera to desired position/orientation. In fixed update to avoid camera jittering.
        transform.position = Vector3.MoveTowards(transform.position, target, Time.deltaTime * speed);
        transform.forward = povs[index].forward;
        transform.rotation = targetRotation;
    }
}
