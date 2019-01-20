using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Adrenak.UniMic {
	[RequireComponent(typeof(AudioSource))]
	public class Mic : MonoBehaviour {
		// ================================================
		// FIELDS
		// ================================================
		#region MEMBERS
		/// <summary>
		/// Whether the microphone is running
		/// </summary>
		public bool IsRecording { get; private set; }

		/// <summary>
		/// The frequency at which the mic is operating
		/// </summary>
		public int Frequency { get; private set; }

		/// <summary>
		/// Last populated audio sample
		/// </summary>
		public float[] Sample { get; private set; }

		/// <summary>
		/// Sample duration/length in milliseconds
		/// </summary>
		public int SampleDurationMS { get; private set; }

		/// <summary>
		/// The length of the sample float array
		/// </summary>
		public int SampleLen {
			get { return Frequency * SampleDurationMS / 1000; }
		}

		/// <summary>
		/// The AudioClip currently being streamed in the Mic
		/// </summary>
		public AudioClip Clip { get; private set; }

		/// <summary>
		/// List of all the available Mic devices
		/// </summary>
		public List<string> Devices { get; private set; }

		/// <summary>
		/// Index of the current Mic device in m_Devices
		/// </summary>
		public int CurrentDeviceIndex { get; private set; }

		/// <summary>
		/// Gets the name of the Mic device currently in use
		/// </summary>
		public string CurrentDeviceName {
			get { return Devices[CurrentDeviceIndex]; }
		}

		AudioSource m_AudioSource;      // Plays the audio clip at 0 volume to get spectrum data
		int m_SampleCount = 0;
		#endregion

		// ================================================
		// EVENTS
		// ================================================
		#region EVENTS
		/// <summary>
		/// Invoked when the instance starts Recording.
		/// </summary>
		public event Action OnStartRecording;

		/// <summary>
		/// Invoked everytime an audio frame is collected. Includes the frame.
		/// </summary>
		public event Action<int, float[]> OnSampleReady;

		/// <summary>
		/// Invoked when the instance stop Recording.
		/// </summary>
		public event Action OnStopRecording;
		#endregion

		// ================================================
		// METHODS
		// ================================================
		#region METHODS

		static Mic m_Instance;
		public static Mic Instance {
			get {
				if (m_Instance == null)
					m_Instance = GameObject.FindObjectOfType<Mic>();
				if (m_Instance == null) {
					m_Instance = new GameObject("UniMic.Mic").AddComponent<Mic>();
					DontDestroyOnLoad(m_Instance.gameObject);
				}
				return m_Instance;
			}
		}

		void Awake() {
			m_AudioSource = GetComponent<AudioSource>();

			Devices = new List<string>();
			foreach (var device in Microphone.devices)
				Devices.Add(device);
			CurrentDeviceIndex = 0;
		}

		void Update() {
			if (m_AudioSource == null)
				m_AudioSource = gameObject.AddComponent<AudioSource>();

			m_AudioSource.mute = true;
			m_AudioSource.loop = true;
			m_AudioSource.maxDistance = m_AudioSource.minDistance = 0;
			m_AudioSource.spatialBlend = 0;

			if (IsRecording && !m_AudioSource.isPlaying)
				m_AudioSource.Play();
		}

		/// <summary>
		/// Changes to a Mic device for Recording
		/// </summary>
		/// <param name="index">The index of the Mic device. Refer to <see cref="Devices"/></param>
		public void ChangeDevice(int index) {
			Microphone.End(CurrentDeviceName);
			CurrentDeviceIndex = index;
			Microphone.Start(CurrentDeviceName, true, 1, Frequency);
		}

		/// <summary>
		/// Starts to stream the input of the current Mic device
		/// </summary>
		public void StartRecording(int frequency = 16000, int sampleLen = 10) {
			StopRecording();
			IsRecording = true;

			Frequency = frequency;
			SampleDurationMS = sampleLen;

			Clip = Microphone.Start(CurrentDeviceName, true, 1, Frequency);
			Sample = new float[Frequency / 1000 * SampleDurationMS * Clip.channels];

			m_AudioSource.clip = Clip;

			StartCoroutine(ReadRawAudio());

			if (OnStartRecording != null)
				OnStartRecording.Invoke();
		}

		/// <summary>
		/// Ends the Mic stream.
		/// </summary>
		public void StopRecording() {
			if (!Microphone.IsRecording(CurrentDeviceName)) return;

			IsRecording = false;

			Microphone.End(CurrentDeviceName);
			Destroy(Clip);
			Clip = null;
			m_AudioSource.Stop();

			StopCoroutine(ReadRawAudio());

			if (OnStopRecording != null)
				OnStopRecording.Invoke();
		}

		/// <summary>
		/// Gets the current audio spectrum
		/// </summary>
		/// <param name="fftWindow">The <see cref="FFTWindow"/> type used to create the spectrum.</param>
		/// <param name="sampleCount">The number of samples required in the output. Use POT numbers</param>
		/// <returns></returns>
		public float[] GetSpectrumData(FFTWindow fftWindow, int sampleCount) {
			var spectrumData = new float[sampleCount];
			try {
				m_AudioSource.GetSpectrumData(spectrumData, 0, fftWindow);
			}
			catch (NullReferenceException e) {
				spectrumData = null;
			}
			return spectrumData;
		}

		/// <summary>
		/// Calls .GetOutputData on the inner audiosource
		/// </summary>
		public float[] GetOutputData(int sampleCount) {
			var data = new float[sampleCount];
			m_AudioSource.GetOutputData(data, 0);
			return data;
		}

		IEnumerator ReadRawAudio() {
			int loops = 0;
			int readAbsPos = 0;
			int prevPos = 0;
			float[] temp = new float[Sample.Length];

			while (Clip != null && Microphone.IsRecording(CurrentDeviceName)) {
				bool isNewDataAvailable = true;

				while (isNewDataAvailable) {
					int currPos = Microphone.GetPosition(CurrentDeviceName);
					if (currPos < prevPos)
						loops++;
					prevPos = currPos;

					var currAbsPos = loops * Clip.samples + currPos;
					var nextReadAbsPos = readAbsPos + temp.Length;

					if (nextReadAbsPos < currAbsPos) {
						Clip.GetData(temp, readAbsPos % Clip.samples);

						Sample = temp;
						m_SampleCount++;
						if (OnSampleReady != null)
							OnSampleReady.Invoke(m_SampleCount, Sample);

						readAbsPos = nextReadAbsPos;
						isNewDataAvailable = true;
					}
					else
						isNewDataAvailable = false;
				}
				yield return null;
			}
		}
		#endregion
	}
}