using System;
using UnityEngine;

namespace DefaultNamespace
{
    public class CastCheck : MonoBehaviour
    {
        [SerializeField] private Transform origin;

        [SerializeField] private LayerMask mask;
        [SerializeField] private float size = 1f;

        public bool IsTouching => Physics.CheckSphere(origin.position, size, mask);

        private void OnDrawGizmos()
        {
            Gizmos.color = IsTouching ? Color.red : Color.green;
            Gizmos.DrawWireSphere(origin.position, size);
        }
    }
}