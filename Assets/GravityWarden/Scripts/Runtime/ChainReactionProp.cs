using UnityEngine;

namespace GravityRepulsion
{
    [RequireComponent(typeof(GravityTarget))]
    public sealed class ChainReactionProp : MonoBehaviour
    {
        public float radius = 3.2f;
        public float force = 13f;
        public int damage = 1;
        public Color effectColor = new(1f, .46f, .16f);

        private bool detonated;

        private void Awake()
        {
            GetComponent<GravityTarget>().Destroyed += _ => Detonate();
        }

        private void OnCollisionEnter(Collision collision)
        {
            var target = GetComponent<GravityTarget>();
            if (target.IsAlive && target.ThrownUntil > Time.time && collision.relativeVelocity.sqrMagnitude > 25f)
            {
                target.DestroyTarget("环境连锁");
            }
        }

        public void Detonate()
        {
            if (detonated) return;
            detonated = true;
            GravityFeedback.Burst(transform.position, effectColor, 32, 2.1f);
            GravityAudio.Play(GravitySound.EnvironmentImpact, transform.position, 1f, .75f);
            foreach (var hit in Physics.OverlapSphere(transform.position, radius, ~0, QueryTriggerInteraction.Collide))
            {
                var target = hit.GetComponentInParent<GravityTarget>();
                if (target != null && target.IsAlive && target.gameObject != gameObject && target.kind == GravityTargetKind.Robot)
                {
                    target.Damage(damage, "环境连锁");
                }
                var body = hit.attachedRigidbody;
                if (body != null && body.gameObject != gameObject) body.AddExplosionForce(force, transform.position, radius, 1.2f, ForceMode.VelocityChange);
            }
        }
    }
}
