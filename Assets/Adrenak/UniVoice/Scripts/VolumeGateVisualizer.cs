using UnityEngine;

namespace Adrenak.UniVoice {
	public class VolumeGateVisualizer : MonoBehaviour {
		public VolumeGate gate;

		VolumeGateVisualizer() { }

		public static VolumeGateVisualizer New(VolumeGate gate) {
			var instance = new GameObject("VolumeGateVisualizer").AddComponent<VolumeGateVisualizer>();
			instance.gate = gate;
			return instance;
		}

		public void OnDrawGizmos() {
			if (gate == null) return;

			if (gate.History == null || gate.History.Count == 0) return;

			for (int i = 0; i < gate.History.Count - 1; i++) {
				var one = gate.History[i];
				var two = gate.History[i + 1];
				Gizmos.color = Color.blue;
				Gizmos.DrawLine(new Vector3(i, one * 100, 0), new Vector3(i + 1, two * 100, 0));
				Gizmos.color = Color.black;
				Gizmos.DrawSphere(new Vector3(i, one * 100, 0), 1f);
			}
			Gizmos.color = Color.red;
			Gizmos.DrawLine(new Vector3(0, gate.Threshold * 100, 0), new Vector3(gate.History.Count, gate.Threshold * 100, 0));
		}
	}
}
