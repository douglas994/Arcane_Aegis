using UnityEngine;

namespace Arcane_Aegis.Network
{
    /// <summary>
    /// Put this on a GameObject in the World scene. The connection is already up + authenticated (persistent
    /// NetClient from Login), so on load it just asks the server to spawn the chosen character (ClientSession).
    /// </summary>
    public class WorldEntry : MonoBehaviour
    {
        private void Start()
        {
            var net = NetClient.Instance;
            if (net == null) { Debug.LogError("[WorldEntry] No NetClient.Instance — enter the game via the Login scene."); return; }

            if (net.Connected) net.EnterWorld(ClientSession.CharacterId);
            else net.OnConnectedToServer += SendOnce; // (shouldn't happen — already connected — but safe)
        }

        private void SendOnce()
        {
            NetClient.Instance.OnConnectedToServer -= SendOnce;
            NetClient.Instance.EnterWorld(ClientSession.CharacterId);
        }
    }
}
