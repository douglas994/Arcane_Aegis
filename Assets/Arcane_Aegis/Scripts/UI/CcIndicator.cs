using UnityEngine;
using Arcane_Aegis.Controllers.Locomotion;

namespace Arcane_Aegis.UI
{
    /// <summary>HUD overlay that shows when the LOCAL player is under crowd control (stun/root/slow). Reads the state
    /// the server already replicates via S2C_ControlState (it lands on the local LocomotionStateMachine), so there's no
    /// extra wiring — just drop this on a HUD object and assign <see cref="root"/> (the thing to show) + an optional label.</summary>
    public sealed class CcIndicator : MonoBehaviour
    {
        [Tooltip("O GameObject mostrado enquanto sob CC (ícone/painel/texto). Fica oculto quando livre.")]
        [SerializeField] private GameObject root;
        [Tooltip("Opcional: texto que diz qual CC (Atordoado / Enraizado / Lento).")]
        [SerializeField] private TMPro.TMP_Text label;

        private LocomotionStateMachine _fsm;

        private void Update()
        {
            if (_fsm == null)
            {
                var p = GameObject.FindGameObjectWithTag("Player"); // local player only (it carries the "Player" tag)
                if (p != null) _fsm = p.GetComponentInChildren<LocomotionStateMachine>(true);
                if (_fsm == null) { if (root != null && root.activeSelf) root.SetActive(false); return; }
            }

            bool stun = _fsm.Stunned, rooted = _fsm.Rooted, slow = _fsm.SpeedMult < 0.999f;
            bool cc = stun || rooted || slow;
            if (root != null && root.activeSelf != cc) root.SetActive(cc);
            if (cc && label != null) label.text = stun ? "Atordoado" : rooted ? "Enraizado" : "Lento";
        }
    }
}
