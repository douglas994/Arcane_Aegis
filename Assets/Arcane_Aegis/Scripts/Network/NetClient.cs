using System;
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

        /// <summary>The one persistent connection (singleton, survives scene loads). Scene scripts use this.</summary>
        public static NetClient Instance { get; private set; }

        /// <summary>True once connected to the ArcaneServer (char-phase sends are allowed; gameplay sends need a spawned Local).</summary>
        public bool Connected => _server != null;

        /// <summary>Raised once connected + handshaked (so the lobby screen can request data only when ready).</summary>
        public event Action OnConnectedToServer;

        // Character-lobby events (raised on the main thread by the handlers).
        public event Action<CreationOption[], CreationOption[]> OnCreationData; // (races, classes)
        public event Action<CharacterSummary[]> OnCharacterList;
        public event Action<CharCreateResult> OnCharacterCreateResult;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; } // keep the one persistent client
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Keep running when the window loses focus (a backgrounded instance stops pinging → server times it out).
            Application.runInBackground = true;
            _listener.Owner = this;

            _net = new NetManager(_listener, TransportType.Udp)
            {
                ConnectionKey = NetConstants.ConnectionKey,
                ProtocolVersion = NetConstants.ProtocolVersion,
            };
            if (entities == null) entities = FindAnyObjectByType<EntityManager>();
            _router = BuildRouter();
        }

        /// <summary>Connects to a zone server (called after the Master returns the chosen realm's address). The
        /// connection then persists across scenes (lobby → world).</summary>
        public void ConnectTo(string serverHost, int serverPort)
        {
            host = serverHost;
            port = serverPort;
            _net.Connect(serverHost, serverPort);
            Debug.Log($"[NetClient] connecting to {serverHost}:{serverPort} …");
        }

        /// <summary>Binds the current scene's EntityManager (World/Dungeon) so entity packets route to it. The
        /// connection survives scene loads; the manager is per-scene, so it re-binds on load.</summary>
        public void SetEntityManager(EntityManager em)
        {
            entities = em;
            _router = BuildRouter(); // rebuild so the entity handlers use the new manager
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
            router.Register(new CreationDataHandler((races, classes) => OnCreationData?.Invoke(races, classes)));
            router.Register(new CharacterListHandler(chars => OnCharacterList?.Invoke(chars)));
            router.Register(new CharacterCreateResultHandler(result => OnCharacterCreateResult?.Invoke(result)));
            return router;
        }

        private void Update() => _net?.PollEvents();

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            _net?.Stop();
        }

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

        // ── character lobby (before entering the world; no spawned Local yet, so gated only on Connected) ──

        /// <summary>Asks the server for the data-driven race/class catalog (for the create screen).</summary>
        public void RequestCreationData()
        {
            if (_server == null) return;
            Send(new C2S_RequestCreationData(), DeliveryMethod.ReliableOrdered);
        }

        /// <summary>Asks the server for this account's characters (account = the authenticated connection).</summary>
        public void RequestCharacters()
        {
            if (_server == null) return;
            Send(new C2S_RequestCharacters(), DeliveryMethod.ReliableOrdered);
        }

        /// <summary>Asks the server to create a character (server validates + persists via the DB).</summary>
        public void CreateCharacter(string name, string raceId, string classId)
        {
            if (_server == null) return;
            Send(new C2S_CreateCharacter { Name = name, RaceId = raceId, ClassId = classId }, DeliveryMethod.ReliableOrdered);
        }

        /// <summary>Spawns the chosen character into the world (call from the World scene; server verifies ownership).</summary>
        public void EnterWorld(uint characterId)
        {
            if (_server == null) return;
            Send(new C2S_EnterWorld { CharacterId = characterId }, DeliveryMethod.ReliableOrdered);
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

        // ── connection callbacks ──

        private void OnConnected(NetPeer peer)
        {
            _server = peer;
            // Authenticate with the session token (the Master pre-registered it at this zone). The server resolves
            // the token → account; no client-sent account. No auto-spawn — the character lobby runs first.
            Send(new C2S_Handshake { SessionToken = ClientSession.Token }, DeliveryMethod.ReliableOrdered);
            Debug.Log("[NetClient] connected → sent handshake (token)");
            OnConnectedToServer?.Invoke();
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
