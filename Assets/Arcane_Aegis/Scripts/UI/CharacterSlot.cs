using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ArcaneShared.Models;

namespace Arcane_Aegis.UI
{
    /// <summary>
    /// Put this on each fixed character-slot card. The <see cref="CharacterLobby"/> fills it with a character
    /// (shows <see cref="filledView"/>) or marks it empty (shows <see cref="emptyView"/> = the "+").
    /// IMPORTANT: keep the Button on the card ROOT (always active/clickable). Put the character visuals in
    /// filledView and the "+" in emptyView — SEPARATE child objects, never the card root (or it hides itself).
    /// </summary>
    public class CharacterSlot : MonoBehaviour
    {
        [SerializeField] private Button button;         // on the card root (stays active so empty slots are clickable too)
        [SerializeField] private GameObject filledView; // character visuals (name/level/portrait) — on when occupied
        [SerializeField] private GameObject emptyView;  // the "+" — on when empty
        [SerializeField] private TMP_Text nameLabel;    // inside filledView
        [SerializeField] private TMP_Text infoLabel;    // inside filledView (e.g. "Nv 40")
        [SerializeField] private GameObject highlight;  // a SEPARATE selection frame — optional (NOT the card root)

        public Button Button => button;

        public void SetCharacter(CharacterSummary c)
        {
            if (filledView != null) filledView.SetActive(true);
            if (emptyView != null) emptyView.SetActive(false);
            if (nameLabel != null) nameLabel.text = c.Name;
            if (infoLabel != null) infoLabel.text = $"Nv {c.Level}";
        }

        public void SetEmpty()
        {
            if (filledView != null) filledView.SetActive(false);
            if (emptyView != null) emptyView.SetActive(true);
            if (nameLabel != null) nameLabel.text = string.Empty;
            if (infoLabel != null) infoLabel.text = string.Empty;
        }

        public void SetSelected(bool on) { if (highlight != null) highlight.SetActive(on); }

        private void Reset() => button = GetComponent<Button>(); // auto-grab the card's Button when added
    }
}
