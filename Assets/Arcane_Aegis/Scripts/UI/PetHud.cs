using UnityEngine;
using UnityEngine.UI;

namespace Arcane_Aegis.UI
{
    /// <summary>HUD frame for the OWNER's active pet: name, level, and an XP bar. Driven by <see cref="PetState"/>
    /// (fed by S2C_PetState) — no per-frame polling. Drop this on a HUD panel and assign the refs; the whole
    /// <see cref="root"/> hides when no pet is out. Build the panel by hand (UI is yours); this just fills it.</summary>
    public sealed class PetHud : MonoBehaviour
    {
        [Tooltip("O painel do pet inteiro — escondido quando nenhum pet está fora.")]
        [SerializeField] private GameObject root;
        [Tooltip("Nome do pet.")]
        [SerializeField] private TMPro.TMP_Text nameLabel;
        [Tooltip("Nível do pet (ex.: 'Nv 5').")]
        [SerializeField] private TMPro.TMP_Text levelLabel;
        [Tooltip("Barra de XP: Image type=Filled, Horizontal (fillAmount = Xp / XpToNext).")]
        [SerializeField] private Image xpFill;
        [Tooltip("Opcional: texto da XP (ex.: '120 / 316').")]
        [SerializeField] private TMPro.TMP_Text xpLabel;
        [Tooltip("Opcional: objeto piscado 1x no level-up (efeito/partícula). Reaproveite seu próprio toast se preferir.")]
        [SerializeField] private GameObject levelUpFx;

        private void OnEnable()
        {
            PetState.OnChanged += Refresh;
            PetState.OnLevelUp += OnLevelUp;
            Refresh();
        }

        private void OnDisable()
        {
            PetState.OnChanged -= Refresh;
            PetState.OnLevelUp -= OnLevelUp;
        }

        private void Refresh()
        {
            if (root != null && root.activeSelf != PetState.HasPet) root.SetActive(PetState.HasPet);
            if (!PetState.HasPet) return;
            if (nameLabel != null) nameLabel.text = PetState.Name;
            if (levelLabel != null) levelLabel.text = $"Nv {PetState.Level}";
            if (xpFill != null) xpFill.fillAmount = PetState.XpFill;
            if (xpLabel != null) xpLabel.text = $"{PetState.Xp} / {PetState.XpToNext}";
        }

        private void OnLevelUp(ushort level)
        {
            if (levelUpFx != null) { levelUpFx.SetActive(false); levelUpFx.SetActive(true); }
            if (Toast.Instance != null) Toast.Instance.Show($"{PetState.Name} subiu para o nível {level}!");
        }
    }
}
