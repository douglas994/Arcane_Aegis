using UnityEngine;
using Arcane_Aegis.Controllers;
using Arcane_Aegis.Network;

namespace Arcane_Aegis.UI
{
    /// <summary>HUD hooks to summon/store the pet and mount/dismount — wire two buttons to <see cref="TogglePet"/> /
    /// <see cref="ToggleMount"/>. The server toggles (summon if none out / store if out; mount if on foot / dismount).
    /// Mounting routes through <see cref="MountSession"/> so the mount-time channel applies.</summary>
    public sealed class CompanionHud : MonoBehaviour
    {
        public void TogglePet() => NetClient.Instance?.SendTogglePet();

        /// <summary>Pet stance buttons (wire each to one): 0 = agressivo, 1 = defensivo, 2 = passivo.</summary>
        public void SetPetStance(int stance) => NetClient.Instance?.SendPetStance((byte)Mathf.Clamp(stance, 0, 2));

        public void ToggleMount()
        {
            if (MountSession.Instance != null) MountSession.Instance.RequestToggleMount();
            else NetClient.Instance?.SendToggleMount(); // no session in scene → fall back to instant toggle
        }
    }
}
