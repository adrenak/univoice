using System;

namespace Adrenak.UniVoice {
    /// <summary>
    /// A minimal, adaptive voice activity detector operating on time-domain PCM.
    /// Supports float [-1,1] and 16-bit samples, with per-call adaptation to
    /// input frequency and channel count. Multi-channel input is downmixed to mono.
    /// </summary>
    /// <remarks>
    /// The detector emits <see cref="OnVadChanged"/> when the speaking state toggles.
    /// Timings (attack, release, gaps) are maintained in milliseconds and remain
    /// stable across sample-rate changes.
    /// </remarks>
    public class SimpleVad {
        /// <summary>
        /// Configuration for the MiniVad voice activity detector.
        /// All time-based parameters are expressed in milliseconds.
        /// </summary>
        [Serializable]
        public class Config {
            /// <summary>
            /// Target analysis frame duration in milliseconds. The frame sample count
            /// is computed from the current input frequency each call.
            /// </summary>
            public int TargetFrameMs = 20;

            /// <summary>
            /// Minimum continuous speech duration required to enter the speaking state.
            /// </summary>
            public int AttackMs = 20;

            /// <summary>
            /// Minimum continuous silence duration required to exit the speaking state.
            /// </summary>
            public int ReleaseMs = 1000;

            /// <summary>
            /// SNR threshold in decibels used to enter the speaking state.
            /// Higher values make entry stricter.
            /// </summary>
            public float SnrEnterDb = 8f;

            /// <summary>
            /// SNR threshold in decibels used to remain in the speaking state.
            /// </summary>
            public float SnrExitDb = 4f;

            /// <summary>
            /// Maximum tolerated duration of consecutive quiet frames while already speaking.
            /// </summary>
            public int MaxGapMs = 300;

            /// <summary>
            /// Grace period after speech onset during which release is disallowed.
            /// </summary>
            public int NoDropWindowMs = 400;

            /// <summary>
            /// Noise-floor update rate (EMA alpha) during non-speech.
            /// </summary>
            public float NonSpeechNoiseUpdateRate = 0.01f;

            /// <summary>
            /// Maximum noise-floor update rate (EMA alpha) during speech.
            /// </summary>
            public float SpeechNoiseUpdateRate = 0.002f;

            /// <summary>
            /// The minimum allowed value for the estimated noise level (RMS).
            /// Prevents the noise estimate from collapsing toward zero, which
            /// would make SNR calculations unstable or excessively large.
            /// </summary>
            public float MinNoiseRms = 1e-5f;

            /// <summary>
            /// Energy floor used to clamp extremely low RMS values.
            /// </summary>
            public float EnergyFloor = 1e-5f;
        }

        /// <summary>
        /// Raised when the VAD speaking state changes.
        /// The event argument is true when entering the speaking state,
        /// and false when exiting.
        /// </summary>
        public event Action<bool> OnVadChanged;

        /// <summary>
        /// Current speaking state.
        /// </summary>
        public bool IsSpeaking { get; private set; }

        private readonly Config _config;

        // Temporary buffer used to collect samples until one full frame is ready.
        private float[] _frameBuf;

        // Current fill position within the frame buffer.
        private int _frameFill;

        // Sample rate currently used for frame geometry.
        private int _curSampleRate = -1;

        // Cached frame size in samples for the current sample rate.
        private int _frameSamples = 0;

        // Duration (ms) of a single analysis frame at the current sample rate.
        private float _frameDurationMs = 0f;

        // Current adaptive noise level estimate (RMS). Updated every frame via EMA.
        private float _noiseRms;

        // Small constant added to denominators to avoid log(0) or division by zero.
        private readonly float _eps = 1e-12f;

        // Time (ms) of continuous speech detected so far.
        private float _speechMs;

        // Time (ms) of continuous silence detected so far.
        private float _silenceMs;

        // Time (ms) since the most recent transition into the speaking state.
        private float _sinceOnsetMs;

        // Accumulated quiet period (ms) while still considered speaking.
        private float _gapMs;

        // Warm-up frames during which we only learn noise and disallow onset.
        private int _warmupFrames = 0;

        /// <summary>
        /// Initializes a new instance of <see cref="SimpleVad"/>.
        /// </summary>
        /// <param name="config">Optional configuration. If null, defaults are used.</param>
        public SimpleVad(Config config = null) {
            _config = config ?? new Config();
            _noiseRms = Math.Max(_config.MinNoiseRms, 5e-3f);
            IsSpeaking = false;
        }

