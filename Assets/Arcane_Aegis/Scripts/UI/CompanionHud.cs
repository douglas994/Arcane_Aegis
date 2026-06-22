using UnityEngine;
using Arcane_Aegis.Network;

namespace Arcane_Aegis.UI
{
    /// <summary>HUD hooks to summon/store the pet and mount/dismount — wire two buttons to <see cref="TogglePet"/> /
    /// <see cref="ToggleMount"/>. The server toggles (summon if none out / store if out; mount if on foot / dismount).</summary>
    public sealed class CompanionHud : MonoBehaviour
    {
        public void TogglePet() => NetClient.Instance?.SendTogglePet();
        public void ToggleMount() => NetClient.Instance?.SendToggleMount();
    }
}
