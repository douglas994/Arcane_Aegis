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
using ArcaneShared.Protocol.ServerToClient;
using Arcane_Aegis.Entities;
using Arcane_Aegis.UI;

namespace Arcane_Aegis.Network
{
    /// <summary>
    /// Client networking entry point: connects to the zone server, logs in, and routes incoming
    /// S2C packets to the EntityManager. Drives the NetManager via PollEvents() on the main thread.
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
        private bool _localSpawned;
        private PlayerView _localView; // our own player (for server position corrections)

        /// <summary>Our own entity id, assigned by the server on login.</summary>
        public ushort MyEntityId { get; private set; }
        public bool InWorld { get; private set; }
        /// <summary>Our own HP% (0..255) from S2C_StateUpdate — bind the HUD to this.</summary>
        public byte LocalHpPercent { get; private set; } = 255;

        private void Awake()
        {
            // Keep running when the window loses focus — otherwise a second test instance freezes,
            // stops sending pings, and the server times it out (PeerDisconnected).
            Application.runInBackground = true;

            _listener.Owner = this;
            if (entities == null) entities = FindAnyObjectByType<EntityManager>();
            if (entities == null)
                Debug.LogError("[NetClient] No EntityManager assigned/found — remote players will NOT spawn. Add an EntityManager to the scene.");
        }

        private void Start()
        {
            _net = new NetManager(_listener, TransportType.Udp)
            {
                ConnectionKey = NetConstants.ConnectionKey,
                ProtocolVersion = NetConstants.ProtocolVersion,
            };
            _net.Connect(host, port);
            Debug.Log($"[NetClient] connecting to {host}:{port} ...");
        }

        private void Update()
        {
            _net?.PollEvents();
        }

        private void OnDestroy()
        {
            _net?.Stop();
        }

        /// <summary>Called by the local controller to report our position/state to the server (~15 Hz).</summary>
        public void SendMovement(Vector3 position, float yaw, MovementState state)
        {
            if (!InWorld || _server == null) return;
            var packet = new C2S_MovementState
            {
                Position = new NetVector3(position.x, position.y, position.z),
                Yaw = yaw,
                State = state,
            };
            Send(packet, DeliveryMethod.Sequenced);
        }

        /// <summary>Asks the server to cast an ability (server validates + resolves; we never decide damage).</summary>
        public void SendCast(byte abilityId, ushort targetId)
        {
            if (!InWorld || _server == null) return;
            Send(new C2S_CastAbility { AbilityId = abilityId, TargetId = targetId }, DeliveryMethod.ReliableOrdered);
        }

        /// <summary>UI-friendly cast: drag this NetClient into a Button's OnClick → CastAbility, set the ability id.
        /// Sends the cast and plays the local predicted attack animation. (int param so the inspector accepts it.)</summary>
        public void CastAbility(int abilityId)
        {
            SendCast((byte)abilityId, 0);
            if (_localView != null) _localView.PlayAttack(); // predicted anim
        }

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

        // ── packet handling (called from the listener) ────────────────

        private void OnConnected(NetPeer peer)
        {
            _server = peer;
            Send(new C2S_Login { Username = username }, DeliveryMethod.ReliableOrdered);
            Debug.Log("[NetClient] connected → sent login");
        }

        private void OnDisconnected(DisconnectReason reason)
        {
            InWorld = false;
            _server = null;
            Debug.Log($"[NetClient] disconnected: {reason}");
        }

        private void OnReceive(BitBuffer reader)
        {
            PacketId id = PacketWriter.ReadId(ref reader);
            switch (id)
            {
                case PacketId.S2C_LoginResult:
                {
                    var p = new S2C_LoginResult();
                    p.Deserialize(ref reader);
                    MyEntityId = p.YourEntityId;
                    InWorld = p.Success;
                    Debug.Log($"[NetClient] login {(p.Success ? "OK" : "FAIL")} — my id = {p.YourEntityId}");
                    if (p.Success && !_localSpawned)
                    {
                        _localSpawned = true;
                        var sp = new Vector3(p.SpawnPosition.X, p.SpawnPosition.Y, p.SpawnPosition.Z);
                        _localView = entities.SpawnLocal(MyEntityId, username, sp);
                    }
                    break;
                }
                case PacketId.S2C_SpawnEntity:
                {
                    var p = new S2C_SpawnEntity();
                    p.Deserialize(ref reader);
                    entities.SpawnRemote(p);
                    break;
                }
                case PacketId.S2C_DespawnEntity:
                {
                    var p = new S2C_DespawnEntity();
                    p.Deserialize(ref reader);
                    entities.Despawn(p.EntityId);
                    break;
                }
                case PacketId.S2C_Snapshot:
                {
                    SnapshotPacket.ReadHeader(ref reader, out int count);
                    for (int i = 0; i < count; i++)
                        entities.ApplySnapshot(SnapshotEntry.Read(ref reader));
                    break;
                }
                case PacketId.S2C_StateUpdate:
                {
                    var p = new S2C_StateUpdate();
                    p.Deserialize(ref reader);
                    LocalHpPercent = p.HpPercent; // own vitals → HUD reads LocalHpPercent
                    if (p.HasCorrection && _localView != null && _localView.Motor != null)
                    {
                        var cp = new Vector3(p.CorrectedPosition.X, p.CorrectedPosition.Y, p.CorrectedPosition.Z);
                        _localView.Motor.SetPosition(cp);
                        Debug.Log($"[NetClient] position corrected by server → {cp}");
                    }
                    break;
                }
                case PacketId.S2C_AbilityCast:
                {
                    var p = new S2C_AbilityCast();
                    p.Deserialize(ref reader);
                    if (p.CasterId != MyEntityId) entities.PlayAttack(p.CasterId); // local already predicted it
                    break;
                }
                case PacketId.S2C_CombatEvent:
                {
                    var p = new S2C_CombatEvent();
                    p.Deserialize(ref reader);
                    Vector3 pos;
                    bool found = false;
                    if (p.TargetId == MyEntityId && _localView != null) { pos = _localView.transform.position; found = true; }
                    else if (entities.TryGetView(p.TargetId, out var v)) { pos = v.transform.position; found = true; }
                    else pos = default;
                    if (found)
                    {
                        bool heal = (p.Flags & S2C_CombatEvent.FlagHeal) != 0;
                        bool crit = (p.Flags & S2C_CombatEvent.FlagCrit) != 0;
                        DamagePopup.Spawn(pos + Vector3.up * 2f, p.Amount, crit, heal);
                    }
                    break;
                }
            }
        }

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
