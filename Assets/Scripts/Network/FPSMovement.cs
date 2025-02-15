using System;
using DefaultNamespace;
using Network.Interfaces;
using Unity.Netcode;
using UnityEngine;
using Utils;

namespace Network
{
    public class FPSMovement : PredictableBehaviour<FPSMovement.Inputs, FPSMovement.Results>
    {
        public struct Inputs : INetworkSerializable, IEquatable<Inputs>, IPrintable
        {
            private const float Tolerance = 0.000001f;
            
            public float forward;
            public float sides;
            public bool jump;
            public bool dash;
            
            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref forward);
                serializer.SerializeValue(ref sides);
                serializer.SerializeValue(ref jump);
                serializer.SerializeValue(ref dash);
            }

            public bool Equals(Inputs other)
            {
                return forward.Equals(other.forward) && sides.Equals(other.sides) && jump == other.jump &&
                       dash == other.dash;
            }

            public string Print()
            {
                return "Forward: " + forward + " Sides: " + sides;
            }
        }

        public struct Results : ITickable, INetworkSerializable, IEquatable<Results>, IPrintable
        {
            public int Tick { get => _tick; set => _tick = value; }
            private int _tick;
            
            public Vector3 position;
            public Vector3 velocity;
            
            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref _tick);
                serializer.SerializeValue(ref position);
                serializer.SerializeValue(ref velocity);
            }

            public bool Equals(Results other)
            {
                return other.Tick == Tick && position.Equals(other.position) && velocity.Equals(other.velocity);
            }

            public string Print()
            {
                return "Tick: " + Tick + " Position: " + position;
            }
        }
        
        private NetworkVariable<Quaternion> _currentRotation;
        
        [Header("Properties")]
        [SerializeField, Range(0, 30)] private float speed = 5f;
        [SerializeField, Range(0, 30)] private float jumpForce = 5f;
        [SerializeField, Range(0, 30)] private float dashForce = 5f;
        [SerializeField, Range(0, 30)] private float gravityMultiplier = 5f;
        [SerializeField, Range(0, 30)] private float maxFallSpeed = 5f;
        [SerializeField, Range(0, 30)] private float drag = 5f;
        [SerializeField, Range(0, 30)] private float airDrag = 5f;
        
        [SerializeField] private float positionTolerance = 0.001f;

        [SerializeField] private float dumbApplyLerpSpeed;
        [SerializeField] private float dashCooldown;
        [SerializeField] private float dashTime;

        private bool CanDash => _dashCooldownTimer <= 0;
        private bool IsDashing => _dashTimer > 0;
        
        [Header("Refs")]
        [SerializeField] private CharacterController controller;
        [SerializeField] private Transform orientation;
        [SerializeField] private CastCheck groundCheck;
        
        private Vector3 _localVelocity;
        private float _dashTimer;
        private float _dashCooldownTimer;
        
        protected override void Awake()
        {
            base.Awake();

            _currentRotation = new(readPerm: NetworkVariableReadPermission.Everyone,
                writePerm: NetworkVariableWritePermission.Owner);
        }
        
        public override void OnNetworkSpawn()
        {
            if(!IsOwner && !IsServer) Destroy(controller);

            if (!IsOwner) _currentRotation.OnValueChanged += HandleRotationUpdate;
            
            base.OnNetworkSpawn();
        }
        
        protected override bool NeedReconciliation(Results results)
        {
            Debug.Log("Need reconciliation ?");

            return (resultsBuffer[results.Tick % bufferSize].position - results.position).sqrMagnitude >
                   positionTolerance * positionTolerance;
        }

        protected override void Reconciliate(Results results)
        {
            Debug.LogError("Reconciliate");

            controller.Teleport(results.position);
            
            for (int i = (results.Tick + 1) % bufferSize; i < bufferSize + 1; i++)
            {
                if (i > lastSentTick % bufferSize)
                {
                    break;
                }
                
                if(i >= bufferSize) i = 0;
                
                resultsBuffer[i] = GetResults(inputsBuffer[i], i);
            }
        }

        protected override void DumbApplyResults(Results results)
        {
            transform.position = Vector3.Lerp(transform.position, results.position,
                dumbApplyLerpSpeed * (1f / NetworkManager.NetworkTickSystem.TickRate));
        }

        protected override Inputs GetInputs()
        {
            return new Inputs
            {
                forward = Input.GetAxisRaw("Vertical"),
                sides = Input.GetAxisRaw("Horizontal"),
                jump = Input.GetButton("Jump"),
                dash = Input.GetButtonDown("Dash")
            };
        }

        [ServerRpc]
        protected override void SendInputsServerRpc(byte[] serializedInputs, int tick)
        {
            base.SendInputsServerRpc(serializedInputs, tick);
        }

        protected override Results GetResults(Inputs inputs, int tick)
        {
            bool isTouchingGround = groundCheck.IsTouching;
            
            float tickDeltaTime = 1f / NetworkManager.NetworkTickSystem.TickRate;
            
            if(_dashTimer > 0) _dashTimer -= tickDeltaTime;
            if(_dashCooldownTimer > 0) _dashCooldownTimer -= tickDeltaTime;
            
            if (!isTouchingGround)
            {
                _localVelocity += Physics.gravity * gravityMultiplier * tickDeltaTime;

                _localVelocity.y = Mathf.Clamp(_localVelocity.y, -maxFallSpeed, 1000);
            }
            else
            {
                _localVelocity.y = -0.1f * tickDeltaTime;
            }
            
            if (inputs.jump && isTouchingGround)
            {
                _localVelocity.y = jumpForce;
            }
            
            var tr = orientation;
            var move = tr.forward * inputs.forward + tr.right * inputs.sides;
            
            move.Normalize();
            
            if (move.sqrMagnitude > 0 && !IsDashing)
            {
                move *= speed * tickDeltaTime;
                _localVelocity = new Vector3(move.x, _localVelocity.y, move.z);
                
                if (inputs.dash && CanDash)
                {
                    _localVelocity += move * dashForce;
                    _dashTimer = dashTime;
                    _dashCooldownTimer = dashCooldown;
                }
            }
            else
            {
                _localVelocity = new Vector3(
                    Mathf.Lerp(_localVelocity.x, 0, (isTouchingGround ? drag : airDrag) * tickDeltaTime),
                    _localVelocity.y,
                    Mathf.Lerp(_localVelocity.z, 0, (isTouchingGround ? drag : airDrag) * tickDeltaTime));
                
                if (inputs.dash && CanDash)
                {
                    _localVelocity += orientation.forward * dashForce;
                    _dashTimer = dashTime;
                    _dashCooldownTimer = dashCooldown;
                }
            }
            
            Debug.LogWarning("vel: " + _localVelocity);
            
            controller.Move(_localVelocity);
            
            return new Results
            {
                Tick = tick,
                position = transform.position,
                velocity = _localVelocity
            };
        }

        public void SetRotation(float yRotation)
        {
            if (!IsOwner) return;
            
            var t = orientation;

            //Apply instant for owner
            t.rotation = Quaternion.Euler(0, yRotation, 0);
            
            _currentRotation.Value = t.rotation;
        }
        
        private void HandleRotationUpdate(Quaternion previousValue, Quaternion newValue)
        {
            orientation.rotation = newValue;
        }
    }
}
