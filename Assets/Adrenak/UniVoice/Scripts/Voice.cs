using Adrenak.AirPeer;
using Adrenak.UniMic;
using UnityEngine;
using System;
using Adrenak.UniStream;
using Byn.Net;

namespace Adrenak.UniVoice {
	public class Voice : MonoBehaviour {
		public bool Speaking { get; set; }
		public Mic Mic { get; private set; }
		public AudioSource Source { get; private set; }
		public VolumeGate gate;

		// EVENTS
		public delegate void GetVoiceSegmentHandler(int index, float[] segment);
		public delegate void SendVoiceSegmentHandler(int index, float[] segment);
		public delegate void ConnectionHandler(ConnectionId id);
		public delegate void CreateResultHandler(bool success);
		public delegate void JoinResultHandler(bool success);

		public event GetVoiceSegmentHandler OnGetVoiceSegment;
		public event SendVoiceSegmentHandler OnSendVoiceSegment;
		public event ConnectionHandler OnJoin;
		public event ConnectionHandler OnLeave;

		Node m_Node;

		// Must be constant for all clients
		/// <summary>
		/// The sampling frequency at which the Mic should operate
		/// </summary>
		const int k_MicFrequency = 16000;

		/// <summary>
		/// The Mic outputs audio data as segments of arbitrary length. Ensure that 1000 % k_MicSegLenMS == 0
		/// </summary>
		const int k_MicSegLenMS = 100;

		// Prevent contruction using 'new' as this is a MonoBehaviour class
		Voice() { }

		/// <summary>
		/// Creates a new instance.
		/// </summary>
		/// <param name="source">The AudioSource which should be used to play the incoming audio.</param>
		public static Voice New(AudioSource source) {
			var go = new GameObject("Voice");
			DontDestroyOnLoad(go);
			var cted = go.AddComponent<Voice>();
			cted.Source = source;
			return cted;
		}

		bool InitNode() {
			m_Node = Node.New();
			m_Node.OnJoin += delegate (ConnectionId id) {
				if (OnJoin != null) OnJoin(id);
			};
			m_Node.OnLeave += delegate (ConnectionId id) {
				if (OnLeave != null) OnLeave(id);
			};
			return m_Node.Init();
		}

		/// <summary>
		/// Creates a voice chat room using a name
		/// </summary>
		/// <param name="roomName">The name to be used to identify the room.</param>
		/// <param name="callback">Whether the room was successfully created.</param>
		public void Create(string roomName, CreateResultHandler callback) {
			if (callback == null) throw new ArgumentNullException("Callback cannot be null");

			if (!InitNode()) {
				callback(false);
				return;
			}

			m_Node.StartServer(roomName, success => {
				if (success)
					Run();
				callback(success);
			});
		}

		/// <summary>
		/// Joins an existing voice chat room using name.
		/// </summary>
		/// <param name="roomName">The name of the room to be joined</param>
		/// <param name="callback">Whether the Join was successful</param>
		public void Join(string roomName, JoinResultHandler callback) {
			if (callback == null) throw new ArgumentNullException("Callback cannot be null");

			if (!InitNode()) {
				callback(false);
				return;
			}

			m_Node.Connect(roomName, cId => {
				if (cId.IsValid())
					Run();
				callback(cId.IsValid());
			});
		}

		/// <summary>
		/// Leaves the voice chat room
		/// </summary>
		public void Leave() {
			m_Node.Disconnect();
		}

		void Run() {
			AudioBuffer buffer;
			AudioStreamer streamer;

			// MIC SETUP
			Mic = Mic.Instance;
			Mic.StartRecording(k_MicFrequency, k_MicSegLenMS);

			var channels = Mic.Clip.channels;
			var segLen = k_MicFrequency / 1000 * k_MicSegLenMS;
			var segCap = 1000 / k_MicSegLenMS;

			// Create an AudioBuffer using the Mic values
			buffer = new AudioBuffer(
				k_MicFrequency,
				channels,
				segLen,
				segCap
			);

			// Use the buffer to create a streamer
			streamer = AudioStreamer.New(buffer, Source);

			// NETWORKING
			// On receiving a message from a peer, see if the tag is "audio", which denotes
			// that the data is an audio segment
			m_Node.OnGetPacket += delegate (ConnectionId cId, Packet packet, bool reliable) {
				var reader = new UniStreamReader(packet.Payload);
				switch (packet.Tag) {
					case "audio":
						var index = reader.ReadInt();
						var segment = reader.ReadFloatArray();

						if (OnGetVoiceSegment != null)
							OnGetVoiceSegment(index, segment);

						// If the streamer is getting filled, request for a packet skip
						streamer.Stream(index, segment);
						break;
				}
			};

			gate = new VolumeGate(120, 1f, 5);
			VolumeGateVisualizer viz = VolumeGateVisualizer.New(gate);
			// When the microphone is ready with a segment
			Mic.OnSampleReady += (index, segment) => {
				// Return checks
				if (m_Node.NodeState == Node.State.Idle || m_Node.NodeState == Node.State.Uninitialized) return;
				if (!Speaking) return;

				// NOISE REMOVAL
				// Very primitive way to reduce the audio input noise
				// by averaging audio samples with a radius (radius value suggested: 2)
				int radius = 2;
				for (int i = radius; i < segment.Length - radius; i++) {
					float temp = 0;
					for (int j = i - radius; j < i + radius; j++)
						temp += segment[j];
					segment[i] = temp / (2 * radius + 1);
				}

				if (!gate.Evaluate(segment)) return;

				// If Speaking is on, create a payload byte array for AirPeer
				// and send
				if (OnSendVoiceSegment != null) OnSendVoiceSegment(index, segment);
				m_Node.Send(Packet.From(m_Node).WithTag("audio").WithPayload(
					new UniStreamWriter()
					.WriteInt(index)
					.WriteFloatArray(segment)
					.Bytes
				));
			};
		}
	}
}