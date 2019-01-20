using Byn.Net;
using System;
using System.Text;
using System.Linq;
using Adrenak.UniStream;

namespace Adrenak.AirPeer {
    public class Packet {
        public short Sender { get; private set; }
        public short[] Recipients { get; private set; }
        public string Tag { get; private set; }
        public byte[] Payload { get; private set; }

        public bool IsToAll {
            get { return Recipients == null || Recipients.Length == 0; }
        }

        public bool HasNoTag {
            get { return Tag == string.Empty; }
        }

        public bool HasNoPayload {
            get { return Payload.Length == 0; }
        }

        Packet() {
            Sender = -1;
            Recipients = new short[0];
            Tag = string.Empty;
            Payload = new byte[0];
        }

        // ================================================
        // OBJECT BUILDER
        // ================================================
        public static Packet From(Node node) {
            return From(node.CId);
        }

        public static Packet From(ConnectionId cid) {
            return From(cid.id);
        }

        public static Packet From(short sender) {
            Packet cted = new Packet();
            cted.Sender = sender;
            return cted;
        }

        public Packet To(ConnectionId recipient) {
            return To(new[] { recipient.id });
        }

        public Packet To(short recipient) {
            return To(new[] { recipient });
        }

        public Packet To(ConnectionId[] recipients) {
            if (recipients == null) recipients = new ConnectionId[0];
            Recipients = recipients.Select(x => x.id).ToList().ToArray();
            return this;
        }

        public Packet To(short[] recipients) {
            if (recipients == null) recipients = new short[0];
            Recipients = recipients;
            return this;
        }

        public Packet With(string tag, string payload) {
            return WithTag(tag).WithPayload(payload);
        }

        public Packet With(string tag, byte[] payload) {
            return WithTag(tag).WithPayload(payload);
        }

        public Packet WithTag(string tag) {
            SetTag(tag);
            return this;
        }

        public Packet WithPayload(string payload) {
            SetPayloadString(payload);
            return this;
        }

        public Packet WithPayload(byte[] payload) {
            SetPayloadBytes(payload);
            return this;
        }

        void SetTag(string tag) {
            if (string.IsNullOrEmpty(tag))
                tag = string.Empty;
            Tag = tag;
        }

        void SetPayloadString(string payload) {
            if (string.IsNullOrEmpty(payload))
                payload = string.Empty;
            Payload = Encoding.UTF8.GetBytes(payload);
        }

        void SetPayloadBytes(byte[] payload) {
            if (payload == null || payload.Length == 0)
                payload = new byte[0];
            Payload = payload;
        }

        // ================================================
        // (DE)SERIALIZATION
        // ================================================
        public static Packet Deserialize(byte[] bytes) {
            UniStreamReader reader = new UniStreamReader(bytes);

            var packet = new Packet();
            try {
                packet.Sender = reader.ReadShort();
                packet.Recipients = reader.ReadShortArray();
                packet.Tag = reader.ReadString();
                packet.Payload = reader.ReadBytes(bytes.Length - reader.Index);
            }
            catch(Exception e) {
                UnityEngine.Debug.LogError("Packet deserialization error: " + e.Message);
                packet = null;
            }

            return packet;
        }

        public byte[] Serialize() {
            UniStreamWriter writer = new UniStreamWriter();
            try {
                writer.WriteShort(Sender);
                writer.WriteShortArray(Recipients);
                writer.WriteString(Tag);
                writer.WriteBytes(Payload);
            }
            catch (Exception e) {
                UnityEngine.Debug.LogError("Packet serialization error : " + e.Message);
            }

            return writer.Bytes;
        }

		public override string ToString() {
			StringBuilder sb = new StringBuilder("Packet:")
				.Append("Sender=").Append(Sender).Append("\n")
				.Append("Recipients=").Append("\n");
			foreach (var r in Recipients)
				sb.Append(r).Append(",");
			sb.Append("\n");
			sb.Append("Tag=").Append(Tag).Append("\n")
				.Append("PayloadLength=").Append(Payload.Length);
			return sb.ToString();
		}
	}
}
