using System;
using System.IO;
using System.Text;

/*
Example script for usage with UniMic. Just add it to a scene,
play and then quit/exit play mode:

public class WavFileWriterTest : MonoBehaviour {
    WavFileWriter writer;

    void Start() {
        string path = string.Empty;
        if (Application.isEditor)
            path = Application.dataPath.Replace("Assets", "output.wav");
        else
            path = Path.Combine(Application.persistentDataPath, "output.wav");

        writer = new WavFileWriter(path);

        Mic.Init();

        Mic.AvailableDevices[0].OnFrameCollected += OnFrameCollected;
        Mic.AvailableDevices[0].StartRecording(20);
    }

    private void OnFrameCollected(int arg1, int arg2, float[] arg3) {
        writer.Write(arg1, arg2, arg3);
    }

    private void OnDestroy() {
        writer.Dispose();
    }
}
*/

namespace Adrenak.UniVoice {
    /// <summary>
    /// A utility to write audio samples to a file on disk. 
    /// Construct using the path you want to store the audio file at.
    /// Invoke Write with the sampling frequency, channel count and PCM samples
    /// and it will lazily initialize.
    /// </summary>
    public class WavFileWriter : IDisposable {
        FileStream fileStream;
        int sampleRate;
        short channels;
        readonly short bitsPerSample = 16;

        long dataChunkPos;
        int totalSampleCount = 0;
        bool isInitialized = false;
        readonly string path;

        public WavFileWriter(string path) {
            this.path = path;
        }

        public void Write(int frequency, int channelCount, float[] samples) {
            if (!isInitialized) {
                sampleRate = frequency;
                channels = (short)channelCount;
                fileStream = new FileStream(path, FileMode.Create, FileAccess.Write);
                WriteWavHeader();
                isInitialized = true;
            }
            else {
                if (frequency != sampleRate || channelCount != channels)
                    throw new InvalidOperationException("Inconsistent frequency or channel count between calls.");
            }

            byte[] buffer = new byte[samples.Length * 2]; // 2 bytes per sample (16-bit PCM)
            for (int i = 0; i < samples.Length; i++) {
                short intSample = (short)Math.Clamp(samples[i] * short.MaxValue, short.MinValue, short.MaxValue);
                buffer[i * 2] = (byte)(intSample & 0xff);
                buffer[i * 2 + 1] = (byte)((intSample >> 8) & 0xff);
            }

            fileStream.Write(buffer, 0, buffer.Length);
            totalSampleCount += samples.Length;
        }

        void WriteWavHeader() {
            var writer = new BinaryWriter(fileStream, Encoding.ASCII, true);

            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(0); // placeholder for file size
            writer.Write(Encoding.ASCII.GetBytes("WAVE"));

            // fmt chunk
            writer.Write(Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16); // PCM header size
            writer.Write((short)1); // PCM format
            writer.Write(channels);
            writer.Write(sampleRate);
            int byteRate = sampleRate * channels * bitsPerSample / 8;
            writer.Write(byteRate);
            short blockAlign = (short)(channels * bitsPerSample / 8);
            writer.Write(blockAlign);
            writer.Write(bitsPerSample);

            // data chunk
            writer.Write(Encoding.ASCII.GetBytes("data"));
            dataChunkPos = fileStream.Position;
            writer.Write(0); // placeholder for data chunk size
        }

        public void Dispose() {
            if (!isInitialized) return;

            long dataSize = totalSampleCount * bitsPerSample / 8;

            fileStream.Seek(dataChunkPos, SeekOrigin.Begin);
            fileStream.Write(BitConverter.GetBytes((int)dataSize), 0, 4);

            fileStream.Seek(4, SeekOrigin.Begin);
            int fileSize = (int)(fileStream.Length - 8);
            fileStream.Write(BitConverter.GetBytes(fileSize), 0, 4);

            fileStream.Dispose();
            isInitialized = false;
        }
    }
}
