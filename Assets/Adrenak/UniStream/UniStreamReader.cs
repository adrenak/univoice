using System;
using UnityEngine;
using System.Text;

namespace Adrenak.UniStream {
	public class UniStreamReader {
		public int Index { get; private set; }
		byte[] m_Payload;

		public UniStreamReader(byte[] payload) {
			m_Payload = payload;
			Index = 0;
		}

		// Default types
		public Int16 ReadShort() {
			var bytes = ReadBytes(2);
			EndianUtility.EndianCorrection(bytes);
			return BitConverter.ToInt16(bytes, 0);
		}

		public Int16[] ReadShortArray() {
			var len = ReadInt();
			var result = new Int16[len];

			for (int i = 0; i < result.Length; i++)
				result[i] = ReadShort();
			return result;
		}

		public Int32 ReadInt() {
			var bytes = ReadBytes(4);
			EndianUtility.EndianCorrection(bytes);
			return BitConverter.ToInt32(bytes, 0);
		}

		public Int32[] ReadIntArray() {
			var len = ReadInt();
			var result = new Int32[len];

			for (int i = 0; i < result.Length; i++)
				result[i] = ReadInt();
			return result;
		}

		public Int64 ReadLong() {
			var bytes = ReadBytes(8);
			EndianUtility.EndianCorrection(bytes);
			return BitConverter.ToInt64(bytes, 0);
		}

		public Int64[] ReadLongArray() {
			var len = ReadLong();
			var result = new Int64[len];

			for (int i = 0; i < result.Length; i++)
				result[i] = ReadLong();
			return result;
		}

		public float ReadFloat() {
			var bytes = ReadBytes(4);
			EndianUtility.EndianCorrection(bytes);
			return BitConverter.ToSingle(bytes, 0);
		}

		public Single[] ReadFloatArray() {
			var len = ReadInt();
			var result = new Single[len];

			for (int i = 0; i < result.Length; i++)
				result[i] = ReadFloat();
			return result;
		}

		public double ReadDouble() {
			var bytes = ReadBytes(8);
			EndianUtility.EndianCorrection(bytes);
			return BitConverter.ToDouble(bytes, 0);
		}

		public Double[] ReadDoubleArray() {
			var len = ReadInt();
			var result = new Double[len];

			for (int i = 0; i < result.Length; i++)
				result[i] = ReadShort();
			return result;
		}

		public char ReadChar() {
			return BitConverter.ToChar(ReadBytes(2), 0);
		}

		public string ReadString() {
			var len = ReadInt();
			return Encoding.UTF8.GetString(ReadBytes(len));
		}

		// Unity types
		public Vector2 ReadVector2() {
			return new Vector2(ReadFloat(), ReadFloat());
		}

		public Vector2[] ReadVector2Array() {
			var len = ReadInt();
			var result = new Vector2[len];

			for (int i = 0; i < len; i++)
				result[i] = ReadVector2();
			return result;
		}

		public Vector3 ReadVector3() {
			return new Vector3(ReadFloat(), ReadFloat(), ReadFloat());
		}

		public Vector3[] ReadVector3Array() {
			var len = ReadInt();
			var result = new Vector3[len];

			for (int i = 0; i < len; i++)
				result[i] = ReadVector3();
			return result;
		}

		public Rect ReadRect() {
			return new Rect(ReadFloat(), ReadFloat(), ReadFloat(), ReadFloat());
		}

		public Rect[] ReadRectArray() {
			var len = ReadInt();
			var result = new Rect[len];

			for (int i = 0; i < len; i++)
				result[i] = ReadRect();
			return result;
		}

		public Color32 ReadColor32() {
			byte r, g, b, a;
			ReadByte(out r);
			ReadByte(out g);
			ReadByte(out b);
			ReadByte(out a);
			return new Color32(r, g, b, a);
		}

		public Color32[] ReadColor32Array() {
			int len = ReadInt();
			var result = new Color32[len];

			for (int i = 0; i < result.Length; i++)
				result[i] = ReadColor32();
			return result;
		}

		public Color ReadColor() {
			return new Color(ReadFloat(), ReadFloat(), ReadFloat(), ReadFloat());
		}

		public Color[] ReadColorArray() {
			int len = ReadInt();
			var result = new Color[len];

			for (int i = 0; i < result.Length; i++)
				result[i] = ReadColor();
			return result;
		}

		public byte[] ReadBytes(int length) {
			try {
				byte[] b = new byte[length];
				Buffer.BlockCopy(m_Payload, Index, b, 0, length);
				Index += length;
				return b;
			}
			catch {
				return null;
			}
		}

		public bool ReadByte(out byte result) {
			try {
				result = m_Payload[Index];
				Index++;
				return true;
			}
			catch {
				result = 0;
				return false;
			}
		}
	}
}
