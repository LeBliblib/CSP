using System;
using System.Runtime.InteropServices;
using Network.Interfaces;
using Unity.Netcode;
using UnityEngine;

namespace Network
{
    public abstract class PredictableBehaviour<TI, TR> : NetworkBehaviour 
        where TI : struct, INetworkSerializable, IEquatable<TI>, IPrintable
        where TR : struct, ITickable, INetworkSerializable, IEquatable<TR>, IPrintable
    {
        private NetworkVariable<TR> _currentServerResults;
        
        [SerializeField, Range(512, 2048)] protected int bufferSize = 1024;

        private TI _currentInputs;
        protected int lastSentTick;
        
        protected TI[] inputsBuffer;
        protected TR[] resultsBuffer;

        public event Action OnNetworkTickProcessed;
        
        protected virtual void Awake()
        {
            _currentServerResults = new();
        }
        
        public override void OnNetworkSpawn()
        {
            if(IsServer) return;
            
            _currentServerResults.OnValueChanged += OnServerResultsChanged;
            
            if(!IsOwner) return;
            
            inputsBuffer = new TI[bufferSize];
            resultsBuffer = new TR[bufferSize];
            
            NetworkManager.NetworkTickSystem.Tick += OnNetworkTick;
        }

        public override void OnNetworkDespawn()
        {
            if(!IsServer)
                _currentServerResults.OnValueChanged -= OnServerResultsChanged;
            
            if (IsOwner)
                NetworkManager.NetworkTickSystem.Tick -= OnNetworkTick;
        }
        
        protected virtual void Update()
        {
            if (IsServer) return;
            
            _currentInputs = GetInputs();
        }
        
        private void OnNetworkTick()
        {
            ProcessNetworkTick(NetworkManager.LocalTime.Tick);
        }
        
        protected virtual void ProcessNetworkTick(int currentTick)
        {
            var bufferIndex = currentTick % bufferSize;
            
            inputsBuffer[bufferIndex] = _currentInputs;
            resultsBuffer[bufferIndex] = GetResults(_currentInputs, currentTick);

            lastSentTick = currentTick;
            
            SendInputsServerRpc(GetBytes(_currentInputs), currentTick);
            OnNetworkTickProcessed?.Invoke();
        }
        
        private void OnServerResultsChanged(TR previousValue, TR newValue)
        {
            if (IsServer) return;

            if (!IsOwner)
            {
                DumbApplyResults(newValue);
                return;
            }
            
            if (NeedReconciliation(newValue)) 
                Reconciliate(newValue);
        }
        
        protected abstract bool NeedReconciliation(TR results);

        protected abstract void Reconciliate(TR results);
        
        /// <summary>
        /// Used to apply results without any checks on clients that are not the owner
        /// </summary>
        /// <param name="results"></param>
        protected abstract void DumbApplyResults(TR results);
        
        protected abstract TI GetInputs();
        protected abstract TR GetResults(TI inputs, int tick);

        [ServerRpc]
        protected virtual void SendInputsServerRpc(byte[] serializedInputs, int tick)
        {
            var inputs = FromBytes(serializedInputs);
            
            Debug.Log("GetInputs: " + inputs.Print());

            var tr = GetResults(inputs, tick);
            
            Debug.Log("GetResults: " + tr.Print());
            
            _currentServerResults.Value = tr;
        }
        
        private byte[] GetBytes(TI str) {
            int size = Marshal.SizeOf(str);
            byte[] arr = new byte[size];

            IntPtr ptr = IntPtr.Zero;
            try
            {
                ptr = Marshal.AllocHGlobal(size);
                Marshal.StructureToPtr(str, ptr, true);
                Marshal.Copy(ptr, arr, 0, size);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
            return arr;
        }
        
        private TI FromBytes(byte[] arr)
        {
            TI str = new TI();

            int size = Marshal.SizeOf(str);
            IntPtr ptr = IntPtr.Zero;
            try
            {
                ptr = Marshal.AllocHGlobal(size);

                Marshal.Copy(arr, 0, ptr, size);

                str = (TI)Marshal.PtrToStructure(ptr, str.GetType());
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
            return str;
        }
    }

    public interface IPrintable
    {
        public string Print();
    }
}