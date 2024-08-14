using UnityEngine;

namespace Mirror.Examples.RigidbodyBenchmark
{
    [RequireComponent(typeof(Rigidbody))]
    public class AddForce : NetworkBehaviour
    {
        public Rigidbody rigidbody3d;
        public float force = 500f;

        protected override void OnValidate()
        {
            base.OnValidate();
            rigidbody3d = GetComponent<Rigidbody>();
        }

        private void Update()
        {
            // do we have authority over this?
            if (!rigidbody3d.isKinematic)
            {
                if (Input.GetKeyDown(KeyCode.Space))
                    rigidbody3d.AddForce(Vector3.up * force);
            }
        }
    }
}
