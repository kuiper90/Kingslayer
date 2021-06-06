using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MoveCamera : MonoBehaviour
{
    public float Speed = 0.05F;

    void Update()
    {
        float xAxisValue = Input.GetAxis("Vertical") * Speed;
        float zAxisValue = Input.GetAxis("Horizontal") * Speed * -1;
        float yValue = 0.0f;

        if (Input.GetKey(KeyCode.Q))
        {
            yValue = Speed;
        }
        if (Input.GetKey(KeyCode.E))
        {
            yValue = -Speed;
        }

        transform.position = new Vector3(transform.position.x + xAxisValue, transform.position.y + yValue, transform.position.z + zAxisValue);
    }
}
