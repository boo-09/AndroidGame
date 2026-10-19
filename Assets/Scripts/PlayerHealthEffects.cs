using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Poison and gradual healing use half-HP steps during active gameplay.
/// Each application keeps its own tick count; scene changes discard pending effects.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerController2))]
public sealed class PlayerHealthEffects : MonoBehaviour
{
    private const float TickInterval = 1f;
    private const float TickAmount = 0.5f;

    private sealed class Effect
    {
        public bool heals;
        public int remainingTicks;
        public float elapsed;
    }

    private readonly List<Effect> effects = new List<Effect>();
    private PlayerController2 player;
    private GameManager gameManager;

    public static PlayerHealthEffects GetOrAdd(PlayerController2 target)
    {
        if (target == null)
        {
            return null;
        }

        PlayerHealthEffects component = target.GetComponent<PlayerHealthEffects>();
        return component != null
            ? component
            : target.gameObject.AddComponent<PlayerHealthEffects>();
    }

    private void Awake()
    {
        player = GetComponent<PlayerController2>();
        foreach (GameManager candidate in FindObjectsByType<GameManager>(FindObjectsSortMode.None))
        {
            if (candidate.gameObject.scene == gameObject.scene)
            {
                gameManager = candidate;
                break;
            }
        }
    }

    public void BeginGradualHeal()
    {
        AddEffect(true, 5);
    }

    public void ApplyPoison()
    {
        AddEffect(false, 4);
    }

    private void AddEffect(bool heals, int ticks)
    {
        if (!isActiveAndEnabled || player == null || !player.isActiveAndEnabled || player.IsDead())
        {
            return;
        }

        effects.Add(new Effect { heals = heals, remainingTicks = ticks });
    }

    private void Update()
    {
        if (player == null || !player.isActiveAndEnabled || player.IsDead())
        {
            effects.Clear();
            return;
        }

        // CanPause is true only after the countdown and before clear/game over.
        // Scaled delta time also freezes effects while skill selection is open.
        if (Time.timeScale <= 0f || (gameManager != null && !gameManager.CanPause))
        {
            return;
        }

        for (int i = 0; i < effects.Count;)
        {
            Effect effect = effects[i];
            effect.elapsed += Time.deltaTime;

            while (effect.elapsed >= TickInterval && effect.remainingTicks > 0)
            {
                effect.elapsed -= TickInterval;
                effect.remainingTicks--;

                if (effect.heals)
                {
                    player.Heal(TickAmount);
                }
                else
                {
                    // A lethal tick ends the old effects even if a revive skill fires.
                    bool lethal = player.hp <= TickAmount;
                    player.TakeDamage(TickAmount);
                    if (lethal || player.IsDead())
                    {
                        effects.Clear();
                        return;
                    }
                }
            }

            if (effect.remainingTicks == 0)
            {
                effects.RemoveAt(i);
            }
            else
            {
                i++;
            }
        }
    }

    private void OnDisable()
    {
        effects.Clear();
    }
}
