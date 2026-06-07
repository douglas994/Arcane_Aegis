using System.Collections.Generic;
using UnityEngine;
using NetworkLibrary.Serialization;
using ArcaneShared.Enums;
using ArcaneShared.Protocol;

namespace Arcane_Aegis.Network
{
    /// <summary>
    /// Maps an incoming PacketId to its handler (registered by NetClient at startup). Keeps NetClient pure
    /// transport — all S2C game logic lives in the handlers.
    /// </summary>
    public sealed class ClientPacketRouter
    {
        private readonly Dictionary<PacketId, IClientPacketHandler> _handlers = new();

        public void Register(IClientPacketHandler handler) => _handlers[handler.PacketId] = handler;

        public void Route(ref BitBuffer reader)
        {
            PacketId id = PacketWriter.ReadId(ref reader);
            if (_handlers.TryGetValue(id, out var handler)) handler.Handle(ref reader);
            else Debug.LogWarning($"[NetClient] no handler for {id}");
        }
    }
}
