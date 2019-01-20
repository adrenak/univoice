using System;
using System.Text;
using UnityEngine;
using System.Collections.Generic;

namespace Adrenak.UniStream {
	public class UniStreamWriter {
		List<byte> m_Bytes;

		public UniStreamWriter() {
			m_Bytes = new List<byte>();
		}
		
		public byte[] Bytes {
			get { return m_Bytes.ToArray(); }
		}

		// Default types
		public UniStreamWriter WriteShort(Int16 value) {
			var bytes = BitConverter.GetBytes(value);
			EndianUtility.EndianCorrection(bytes);
			WriteBytes(bytes);
			return this;
		}

		public UniStreamWriter WriteInt(Int32 value) {
			var bytes = BitConverter.GetBytes(value);
			EndianUtility.EndianCorrection(bytes);
			WriteBytes(bytes);
			return this;
		}

		public UniStreamWriter WriteLong(Int64 value) {
			var bytes = BitConverter.GetBytes(value);
			EndianUtility.EndianCorrection(bytes);
			WriteBytes(bytes);
			return this;
		}

		public UniStreamWriter WriteFloat(Single value) {
			var bytes = BitConverter.GetBytes(value);
			EndianUtility.EndianCorrection(bytes);
			WriteBytes(bytes);
			return this;
		}

		public UniStreamWriter WriteDouble(double value) {
			var bytes = BitConverter.GetBytes(value);
			EndianUtility.EndianCorrection(bytes);
			WriteBytes(bytes);
			return this;
		}

		public UniStreamWriter WriteChar(char val) {
			WriteBytes(BitConverter.GetBytes(val));
			return this;
		}

		public UniStreamWriter WriteShortArray(Int16[] array) {
			WriteInt(array.Length);

			foreach (var e in array)
				WriteShort(e);
			return this;
		}

		public UniStreamWriter WriteIntArray(Int32[] array) {
			WriteInt(array.Length);

			foreach (var e in array)
				WriteInt(e);
			return this;
		}

		public UniStreamWriter WriteLongArray(Int64[] array) {
			WriteInt(array.Length);

			foreach (var e in array)
				WriteLong(e);
			return this;
		}

		public UniStreamWriter WriteFloatArray(Single[] array) {
			WriteInt(array.Length);

			foreach (var e in array)
				WriteFloat(e);
			return this;
		}


		public UniStreamWriter WriteDoubleArray(Double[] array) {
			WriteInt(array.Length);

			foreach (var e in array)
				WriteDouble(e);
			return this;
		}

		public UniStreamWriter WriteString(string str) {
			var strB = Encoding.UTF8.GetBytes(str);
			WriteInt(strB.Length);
			WriteBytes(strB);
			return this;
		}

		// Unity types
		public UniStreamWriter WriteVector3(Vector3 value) {
			var xbytes = BitConverter.GetBytes(value.x);
			var ybytes = BitConverter.GetBytes(value.y);
			var zbytes = BitConverter.GetBytes(value.z);

			EndianUtility.EndianCorrection(xbytes);
			EndianUtility.EndianCorrection(ybytes);
			EndianUtility.EndianCorrection(zbytes);

			WriteBytes(xbytes);
			WriteBytes(ybytes);
			WriteBytes(zbytes);

			return this;
		}

		public UniStreamWriter WriteVector3Array(Vector3[] array) {
			var lenB = BitConverter.GetBytes(array.Length);
			EndianUtility.EndianCorrection(lenB);
			WriteBytes(lenB);

			foreach (var e in array)
				WriteVector3(e);

			return this;
		}

		public UniStreamWriter WriteVector2(Vector2 value) {
			var xbytes = BitConverter.GetBytes(value.x);
			var ybytes = BitConverter.GetBytes(value.y);

			EndianUtility.EndianCorrection(xbytes);
			EndianUtility.EndianCorrection(ybytes);

			WriteBytes(xbytes);
			WriteBytes(ybytes);

			return this;
		}

		public UniStreamWriter WriteVector2Array(Vector2[] array) {
			var lenB = BitConverter.GetBytes(array.Length);
			EndianUtility.EndianCorrection(lenB);
			WriteBytes(lenB);

			foreach (var e in array)
				WriteVector2(e);

			return this;
		}

		public UniStreamWriter WriteRect(Rect rect) {
			var xbytes = BitConverter.GetBytes(rect.x);
			var ybytes = BitConverter.GetBytes(rect.y);
			var wbytes = BitConverter.GetBytes(rect.width);
			var hbytes = BitConverter.GetBytes(rect.height);

			EndianUtility.EndianCorrection(xbytes);
			EndianUtility.EndianCorrection(ybytes);
			EndianUtility.EndianCorrection(wbytes);
			EndianUtility.EndianCorrection(hbytes);

			WriteBytes(xbytes);
			WriteBytes(ybytes);
			WriteBytes(wbytes);
			WriteBytes(hbytes);

			return this;
		}

		public UniStreamWriter WriteRectArray(Rect[] array) {
			var lenB = BitConverter.GetBytes(array.Length);
			EndianUtility.EndianCorrection(lenB);
			WriteBytes(lenB);

			foreach (var e in array)
				WriteRect(e);

			return this;
		}

		public UniStreamWriter WriteColor32(Color32 color) {
			WriteByte(color.r);
			WriteByte(color.g);
			WriteByte(color.b);
			WriteByte(color.a);
			return this;
		}

		public UniStreamWriter WriteColor32Array(Color32[] array) {
			WriteInt(array.Length);

			for (int i = 0; i < array.Length; i++)
				WriteColor32(array[i]);
			return this;
		}

		public UniStreamWriter WriteColor(Color color) {
			WriteFloat(color.r);
			WriteFloat(color.g);
			WriteFloat(color.b);
			WriteFloat(color.a);
			return this;
		}

		public UniStreamWriter WriteColorArray(Color[] array) {
			WriteInt(array.Length);

			for (int i = 0; i < array.Length; i++)
				WriteColor(array[i]);
			return this;
		}

		public void WriteBytes(byte[] block) {
			foreach (var b in block)
				m_Bytes.Add(b);
		}

		public void WriteByte(byte b) {
			m_Bytes.Add(b);
		}
	}
}
