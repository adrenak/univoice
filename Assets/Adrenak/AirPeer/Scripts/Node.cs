using System;
using System.Linq;
using Byn.Net;
using UnityEngine;
using Adrenak.UniStream;
using System.Collections.Generic;

namespace Adrenak.AirPeer {
	public class Node : MonoBehaviour {
		public enum State {
			Uninitialized,
			Idle,
			Server,
			Client
		}

		string k_SignallingServer = "wss://because-why-not.com:12777/chatapp";
		string k_ICEServer1 = "stun:because-why-not.com:12779";
		string k_ICEServer2 = "stun:stun.l.google.com:19302";

		Action<bool> m_StartServerCallback;
		Action m_StopServerCallback;
		Action<ConnectionId> m_ConnectCallback;

		/// <summary>
		/// Fires on the client end when the server has been stopped
		/// </summary>
		public event Action OnServerStopped;

		/// <summary>
		/// Fired when a new client has joined the network
		/// </summary>
		public event Action<ConnectionId> OnJoin;

		/// <summary>
		/// Fired when a client has left the network
		/// </summary>
		public event Action<ConnectionId> OnLeave;

		/// <summary>
		/// Fired when a packet has been received by the node
		/// </summary>
		public event Action<ConnectionId, Packet, bool> OnGetPacket;

		/// <summary>
		/// Fired when a raw message (byte array) has been received by the node
		/// </summary>
		public event Action<ConnectionId, byte[], bool> OnGetBytes;

		IBasicNetwork m_Network;
		public List<ConnectionId> ConnectionIds { get; private set; }
		public ConnectionId CId {
			get {
				if (ConnectionIds == null || ConnectionIds.Count == 0)
					return ConnectionId.INVALID;
				return ConnectionIds[0];
			}
		}
		public State NodeState { get; private set; }

		// ================================================
		// LIFECYCLE
		// ================================================

		// Prevent 'new' keyword creation
		Node() { }

		/// <summary>
		/// Creates a new Node instance. The instance can be used as a client or a server
		/// </summary>
		/// <returns>The created instance</returns>
		public static Node New() {
			var go = new GameObject() {
				hideFlags = HideFlags.HideAndDontSave
			};
			DontDestroyOnLoad(go);
			return go.AddComponent<Node>();
		}

		/// <summary>
		/// Initializes the inner network
		/// </summary>
		/// <returns>Whether the initialization was successful</returns>
		public bool Init() {
			Deinit();

			ConnectionIds = new List<ConnectionId>();
			m_Network = WebRtcNetworkFactory.Instance.CreateDefault(
				k_SignallingServer,
				new[] { new IceServer(k_ICEServer1), new IceServer(k_ICEServer2) }
			);
			var result = m_Network != null;
			if (result)
				NodeState = State.Uninitialized;
			else
				NodeState = State.Idle;
			return m_Network != null;
		}

		/// <summary>
		/// Deinitializes the inner network
		/// </summary>
		/// <returns>Whether the deinitialization can be done</returns>
		public bool Deinit() {
			if (m_Network != null) {
				m_Network.Dispose();
				m_Network = null;
				NodeState = State.Uninitialized;
				return true;
			}
			return false;
		}

		void Update() {
			if (m_Network != null) {
				m_Network.Update();
				ReadNetworkEvents();
			}
			if (m_Network != null)
				m_Network.Flush();
		}

		void OnDestroy() {
			Deinit();
		}

		// ================================================
		// NETWORK API
		// ================================================
		/// <summary>
		/// Starts the server with a given server name on this node
		/// </summary>
		/// <param name="name">The name of the server to be started</param>
		/// <param name="callback">Callback for whether the server could start</param>
		/// <returns>If a server can be started</returns>
		public bool StartServer(string name, Action<bool> callback) {
			if (m_Network == null) return false;
			m_StartServerCallback = callback;
			m_Network.StartServer(name);
			return true;
		}

		/// <summary>
		/// Stops the server on the node
		/// </summary>
		/// <param name="callback">Callback for when the server stopped</param>
		/// <returns>If the server can be stopped</returns>
		public bool StopServer(Action callback) {
			if (m_Network == null) return false;
			// WebRTC doesn't tell the client when the server it is connected to
			// goes offline. So broadcast a reserved event message to everyone, reliably
			Send(Packet.From(CId).WithTag(ReservedTags.ServerStopped), true);
			m_StopServerCallback = callback;
			m_Network.StopServer();
			return false;
		}

