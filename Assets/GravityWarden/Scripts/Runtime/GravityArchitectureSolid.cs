using UnityEngine;

namespace GravityRepulsion
{
    /// <summary>One fixed imported building or street fixture, preserving the gaps between models.</summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class GravityArchitectureSolid : MonoBehaviour
    {
        public bool isBuilding;
        private void Awake()
        {
            var shape = GetComponent<BoxCollider>();
            shape.enabled = true;
            shape.isTrigger = false;
            shape.contactOffset = .005f;
            shape.sharedMaterial = GravityTarget.RestingPropMaterial;
        }
    }
}
