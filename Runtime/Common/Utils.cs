using System.IO;
using System.IO.Compression;
using System.Net.Sockets;
using System.Net;
using System.Runtime.Serialization.Formatters.Binary;
using System;
using UnityEngine;

public class Utils {
    public class GZip {
        public static byte[] Compress(byte[] data) {
            using (MemoryStream compressedStream = new MemoryStream()) {
                using (GZipStream gzipStream = new GZipStream(compressedStream, CompressionMode.Compress)) {
                    gzipStream.Write(data, 0, data.Length);
                }

                return compressedStream.ToArray();
            }
        }

        public static byte[] Decompress(byte[] compressedData) {
            using (MemoryStream decompressedStream = new MemoryStream()) {
                using (MemoryStream compressedStream = new MemoryStream(compressedData)) {
                    using (GZipStream gzipStream = new GZipStream(compressedStream, CompressionMode.Decompress)) {
                        gzipStream.CopyTo(decompressedStream);
                    }
                }

                return decompressedStream.ToArray();
            }
        }
    }

    public class Bytes {
        public static byte[] FloatsToBytes(float[] floats) {
            int byteCount = sizeof(float) * floats.Length;
            byte[] byteArray = new byte[byteCount];

            Buffer.BlockCopy(floats, 0, byteArray, 0, byteCount);

            return byteArray;
        }

        public static float[] BytesToFloats(byte[] bytes) {
            int floatCount = bytes.Length / sizeof(float);
            float[] floatArray = new float[floatCount];

            Buffer.BlockCopy(bytes, 0, floatArray, 0, bytes.Length);

            return floatArray;
        }

        public static byte[] ToByteArray<T>(T obj) {
            if (obj == null)
                return null;
            BinaryFormatter bf = new BinaryFormatter();
            using (MemoryStream ms = new MemoryStream()) {
                bf.Serialize(ms, obj);
                return ms.ToArray();
            }
        }

        public static T FromByteArray<T>(byte[] data) {
            if (data == null)
                return default;
            BinaryFormatter bf = new BinaryFormatter();
            using (MemoryStream ms = new MemoryStream(data)) {
                object obj = bf.Deserialize(ms);
                return (T)obj;
            }
        }
    }

    public static class Audio {
        static float[] audioF;
        static float sumOfSquares;
        public static float CalculateRMS(byte[] audio) {
            audioF = Bytes.BytesToFloats(audio);

            foreach(var x in audioF)
                sumOfSquares += x * x;
            return Mathf.Sqrt(sumOfSquares / audioF.Length);
        }
    }

    public static class Network {
        public static string LocalIPv4Address {
            get {
                using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0)) {
                    socket.Connect("8.8.8.8", 65530);
                    IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
                    return endPoint.Address.ToString();
                }
            }
        }
    }
}
