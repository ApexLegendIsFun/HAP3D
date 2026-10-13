using UnityEngine;

namespace HAP3D
{
    /// <summary>One actor; visible blocks are independent of its smooth physics capsule.</summary>
    public sealed class HAPActor : MonoBehaviour
    {
        public const float GravityAcceleration = 22f;
        public const float MoveSpeed = 4f;
        public Rigidbody Body { get; private set; }
        public CapsuleCollider Capsule { get; private set; }
        public int GravitySign { get; private set; } = -1;
        public bool Grounded { get; private set; }
        public bool IsYang { get; private set; }
        private Transform visual;
        private Transform selection;
        private Transform gravityArrow;
        private float visualTime;
        private Quaternion targetRotation = Quaternion.identity;
        private readonly RaycastHit[] groundHits = new RaycastHit[12];

        public void Initialize(bool yang, Material shell, Material contrast, Material accent)
        {
            IsYang = yang;
            Capsule = gameObject.AddComponent<CapsuleCollider>();
            Capsule.radius = .4f;
            Capsule.height = 1.6f;
            Body = gameObject.AddComponent<Rigidbody>();
            Body.useGravity = false;
            Body.mass = 1f;
            Body.linearDamping = 0f;
            Body.constraints = RigidbodyConstraints.FreezeRotation;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.Continuous;
            visual = new GameObject("Block character").transform;
            visual.SetParent(transform, false);
            HAPPrototype.VisualBox("Body", visual, new Vector3(0, -.12f, 0), new Vector3(.62f, .78f, .58f), shell);
            HAPPrototype.VisualBox("Head", visual, new Vector3(0, .47f, 0), new Vector3(.72f, .55f, .68f), shell);
            HAPPrototype.VisualBox("Foot L", visual, new Vector3(-.18f, -.66f, 0), new Vector3(.23f, .25f, .48f), contrast);
            HAPPrototype.VisualBox("Foot R", visual, new Vector3(.18f, -.66f, 0), new Vector3(.23f, .25f, .48f), contrast);
            HAPPrototype.VisualBox("Eye L", visual, new Vector3(-.16f, .49f, -.347f), new Vector3(.07f, .09f, .025f), contrast);
            HAPPrototype.VisualBox("Eye R", visual, new Vector3(.16f, .49f, -.347f), new Vector3(.07f, .09f, .025f), contrast);
            HAPPrototype.VisualBox("Heart", visual, new Vector3(0, -.02f, -.3f), new Vector3(.14f, .14f, .035f), contrast);
            selection = new GameObject("Selection corners").transform;
            selection.SetParent(transform, false);
            foreach (float x in new[] { -.52f, .52f })
            foreach (float z in new[] { -.52f, .52f })
                HAPPrototype.VisualBox("Corner", selection, new Vector3(x, 0, z), new Vector3(.16f, .045f, .16f), accent);
            gravityArrow = HAPPrototype.VisualBox("Gravity diamond", transform, new Vector3(0, 1.08f, 0), new Vector3(.16f, .16f, .16f), accent).transform;
            gravityArrow.localRotation = Quaternion.Euler(0, 0, 45);
        }

        public void ResetActor(Vector3 position)
        {
            GravitySign = -1; Grounded = false; visualTime = 0;
            Body.isKinematic = false;
            // Rigidbody interpolation may not update the visible Transform before a physics tick.
            // Reset both poses so title freezes and freshly rebuilt stages never render at origin.
            transform.SetPositionAndRotation(position, Quaternion.identity);
            Body.position = position;
            Body.rotation = Quaternion.identity;
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
            targetRotation = Quaternion.identity;
            visual.localRotation = targetRotation;
            visual.localPosition = Vector3.zero;
        }

        public void RefreshGrounded()
        {
            var up = Vector3.up * -GravitySign;
            var foot = Body.position - up * (Capsule.height * .5f - .015f);
            var count = Physics.RaycastNonAlloc(foot + up * .035f, -up, groundHits, .18f, ~0, QueryTriggerInteraction.Ignore);
            Grounded = false;
            for (int i = 0; i < count; i++)
            {
                var hit = groundHits[i];
                if (hit.collider == Capsule || hit.collider.GetComponentInParent<HAPActor>() != null) continue;
                if (Vector3.Dot(hit.normal, up) < .5f) continue;
                Grounded = true;
                break;
            }
        }

        public void Simulate(Vector2 input, bool frozen)
        {
            if (frozen)
            {
                Body.linearVelocity = Vector3.zero;
                return;
            }
            var velocity = Body.linearVelocity;
            velocity.y += GravitySign * GravityAcceleration * Time.fixedDeltaTime;
            velocity.x = Grounded ? input.x * MoveSpeed : 0f;
            velocity.z = Grounded ? input.y * MoveSpeed : 0f;
            Body.linearVelocity = velocity;
        }

        public bool TryFlip()
        {
            RefreshGrounded();
            if (!Grounded) return false;
            Body.linearVelocity = Vector3.zero;
            GravitySign *= -1;
            Grounded = false;
            targetRotation = GravitySign == -1 ? Quaternion.identity : Quaternion.Euler(0, 0, 180);
            return true;
        }

        public void Animate(bool selected, bool frozen, float dt)
        {
            visualTime += dt;
            visual.localRotation = Quaternion.Slerp(visual.localRotation, targetRotation, 1f - Mathf.Exp(-12f * dt));
            float speed = new Vector2(Body.linearVelocity.x, Body.linearVelocity.z).magnitude;
            visual.localPosition = Vector3.up * (frozen || !Grounded ? 0 : Mathf.Sin(visualTime * 17) * .025f * Mathf.Clamp01(speed));
            selection.gameObject.SetActive(selected);
            selection.localPosition = new Vector3(0, GravitySign * .765f, 0);
            gravityArrow.localPosition = new Vector3(0, -GravitySign * (1.04f + .035f * Mathf.Sin(visualTime * 3)), 0);
            gravityArrow.gameObject.SetActive(selected);
        }
    }
}
