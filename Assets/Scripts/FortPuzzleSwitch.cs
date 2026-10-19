using System.Collections.Generic;
using UnityEngine;

namespace FortStages
{
    /// <summary>Activates on player entry, including players with several colliders.</summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class FortPuzzleSwitch : MonoBehaviour
    {
        public FortPuzzle puzzle;
        public int index;

        private readonly Dictionary<PlayerController2, int> occupants = new Dictionary<PlayerController2, int>();

        private void Awake()
        {
            GetComponent<BoxCollider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            PlayerController2 player = other.GetComponentInParent<PlayerController2>();
            if (player == null)
                return;

            occupants.TryGetValue(player, out int colliderCount);
            occupants[player] = colliderCount + 1;
            if (colliderCount == 0 && puzzle != null && Time.timeScale > 0f && !player.IsDead())
                puzzle.Activate(index);
        }

        private void OnTriggerExit(Collider other)
        {
            PlayerController2 player = other.GetComponentInParent<PlayerController2>();
            if (player == null || !occupants.TryGetValue(player, out int colliderCount))
                return;

            if (colliderCount <= 1)
                occupants.Remove(player);
            else
                occupants[player] = colliderCount - 1;
        }

        private void OnDisable()
        {
            occupants.Clear();
        }
    }
}