        /// <summary>
        /// Ensures internal frame geometry matches the provided frequency.
        /// Recomputes the frame sample count and resets partial-frame state when changed.
        /// </summary>
        /// <param name="frequency">Input sample rate in Hz.</param>
        private void EnsureGeometry(int frequency) {
            if (frequency == _curSampleRate && _frameBuf != null) return;

            _curSampleRate = frequency;

            // Choose frame sample count from target frame duration
            int frameSamples = Math.Max(80, (_curSampleRate * _config.TargetFrameMs) / 1000);

            // Recompute warm-up frames for the new rate: ~200 ms of noise learning
            _warmupFrames = Math.Max(1, (int)Math.Ceiling(200.0 / _config.TargetFrameMs));

            if (frameSamples != _frameSamples || _frameBuf == null) {
                _frameSamples = frameSamples;
                _frameBuf = new float[_frameSamples];
                _frameFill = 0;
            }

            _frameDurationMs = 1000f * _frameSamples / (float)_curSampleRate;

            // When geometry changes, reset streaming timers so old partials don't leak across rates
            _speechMs = 0f;
            _silenceMs = 0f;
            _sinceOnsetMs = IsSpeaking ? 0f : _sinceOnsetMs; // safe reset on onset timing
            _gapMs = 0f;

            _noiseRms = Math.Max(_noiseRms, Math.Max(_config.MinNoiseRms, 5e-3f));
        }

        /// <summary>
        /// Resets internal state and timers.
        /// </summary>
        /// <param name="isSpeaking">Initial speaking state after reset.</param>
        public void Reset(bool isSpeaking = false) {
            if (_frameBuf != null)
                Array.Clear(_frameBuf, 0, _frameBuf.Length);
            _frameFill = 0;
            _speechMs = 0f;
            _silenceMs = 0f;
            _sinceOnsetMs = 0f;
            _gapMs = 0f;
            _noiseRms = 5e-3f;
            IsSpeaking = isSpeaking;
        }

        /// <summary>
        /// Processes interleaved float PCM in the range [-1, 1] with adaptive
        /// handling of frequency and channels. Multi-channel input is downmixed
        /// to mono via averaging.
        /// </summary>
        /// <param name="frequency">Input sample rate in Hz.</param>
        /// <param name="channels">Number of interleaved channels. If 0, treated as 1 (mono).</param>
        /// <param name="samples">Buffer containing interleaved sample data.</param>
        /// <param name="count">Number of elements from <paramref name="samples"/> to process.</param>
        public void Process(int frequency, int channels, float[] samples, int count) {
            if (samples == null || count <= 0) return;
            if (channels <= 0) channels = 1;

            // Reconfigure frame geometry if sample rate changed or not initialized
            EnsureGeometry(frequency);

            // Consume 'count' values which represent count/channels mono samples
            int usable = (count / channels) * channels; // ignore any trailing partial
            int idx = 0;

            while (idx < usable) {
                // Downmix one interleaved multi-channel sample to mono
                float sum = 0f;
                for (int c = 0; c < channels; c++) {
                    sum += samples[idx + c];
                }
                float mono = sum / channels;
                idx += channels;

                _frameBuf[_frameFill++] = mono;

                if (_frameFill == _frameBuf.Length) {
                    ProcessOneFrame(_frameBuf);
                    _frameFill = 0;
                }
            }
        }

        /// <summary>
        /// Processes interleaved 16-bit PCM with adaptive handling of frequency and channels.
        /// Multi-channel input is downmixed to mono via averaging.
        /// </summary>
        /// <param name="frequency">Input sample rate in Hz. If 0, a default is used.</param>
        /// <param name="channels">Number of interleaved channels. If 0, treated as 1.</param>
        /// <param name="samples">Buffer containing interleaved sample data.</param>
        /// <param name="count">Number of elements from <paramref name="samples"/> to process.</param>
        public void Process(int frequency, int channels, short[] samples, int count) {
            if (samples == null || count <= 0) return;
            if (channels <= 0) channels = 1;

            EnsureGeometry(frequency);

            int usable = (count / channels) * channels;
            int idx = 0;

            while (idx < usable) {
                int baseIdx = idx;
                float sum = 0f;
                for (int c = 0; c < channels; c++) {
                    sum += samples[baseIdx + c] / 32768f;
                }
                float mono = sum / channels;
                idx += channels;

                _frameBuf[_frameFill++] = mono;

                if (_frameFill == _frameBuf.Length) {
                    ProcessOneFrame(_frameBuf);
                    _frameFill = 0;
                }
            }
        }