		/// <summary>
		/// Connects the inner client to a server identified by the name
		/// </summary>
		/// <param name="name">The name of the server to be connected to</param>
		/// <param name="callback">Whether the connection was successful</param>
		/// <returns>If the inner client can connect to a server</returns>
		public bool Connect(string name, Action<ConnectionId> callback) {
			if (m_Network == null) return false;
			m_ConnectCallback = callback;
			m_Network.Connect(name);
			return true;
		}

		/// <summary>
		/// Disconnects the inner client from the server
		/// </summary>
		/// <returns>If the inner client disconnected successfully</returns>
		public bool Disconnect() {
			if (m_Network == null) return false;
			m_Network.Disconnect(CId);
			return true;
		}

		/// <summary>
		/// Sends a packet over the network
		/// </summary>
		/// <param name="packet">The <see cref="Packet"/> instance that is to be sent</param>
		/// <param name="reliable">Whether the transmission is reliable (slow) or unreliable (fast)</param>
		/// <returns>Whether message can be sent or not</returns>
		public bool Send(Packet packet, bool reliable = false) {
			if (m_Network == null || ConnectionIds == null || ConnectionIds.Count == 0) return false;

			List<ConnectionId> recipients = new List<ConnectionId>();
			if (packet.Recipients.Length != 0) {
				recipients = ConnectionIds.Select(x => x)
					.Where(x => packet.Recipients.Contains(x.id))
					.ToList();
			}
			else
				recipients = ConnectionIds.ToList();

			var bytes = packet.Serialize();
			foreach (var cid in recipients)
				m_Network.SendData(cid, bytes, 0, bytes.Length, reliable);

			return true;
		}

		/// <summary>
		/// Send a "raw" byte array over the network. 
		/// </summary>
		/// <param name="bytes">The byte array that has to be sent over the network</param>
		/// <param name="reliable">Whether the transmission is reliable (slow) or unreliable (fast)</param>
		/// <returns>Whether message can be sent or not</returns>
		public bool Send(byte[] bytes, bool reliable) {
			if (m_Network == null || ConnectionIds == null || ConnectionIds.Count == 0) return false;

			foreach (var cid in ConnectionIds)
				m_Network.SendData(cid, bytes, 0, bytes.Length, reliable);
			return true;
		}

		// ================================================
		// NETWORK EVENT PROCESSING
		// ================================================
		void ReadNetworkEvents() {
			NetworkEvent netEvent;
			while (m_Network != null && m_Network.Dequeue(out netEvent))
				ProcessNetworkEvent(netEvent);
		}

		void ProcessNetworkEvent(NetworkEvent netEvent) {
			switch (netEvent.Type) {
				case NetEventType.ServerInitialized:
					OnServerInitSuccess(netEvent);
					break;

				case NetEventType.ServerInitFailed:
					OnServerInitFailed(netEvent);
					break;

				case NetEventType.ServerClosed:
					OnServerClosed(netEvent);
					break;

				case NetEventType.NewConnection:
					OnNewConnection(netEvent);
					break;

				case NetEventType.ConnectionFailed:
					OnConnectionFailed(netEvent);
					break;

				// Clients disconnect instantly and server gets to know.
				case NetEventType.Disconnected:
					OnDisconnected(netEvent);
					break;

				case NetEventType.ReliableMessageReceived:
					OnMessageReceived(netEvent, true);
					break;

				case NetEventType.UnreliableMessageReceived:
					OnMessageReceived(netEvent, false);
					break;
			}
		}

		void OnServerInitSuccess(NetworkEvent netEvent) {
			ConnectionIds.Add(new ConnectionId(0));
			NodeState = State.Server;
			m_StartServerCallback.TryInvoke(true);
			m_StartServerCallback = null;
		}

		void OnServerInitFailed(NetworkEvent netEvent) {
			Deinit();
			NodeState = State.Uninitialized;
			m_StartServerCallback.TryInvoke(false);
			m_StartServerCallback = null;
		}

