using UnityEngine;

namespace Adrenak.UniStream {
	public class Example : MonoBehaviour {
		void Start() {
			var writer = new UniStreamWriter();
			writer.WriteInt(90);
			writer.WriteString("Vatsal");
			writer.WriteString("Adrenak");
			writer.WriteString("Ambastha");
			writer.WriteVector3(Vector3.one);

			var reader = new UniStreamReader(writer.Bytes);

			// READ IN THE SAME ORDER
			Debug.Log(reader.ReadInt());
			Debug.Log(reader.ReadString());
			Debug.Log(reader.ReadString());
			Debug.Log(reader.ReadString());
			Debug.Log(reader.ReadVector3());
		}
	}
}