        /// <summary>
        /// Convenience overload for processing fully-filled float buffers.
        /// </summary>
        /// <param name="frequency">Input sample rate in Hz.</param>
        /// <param name="channels">Number of interleaved channels.</param>
        /// <param name="samples">Buffer containing interleaved sample data.</param>
        public void Process(int frequency, int channels, float[] samples)
            => Process(frequency, channels, samples, samples?.Length ?? 0);

        /// <summary>
        /// Convenience overload for processing fully-filled 16-bit buffers.
        /// </summary>
        /// <param name="frequency">Input sample rate in Hz.</param>
        /// <param name="channels">Number of interleaved channels.</param>
        /// <param name="samples">Buffer containing interleaved sample data.</param>
        public void Process(int frequency, int channels, short[] samples)
            => Process(frequency, channels, samples, samples?.Length ?? 0);

        /// <summary>
        /// Processes a single analysis frame and updates the speaking state.
        /// </summary>
        /// <param name="frame">Mono frame of length equal to the current frame size.</param>
        private void ProcessOneFrame(float[] frame) {
            // --- Energy / RMS ---
            double sumSq = 0;
            for (int i = 0; i < frame.Length; i++) {
                float s = frame[i];
                sumSq += (double)s * s;
            }
            float rms = (float)Math.Sqrt(sumSq / frame.Length);
            rms = Math.Max(rms, _config.EnergyFloor);

            // --- SNR(dB) vs noise floor ---
            float noise = Math.Max(_noiseRms, _config.MinNoiseRms);
            float snrDb = 20f * (float)Math.Log10((rms + _eps) / (noise + _eps));

            // Pick threshold depending on current state (hysteresis)
            float threshold = IsSpeaking ? _config.SnrExitDb : _config.SnrEnterDb;
            bool rawSpeech = (snrDb >= threshold) && (rms > _config.EnergyFloor);

            // Noise EMA: slow during speech, faster during non-speech
            float alpha = rawSpeech ? _config.SpeechNoiseUpdateRate : _config.NonSpeechNoiseUpdateRate;
            _noiseRms = (1f - alpha) * _noiseRms + alpha * rms;
            _noiseRms = Math.Max(_noiseRms, _config.MinNoiseRms);

            // During warm-up: do not allow entering speaking state.
            // Keep learning noise using the non-speech update rate feel.
            if (_warmupFrames > 0) {
                _warmupFrames--;

                // Treat this frame as "effective silence" for timers.
                // (We still did the EMA update above, so noise keeps adapting.)
                _silenceMs += _frameDurationMs;
                _speechMs = 0f;

                // Do not change speaking state during warm-up.
                return;
            }

            // --- Gap filling: allow brief quiet while speaking ---
            if (IsSpeaking) {
                if (rawSpeech) _gapMs = 0f;
                else _gapMs += _frameDurationMs;
            }
            else {
                _gapMs = 0f;
            }

            // Effective speech used for timers/state:
            bool effectiveSpeech = rawSpeech || (IsSpeaking && _gapMs <= _config.MaxGapMs);

            // --- Time-based hangover & no-drop window ---
            if (effectiveSpeech) {
                _speechMs += _frameDurationMs;
                _silenceMs = 0f;
            }
            else {
                _silenceMs += _frameDurationMs;
                _speechMs = 0f;
            }

            bool newIsSpeaking = IsSpeaking;

            // Enter speaking after AttackMs of continuous effectiveSpeech
            if (!IsSpeaking && _speechMs >= _config.AttackMs) {
                newIsSpeaking = true;
                _sinceOnsetMs = 0f; // reset onset timer when we flip on
                _gapMs = 0f;
            }

            // Update onset timer if speaking
            if (newIsSpeaking) _sinceOnsetMs += _frameDurationMs;

            // Exit only if:
            //  1) we've accumulated ReleaseMs of effective silence AND
            //  2) we're past the initial NoDropWindow
            if (IsSpeaking && _silenceMs >= _config.ReleaseMs && _sinceOnsetMs >= _config.NoDropWindowMs) {
                newIsSpeaking = false;
            }

            if (newIsSpeaking != IsSpeaking) {
                IsSpeaking = newIsSpeaking;
                OnVadChanged?.Invoke(IsSpeaking);
                // reset timers appropriately
                if (IsSpeaking) {
                    _sinceOnsetMs = 0f;
                    _gapMs = 0f;
                }
                else {
                    _silenceMs = 0f;
                    _speechMs = 0f;
                    _gapMs = 0f;
                }
            }
        }
    }
}
