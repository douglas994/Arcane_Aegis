using NetworkLibrary.Serialization;
using ArcaneShared.Enums;
using ArcaneShared.Protocol.ServerToClient;
using Arcane_Aegis.UI;

namespace Arcane_Aegis.Network.Handlers
{
    /// <summary>S2C_WorldEvent: a zone-wide announcement (a boss spawned or was slain) → a toast/banner.</summary>
    public sealed class WorldEventHandler : IClientPacketHandler
    {
        public PacketId PacketId => PacketId.S2C_WorldEvent;

        public void Handle(ref BitBuffer reader)
        {
            var p = new S2C_WorldEvent();
            p.Deserialize(ref reader);
            if (Toast.Instance == null) return;

            string name = string.IsNullOrEmpty(p.Name) ? "Um chefe" : p.Name;
            string msg = p.Kind == S2C_WorldEvent.KindBossSlain ? $"{name} foi derrotado!" : $"{name} apareceu!";
            Toast.Instance.Show(msg);
        }
    }
}
