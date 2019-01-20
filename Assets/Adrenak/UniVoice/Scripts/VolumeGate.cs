using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace Adrenak.UniVoice {
	public class VolumeGate {
		public float Threshold { get; private set; }
		public int HistoryLength { get; private set; }
		public float Sensitivity { get; private set; }
		public int Tolerance { get; private set; }

		public List<float> History { get; private set; }

		/// <summary>
		/// Constructs an instance
		/// </summary>
		/// <param name="historyLength">
		/// The number of segment samples the gate should consider. 
		/// Keep it to a large number, equivalent a certain duration of time in seconds.
		/// Suggested : 30
		/// </param>
		/// <param name="sensitivity">
		/// Noise sensitivity of the gate. 
		/// High values filter out more segments and remove more ambient sound but may lead to parts of low volume speech being filtered away.
		/// Low values filter out less segments, are less effective in filtering ambient sound but prevent low volume speech from being dropped.
		/// </param>
		/// <param name="tolerance">
		/// The number of consecutive segments than can be below the calculated threshold but still allowed to be transmitted.
		/// This is because sometimes the volume of the speaker may drop below threshold while speaking and we don't want that to be skipped.
		/// At the same time, setting the tolerance too high will result in silence in speech, such as pauses between sentences to not be detected.
		/// Suggested value: equivalent to less than 500 ms
		/// </param>
		/// <returns></returns>
		public VolumeGate(int historyLength, float sensitivity, int tolerance) {
			sensitivity = Mathf.Clamp01(sensitivity);

			History = new List<float>();
			HistoryLength = historyLength;
			Threshold = 0;
			Sensitivity = sensitivity;
			Tolerance = tolerance;
		}

		int belowCount;
		/// <summary>
		/// Accepts a segments and returns if it should be transmitted.
		/// </summary>
		/// <param name="segment">The audio segment to be evaluated</param>
		public bool Evaluate(float[] segment) {
			// Add the volume to history and limit its length
			var peak = segment.Max();
			History.Add(peak);
			if (History.Count > HistoryLength)
				History.RemoveAt(0);

			var orderedHistory = History.OrderByDescending(x => x).ToList();
			float weightedSum = 0;
			float sumOfWeights = 0;
			for(int i = 0; i < orderedHistory.Count; i ++) {
				weightedSum += orderedHistory[i] * i;
				sumOfWeights += i;
			}
			Threshold = weightedSum / sumOfWeights * Sensitivity;

			var isBelow = peak < Threshold;
			if (isBelow) belowCount++;
			else belowCount = 0;

			var shouldNotTransmit = belowCount > Tolerance;
			return !shouldNotTransmit;
		}
	}
}
