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
using ArcaneShared.Protocol.ServerToClient;

namespace Arcane_Aegis.Network
{
    /// <summary>
    /// Talks (TCP) to ArcaneMaster for the meta-flow: create account / login. On success it holds the session
    /// <see cref="Token"/> + <see cref="AccountId"/> (used later for the server/character lists + zone handshake).
    /// Separate from <see cref="NetClient"/> (which is the UDP zone connection used in the World scene).
    /// </summary>
    public class MasterClient : MonoBehaviour
    {
        [Header("Master (meta-flow)")]
        [SerializeField] private string host = "127.0.0.1";
        [SerializeField] private int port = 47200;

        private NetManager _net;
        private NetPeer _server;
        private readonly Listener _listener = new();

        /// <summary>True once the TCP connection to the Master is up.</summary>
        public bool Connected => _server != null;
        /// <summary>Session token from a successful login (0 until then).</summary>
        public uint Token { get; private set; }
        public uint AccountId { get; private set; }
        /// <summary>The server/realm the player picked on the select screen (used by the next slices).</summary>
        public byte SelectedServerId { get; set; }

        /// <summary>Raised on the main thread when the Master replies: (ok, reason).</summary>
        public event Action<bool, AuthReason> OnAuthResult;
        /// <summary>Raised on the main thread with the available servers (empty = bad token / none).</summary>
        public event Action<ServerInfo[]> OnServerList;

        private void Awake()
        {
            Application.runInBackground = true;
            _listener.Owner = this;
        }

        private void Start()
        {
            _net = new NetManager(_listener, TransportType.Tcp)
            {
                ConnectionKey = NetConstants.ConnectionKey,
                ProtocolVersion = NetConstants.ProtocolVersion,
            };
            _net.Connect(host, port);
            Debug.Log($"[MasterClient] connecting tcp:{host}:{port} …");
        }

        private void Update() => _net?.PollEvents();
        private void OnDestroy() => _net?.Stop();

        // ── requests ──
        public void CreateAccount(string username, string email, string password)
            => Send(new C2S_CreateAccount { Username = username, Email = email, Password = password });

        public void Login(string username, string password)
            => Send(new C2S_AccountLogin { Username = username, Password = password });

        /// <summary>Asks the Master for the server/realm list (uses the session token from login).</summary>
        public void RequestServerList()
            => Send(new C2S_RequestServerList { Token = Token });

        private void Send<T>(in T packet) where T : IPacket
        {
            if (_server == null) { Debug.LogWarning("[MasterClient] not connected yet"); return; }
            var buffer = new BitBuffer();
            try
            {
                PacketWriter.Write(ref buffer, packet);
                _server.Send(buffer, DeliveryMethod.ReliableOrdered);
            }
            finally
            {
                buffer.Dispose();
            }
        }

        // ── connection callbacks ──
        private void OnConnected(NetPeer peer) { _server = peer; Debug.Log("[MasterClient] connected"); }
        private void OnDisconnected(DisconnectReason reason) { _server = null; Debug.Log($"[MasterClient] disconnected: {reason}"); }

        private void OnReceive(BitBuffer reader)
        {
            PacketId id = PacketWriter.ReadId(ref reader);
            switch (id)
            {
                case PacketId.S2C_AuthResult:
                {
                    var p = new S2C_AuthResult();
                    p.Deserialize(ref reader);
                    if (p.Ok) { Token = p.Token; AccountId = p.AccountId; }
                    OnAuthResult?.Invoke(p.Ok, (AuthReason)p.Reason);
                    break;
                }
                case PacketId.S2C_ServerList:
                {
                    var p = new S2C_ServerList();
                    p.Deserialize(ref reader);
                    OnServerList?.Invoke(p.Servers ?? Array.Empty<ServerInfo>());
                    break;
                }
            }
        }

        private sealed class Listener : INetEventListener
        {
            public MasterClient Owner = null!;
            public void OnPeerConnected(NetPeer peer) => Owner.OnConnected(peer);
            public void OnPeerDisconnected(NetPeer peer, DisconnectReason reason) => Owner.OnDisconnected(reason);
            public void OnNetworkReceive(NetPeer peer, BitBuffer reader, DeliveryMethod deliveryMethod) => Owner.OnReceive(reader);
            public void OnNetworkError(SocketError socketError) => Debug.LogWarning($"[MasterClient] net error: {socketError}");
        }
    }
}
