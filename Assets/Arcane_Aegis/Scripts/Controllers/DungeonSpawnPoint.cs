using UnityEngine;

namespace Arcane_Aegis.Controllers
{
    /// <summary>Drop this empty marker in the Dungeon scene where the player should APPEAR on entering (e.g. right next to
    /// the exit portal). The client spawns the local player here and the server adopts the position (an instance accepts
    /// the client's initial position), so there's no coord to match by hand. If no marker is present, the player falls back
    /// to the template's spawn point (Config/zones.json). Only the first one found is used; point its blue arrow (forward)
    /// the way the player should face.</summary>
    public sealed class DungeonSpawnPoint : MonoBehaviour
    {
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 1f, 0.5f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, 0.5f);
            Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.9f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 1.5f); // facing
        }
    }
}
