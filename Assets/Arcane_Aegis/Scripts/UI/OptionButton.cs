using UnityEngine;
using UnityEngine.UI;

namespace Arcane_Aegis.UI
{
    /// <summary>
    /// Put this on a FIXED class/race/gender button (with its portrait/icon art). The <see cref="CharacterLobby"/>
    /// auto-wires its click (→ selects that id) and highlights the selected one in its group. Set <see cref="kind"/>
    /// + <see cref="id"/> (must match content, e.g. "warrior", "beast", "male"). highlight = a selection frame (optional).
    /// If you use this, DON'T also wire the button's OnClick to SelectClass/Race/Gender (it'd fire twice).
    /// </summary>
    public class OptionButton : MonoBehaviour
    {
        public enum Kind { Class, Race, Gender }

        public Kind kind;
        public string id;
        [SerializeField] private Button button;
        [SerializeField] private GameObject highlight; // selection frame — shown when this option is picked

        public Button Button => button;
        public void SetSelected(bool on) { if (highlight != null) highlight.SetActive(on); }

        private void Reset() => button = GetComponent<Button>(); // auto-grab the button when added
    }
}
