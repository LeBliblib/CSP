using System;
using Network.Interfaces;
using Unity.Netcode;
using UnityEngine;

namespace Network
{
    public class FPSCamera : NetworkBehaviour
    {
        [SerializeField] private float sensitivity = 1f;
        [SerializeField] private float clampRot = 90f;
        
        [SerializeField] private Transform cam;
        [SerializeField] private Transform camParent;

        [SerializeField] private FPSMovement body;
        [SerializeField] private Transform bodyHead;
       
        private float rotX, rotY;

        private void Awake()
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;

            if(body)
                body.OnNetworkTickProcessed += SyncPositionToBody;
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            
            if(body)
                body.OnNetworkTickProcessed -= SyncPositionToBody;
        }
        
        public override void OnNetworkSpawn()
        {
            if(!IsOwner && !IsServer) Destroy(gameObject);
            
            base.OnNetworkSpawn();
        }

        private void Update()
        {
            Vector2 inputs = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
            
            rotY += inputs.x * sensitivity;
            rotX += -inputs.y * sensitivity;

            rotX = Mathf.Clamp(rotX, -clampRot, clampRot);
        }

        private void SyncPositionToBody()
        {
            camParent.rotation = Quaternion.Euler(0, rotY, 0);
            cam.localRotation = Quaternion.Euler(rotX, 0, 0);
            
            body.SetRotation(rotY);
            
            if (bodyHead)
                transform.position = bodyHead.position;
        }
    }
}