using UnityEngine;

namespace Utils
{
    public static class CharacterControllerExtensions
    {
        // ReSharper disable once Unity.InefficientPropertyAccess
        public static void Teleport(this CharacterController controller, Vector3 position)
        {
            controller.enabled = false;
            controller.transform.position = position;
            controller.enabled = true;
        }
    }
}