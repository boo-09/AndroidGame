using UnityEngine;

namespace FortStages
{
    /// <summary>A swept projectile: one impact, solid walls block it, no collision callbacks to double-hit.</summary>
    [DisallowMultipleComponent]
    public sealed class FortThrownProjectile : MonoBehaviour
    {
        public bool poisonous;
        [Min(0.1f)] public float speed = 8f;
        [Min(0.01f)] public float radius = 0.18f;
        [Min(0.1f)] public float lifetime = 4f;

        private Enemy owner;
        private PlayerController2 target;
        private GameManager manager;
        private Vector3 direction;
        private float age;
        private bool launched;
        private bool spent;

        public void Launch(Enemy source, PlayerController2 player, Vector3 throwDirection)
        {
            owner = source;
            target = player;
            manager = FindFirstObjectByType<GameManager>();
            direction = throwDirection.normalized;
            age = 0f;
            launched = true;
        }

        private void Update()
        {
            if (!launched || spent) return;
            if (target == null || target.IsDead()) { Destroy(gameObject); return; }
            if (Time.timeScale <= 0f || (manager != null && !manager.CanPause)) return;

            age += Time.deltaTime;
            if (age >= lifetime) { Destroy(gameObject); return; }

            // Handle a spawn already touching a player/wall as well as fast motion between frames.
            Collider[] overlaps = Physics.OverlapSphere(transform.position, radius, ~0,
                QueryTriggerInteraction.Ignore);
            Collider overlapPlayer = null;
            foreach (Collider overlap in overlaps)
            {
                if (Ignore(overlap)) continue;
                if (overlap.GetComponentInParent<PlayerController2>() == null)
                {
                    Impact(overlap);
                    return;
                }
                overlapPlayer = overlap;
            }
            if (overlapPlayer != null) { Impact(overlapPlayer); return; }

            float distance = speed * Time.deltaTime;
            RaycastHit[] hits = Physics.SphereCastAll(transform.position, radius, direction, distance,
                ~0, QueryTriggerInteraction.Ignore);
            Collider nearest = null;
            float nearestDistance = float.PositiveInfinity;
            foreach (RaycastHit hit in hits)
            {
                if (Ignore(hit.collider) || hit.distance >= nearestDistance) continue;
                nearest = hit.collider;
                nearestDistance = hit.distance;
            }
            if (nearest != null)
            {
                transform.position += direction * nearestDistance;
                Impact(nearest);
                return;
            }

            transform.position += direction * distance;
            transform.Rotate(Vector3.right, 360f * Time.deltaTime, Space.Self);
        }

        private bool Ignore(Collider collider)
        {
            return collider.transform.IsChildOf(transform) ||
                (owner != null && collider.transform.IsChildOf(owner.transform)) ||
                collider.GetComponentInParent<Enemy>() != null;
        }

        private void Impact(Collider collider)
        {
            if (spent) return;
            spent = true;
            PlayerController2 player = collider.GetComponentInParent<PlayerController2>();
            if (player != null && !player.IsDead())
            {
                if (poisonous) PlayerHealthEffects.GetOrAdd(player).ApplyPoison();
                else player.TakeDamage(1f);
            }
            Destroy(gameObject);
        }
    }
}
