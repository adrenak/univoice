using System;

namespace Adrenak.UniStream {
	public class EndianUtility {
		static bool useLittleEndian = true;

		public static bool RequiresEndianCorrection {
			get { return useLittleEndian ^ BitConverter.IsLittleEndian; }
		}

		public static void EndianCorrection(byte[] bytes) {
			if (RequiresEndianCorrection)
				Array.Reverse(bytes);
		}
	}
}