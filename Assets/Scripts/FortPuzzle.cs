using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace FortStages
{
    /// <summary>Entry-operated rune or switch puzzle that releases one existing key.</summary>
    public sealed class FortPuzzle : MonoBehaviour
    {
        public enum Mode { OrderedRunes, TwoSwitches }

        public Mode mode;
        public int[] sequence = { 0, 1, 2 };
        public GameObject rewardKey;
        public GameObject gate;
        public TMP_Text progressText;
        public Renderer[] indicators;

        public bool Solved { get; private set; }
        public bool LastInputWrong { get; private set; }
        public int Progress { get; private set; }
        public int RequiredCount => mode == Mode.TwoSwitches ? 2 : sequence == null ? 0 : sequence.Length;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private readonly HashSet<int> activatedSwitches = new HashSet<int>();
        private MaterialPropertyBlock properties;
        private Color[] idleColors;

        private void Awake()
        {
            properties = new MaterialPropertyBlock();
            idleColors = new Color[indicators == null ? 0 : indicators.Length];
            for (int i = 0; i < idleColors.Length; i++)
            {
                Material material = indicators[i] == null ? null : indicators[i].sharedMaterial;
                idleColors[i] = material != null && material.HasProperty(BaseColorId) ? material.GetColor(BaseColorId) :
                    material != null && material.HasProperty(ColorId) ? material.GetColor(ColorId) : Color.white;
            }
            if (rewardKey != null)
                rewardKey.SetActive(false);
            if (gate != null)
                gate.SetActive(true);
            UpdateFeedback(false);
        }

        public void Activate(int index)
        {
            if (Solved || index < 0 || RequiredCount == 0)
                return;

            bool wrong = false;
            if (mode == Mode.OrderedRunes)
            {
                if (sequence[Progress] == index)
                    Progress++;
                else
                {
                    Progress = 0;
                    wrong = true;
                }
            }
            else
            {
                // Indicators correspond to the actual switches when supplied.
                if (indicators != null && indicators.Length > 0 && index >= indicators.Length)
                    return;
                if (!activatedSwitches.Add(index))
                    return;
                Progress = activatedSwitches.Count;
            }

            LastInputWrong = wrong;
            if (Progress >= RequiredCount)
            {
                Solved = true;
                if (rewardKey != null)
                    rewardKey.SetActive(true);
                if (gate != null)
                    gate.SetActive(false);
            }

            UpdateFeedback(wrong);
        }

        private void UpdateFeedback(bool wrong)
        {
            if (properties == null)
                properties = new MaterialPropertyBlock();

            if (progressText != null)
            {
                string label = mode == Mode.OrderedRunes ? "紋章" : "スイッチ";
                progressText.text = Solved ? "謎解き成功！ 鍵を手に入れよう" :
                    wrong ? "順序が違います。最初からやり直そう" :
                    $"{label}: {Progress}/{RequiredCount}";
            }

            if (indicators == null)
                return;

            for (int i = 0; i < indicators.Length; i++)
            {
                Renderer indicator = indicators[i];
                if (indicator == null)
                    continue;

                bool selected = Solved || activatedSwitches.Contains(i);
                if (mode == Mode.OrderedRunes && sequence != null)
                {
                    for (int step = 0; step < Progress; step++)
                        selected |= sequence[step] == i;
                }

                Color idleColor = idleColors != null && i < idleColors.Length ? idleColors[i] : Color.white;
                Color color = Solved ? new Color(1f, 0.8f, 0.24f) :
                    selected ? Color.Lerp(idleColor, Color.white, 0.35f) : idleColor;
                properties.Clear();
                indicator.GetPropertyBlock(properties);
                properties.SetColor(BaseColorId, color);
                properties.SetColor(ColorId, color);
                indicator.SetPropertyBlock(properties);
            }
        }
    }
}
