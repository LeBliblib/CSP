using System;
using UnityEngine;

public class Test : MonoBehaviour
{
    [SerializeField] private CharacterController controller;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.A))
        {
            Debug.Log("Before > " + transform.position);
            controller.Move(Vector3.forward * 1);
            Debug.Log("After > " + transform.position);
        }
    }
}