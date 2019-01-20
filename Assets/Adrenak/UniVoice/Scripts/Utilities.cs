namespace Adrenak.UniVoice {
	public static class Utilities {
		public static byte[] ToByteArray(float[] floatArray) {
			int len = floatArray.Length * 4;
			byte[] byteArray = new byte[len];
			int pos = 0;
			foreach (float f in floatArray) {
				byte[] data = System.BitConverter.GetBytes(f);
				System.Array.Copy(data, 0, byteArray, pos, 4);
				pos += 4;
			}
			return byteArray;
		}

		public static float[] ToFloatArray(byte[] byteArray) {
			int len = byteArray.Length / 4;
			float[] floatArray = new float[len];
			for (int i = 0; i < byteArray.Length; i += 4) {
				floatArray[i / 4] = System.BitConverter.ToSingle(byteArray, i);
			}
			return floatArray;
		}
	}
}
