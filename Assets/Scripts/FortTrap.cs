using System.Collections.Generic;
using UnityEngine;

namespace FortStages
{
    /// <summary>A repeating floor trap with a warning and one hit per player per activation.</summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class FortTrap : MonoBehaviour
    {
        public Renderer warningRenderer;
        [Min(0f)] public float safeTime = 3f;
        [Min(0f)] public float warningTime = 1.3f;
        [Min(0f)] public float activeTime = 0.9f;
        public float phaseOffset;
        [Min(0f)] public float damage = 1f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly Color SafeColor = new Color(0.26f, 0.32f, 0.34f);
        private static readonly Color WarningColor = new Color(1f, 0.65f, 0.08f);
        private static readonly Color ActiveColor = new Color(1f, 0.12f, 0.08f);

        private readonly HashSet<PlayerController2> damagedPlayers = new HashSet<PlayerController2>();
        private BoxCollider trigger;
        private GameManager gameManager;
        private MaterialPropertyBlock properties;
        private float elapsed;
        private int activation = int.MinValue;
        private int visualState = -1;

        private void Awake()
        {
            trigger = GetComponent<BoxCollider>();
            trigger.isTrigger = true;
            gameManager = FindFirstObjectByType<GameManager>();
            properties = new MaterialPropertyBlock();
            SetVisual(0);
        }

        private void Update()
        {
            // CanPause also excludes the opening countdown and completed games.
            if (Time.timeScale <= 0f || gameManager == null || !gameManager.CanPause)
                return;

            elapsed += Time.deltaTime;
            float safe = Mathf.Max(0f, safeTime);
            float warning = Mathf.Max(0f, warningTime);
            float active = Mathf.Max(0f, activeTime);
            float duration = safe + warning + active;
            if (duration <= 0f)
            {
                SetVisual(0);
                return;
            }

            float phaseTime = elapsed + phaseOffset;
            float phase = Mathf.Repeat(phaseTime, duration);
            bool isActive = active > 0f && phase >= safe + warning;
            SetVisual(isActive ? 2 : phase >= safe ? 1 : 0);
            if (!isActive)
                return;

            int currentActivation = Mathf.FloorToInt(phaseTime / duration);
            if (currentActivation != activation)
            {
                activation = currentActivation;
                damagedPlayers.Clear();
            }

            if (!trigger.enabled)
                return;

            Vector3 scale = transform.lossyScale;
            Vector3 halfExtents = Vector3.Scale(trigger.size * 0.5f,
                new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            Collider[] overlaps = Physics.OverlapBox(transform.TransformPoint(trigger.center),
                halfExtents, transform.rotation, ~0, QueryTriggerInteraction.Collide);

            foreach (Collider overlap in overlaps)
            {
                PlayerController2 player = overlap.GetComponentInParent<PlayerController2>();
                if (player == null || player.IsDead() || !damagedPlayers.Add(player))
                    continue;

                PlayerSkills skills = player.GetComponent<PlayerSkills>();
                float hitDamage = Mathf.Max(0f, damage);
                if (skills != null)
                    hitDamage = skills.GetEnemyAttackDamage(hitDamage);

                if (hitDamage > 0f && Time.timeScale > 0f && gameManager.CanPause)
                    player.TakeDamage(hitDamage);
            }
        }

        private void SetVisual(int state)
        {
            if (warningRenderer == null || visualState == state)
                return;

            visualState = state;
            Color color = state == 2 ? ActiveColor : state == 1 ? WarningColor : SafeColor;
            properties.Clear();
            warningRenderer.GetPropertyBlock(properties);
            properties.SetColor(BaseColorId, color);
            properties.SetColor(ColorId, color);
            properties.SetColor(EmissionColorId, state == 0 ? Color.black : color * 0.25f);
            warningRenderer.SetPropertyBlock(properties);
        }
    }
}
