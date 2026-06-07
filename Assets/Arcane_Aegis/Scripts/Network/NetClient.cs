using System.Net.Sockets;
using UnityEngine;
using NetworkLibrary;
using NetworkLibrary.Serialization;
using NetworkLibrary.Transport;
using ArcaneShared.Constants;
using ArcaneShared.Enums;
using ArcaneShared.Models;
using ArcaneShared.Protocol;
using ArcaneShared.Protocol.ClientToServer;
using Arcane_Aegis.Entities;
using Arcane_Aegis.Network.Handlers;

namespace Arcane_Aegis.Network
{
    /// <summary>
    /// Pure transport: connects to the zone server, sends C2S packets, and hands incoming S2C packets to the
    /// ClientPacketRouter (the handlers do the game logic). Holds NO game state — that lives on the entity views
    /// (EntityManager / HumanoidView vitals). Drives the NetManager via PollEvents() on the main thread.
    /// </summary>
    public class NetClient : MonoBehaviour
    {
        [Header("Connection")]
        [SerializeField] private string host = "127.0.0.1";
        [SerializeField] private int port = 47100;
        [SerializeField] private string username = "Hero";

        [Header("Refs")]
        [SerializeField] private EntityManager entities;

        private NetManager _net;
        private NetPeer _server;
        private readonly Listener _listener = new();
        private ClientPacketRouter _router;

        private void Awake()
        {
            // Keep running when the window loses focus — otherwise a second test instance freezes,
            // stops sending pings, and the server times it out (PeerDisconnected).
            Application.runInBackground = true;
            _listener.Owner = this;

            if (entities == null) entities = FindAnyObjectByType<EntityManager>();
            if (entities == null) Debug.LogError("[NetClient] No EntityManager in the scene — entities won't spawn.");
        }

        private void Start()
        {
            _router = BuildRouter();

            _net = new NetManager(_listener, TransportType.Udp)
            {
                ConnectionKey = NetConstants.ConnectionKey,
                ProtocolVersion = NetConstants.ProtocolVersion,
            };
            _net.Connect(host, port);
            Debug.Log($"[NetClient] connecting to {host}:{port} ...");
        }

        private ClientPacketRouter BuildRouter()
        {
            var router = new ClientPacketRouter();
            router.Register(new LoginResultHandler(entities, username));
            router.Register(new SpawnEntityHandler(entities));
            router.Register(new DespawnEntityHandler(entities));
            router.Register(new SnapshotHandler(entities));
            router.Register(new StateUpdateHandler(entities));
            router.Register(new AbilityCastHandler(entities));
            router.Register(new CombatEventHandler(entities));
            return router;
        }

        private void Update() => _net?.PollEvents();
        private void OnDestroy() => _net?.Stop();

        // ── send (transport only; game logic decides WHEN to call these) ──

        /// <summary>Reports our position/state to the server (~15 Hz, called by MovementSender).</summary>
        public void SendMovement(Vector3 position, float yaw, MovementState state)
        {
            if (!CanSend) return;
            Send(new C2S_MovementState
            {
                Position = new NetVector3(position.x, position.y, position.z),
                Yaw = yaw,
                State = state,
            }, DeliveryMethod.Sequenced);
        }

        /// <summary>Asks the server to cast an ability (PlayerCombat decides WHEN; server validates + resolves).</summary>
        public void SendCast(byte abilityId, ushort targetId)
        {
            if (!CanSend) return;
            Send(new C2S_CastAbility { AbilityId = abilityId, TargetId = targetId }, DeliveryMethod.ReliableOrdered);
        }

        private bool CanSend => _server != null && entities != null && entities.Local != null;

        private void Send<T>(in T packet, DeliveryMethod method) where T : IPacket
        {
            var buffer = new BitBuffer();
            try
            {
                PacketWriter.Write(ref buffer, packet);
                _server.Send(buffer, method);
            }
            finally
            {
                buffer.Dispose();
            }
        }

        // ── connection callbacks ──

        private void OnConnected(NetPeer peer)
        {
            _server = peer;
            Send(new C2S_Login { Username = username }, DeliveryMethod.ReliableOrdered);
            Debug.Log("[NetClient] connected → sent login");
        }

        private void OnDisconnected(DisconnectReason reason)
        {
            _server = null;
            Debug.Log($"[NetClient] disconnected: {reason}");
        }

        private void OnReceive(BitBuffer reader) => _router.Route(ref reader);

        /// <summary>Bridges NetworkLibrary events to the MonoBehaviour (kept separate so NetClient isn't an interface dump).</summary>
        private sealed class Listener : INetEventListener
        {
            public NetClient Owner = null!;

            public void OnPeerConnected(NetPeer peer) => Owner.OnConnected(peer);
            public void OnPeerDisconnected(NetPeer peer, DisconnectReason reason) => Owner.OnDisconnected(reason);
            public void OnNetworkReceive(NetPeer peer, BitBuffer reader, DeliveryMethod deliveryMethod) => Owner.OnReceive(reader);
            public void OnNetworkError(SocketError socketError) => Debug.LogWarning($"[NetClient] net error: {socketError}");
        }
    }
}