		void OnServerClosed(NetworkEvent netEvent) {
			NodeState = State.Uninitialized;
			m_StopServerCallback.TryInvoke();
			m_StopServerCallback = null;
		}

		void OnNewConnection(NetworkEvent netEvent) {
			ConnectionId newCId = netEvent.ConnectionId;
			ConnectionIds.Add(newCId);

			if (NodeState == State.Uninitialized) {
				OnJoin.TryInvoke(newCId);

				// Add server as a connection on the client end
				ConnectionIds.Add(new ConnectionId(0));
				NodeState = State.Client;
			}
			else if (NodeState == State.Server) {
				OnJoin.TryInvoke(newCId);
				foreach (var id in ConnectionIds) {
					if (id.id == 0 || id.id == newCId.id) continue;

					byte[] payload;

					// Announce the new connection to the old ones and vice-versa
					payload = new UniStreamWriter().WriteShort(newCId.id).Bytes;
					Send(Packet.From(this).To(id).With(ReservedTags.ClientJoined, payload), true);

					payload = new UniStreamWriter().WriteShort(id.id).Bytes;
					Send(Packet.From(this).To(newCId).With(ReservedTags.ClientJoined, payload), true);
				}
			}

			m_ConnectCallback.TryInvoke(newCId);
			m_ConnectCallback = null;
		}

		void OnConnectionFailed(NetworkEvent netEvent) {
			if (NodeState == State.Server) return;

			Deinit();
			NodeState = State.Uninitialized;
			m_ConnectCallback.TryInvoke(ConnectionId.INVALID);
			m_ConnectCallback = null;
		}

		void OnDisconnected(NetworkEvent netEvent) {
			if (NodeState == State.Client) {
				NodeState = State.Uninitialized;
				Deinit();
			}
			else if (NodeState == State.Server) {
				OnLeave.TryInvoke(netEvent.ConnectionId);
				var dId = netEvent.ConnectionId;
				ConnectionIds.Remove(netEvent.ConnectionId);

				var payload = new UniStreamWriter().WriteShort(dId.id).Bytes;

				// Clients are not aware of each other as this is a star network
				// Send a reliable reserved message to everyone to announce the disconnection
				Send(Packet.From(CId).With(ReservedTags.ClientLeft, payload), true);
			}
		}

		void OnMessageReceived(NetworkEvent netEvent, bool reliable) {
			var bytes = netEvent.GetDataAsByteArray();
			var packet = Packet.Deserialize(bytes);

			// If packet is null, it is a "raw" byte array message. 
			// Forward it to everyone
			if (packet == null) {
				OnGetBytes.TryInvoke(netEvent.ConnectionId, bytes, reliable);
				foreach (var r in ConnectionIds) {
					// Forward to everyone except the original sender and the server
					if (r == CId || r == netEvent.ConnectionId) continue;
					Send(Packet.From(CId).To(r).With(ReservedTags.PacketForwarding, packet.Serialize()), true);
				}
				return;
			}
				

			string reservedTag = packet.Tag.StartsWith("reserved") ? packet.Tag : string.Empty;

			// If is not a reserved message
			if (reservedTag == string.Empty) {
				OnGetPacket.TryInvoke(netEvent.ConnectionId, packet, reliable);

				if (NodeState != State.Server) return;

				// The server tries to broadcast the packet to everyone else listed as recipients
				foreach (var r in packet.Recipients) {
					// Forward to everyone except the original sender and the server
					if (r == CId.id || r == netEvent.ConnectionId.id) continue;
					Send(Packet.From(CId).To(r).With(ReservedTags.PacketForwarding, packet.Serialize()), true);
				}
				return;
			}

			// handle reserved messages
			switch (reservedTag) {
				case ReservedTags.ServerStopped:
					OnServerStopped.TryInvoke();
					break;
				case ReservedTags.ClientJoined:
					ConnectionIds.Add(netEvent.ConnectionId);
					OnJoin.TryInvoke(netEvent.ConnectionId);
					break;
				case ReservedTags.ClientLeft:
					ConnectionIds.Remove(netEvent.ConnectionId);
					OnLeave.TryInvoke(netEvent.ConnectionId);
					break;
				case ReservedTags.PacketForwarding:
					OnGetPacket.TryInvoke(netEvent.ConnectionId, packet, reliable);
					break;
			}
		}
	}
}