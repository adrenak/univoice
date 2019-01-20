using System;
using Byn.Net;
using System.Text;

namespace Adrenak.AirPeer {
    public static class Extensions {
        public static string GetDataAsString(this NetworkEvent netEvent) {
            if (netEvent.MessageData == null) return netEvent.Type.ToString();
            var bytes = netEvent.GetDataAsByteArray();
            return Encoding.UTF8.GetString(bytes, 0, bytes.Length);
        }

        public static T[] Concat<T>(this T[] arr1, T[] arr2) {
            var result = new T[arr1.Length + arr2.Length];
            arr1.CopyTo(result, 0);
            arr2.CopyTo(result, arr1.Length);
            return result;
        }

        public static T[] ToArray<T>(this T singleObject) {
            return new T[] { singleObject };
        }

        public static void TryInvoke(this Action action) {
            if (action != null) action();
        }

        public static void TryInvoke<T>(this Action<T> action, T param) {
            if (action != null) action(param);
        }

        public static void TryInvoke<T1, T2>(this Action<T1, T2> action, T1 param1, T2 param2) {
            if (action != null) action(param1, param2);
        }

        public static void TryInvoke<T1, T2, T3>(this Action<T1, T2, T3> action, T1 param1, T2 param2, T3 param3) {
            if (action != null) action(param1, param2, param3);
        }

        // ================================================
        // FROM BYTES
        // ================================================
        public static bool ToBoolean(this byte[] bytes) {
            return BitConverter.ToBoolean(bytes, 0);
        }
        
        public static short ToShort(this byte[] bytes) {
            return BitConverter.ToInt16(bytes, 0);
        }

        public static ushort ToUShort(this byte[] bytes) {
            return BitConverter.ToUInt16(bytes, 0);
        }

        public static int ToInt(this byte[] bytes) {
            return BitConverter.ToInt32(bytes, 0);
        }

        public static uint ToUInt(this byte[] bytes) {
            return BitConverter.ToUInt32(bytes, 0);
        }

        public static long ToLong(this byte[] bytes) {
            return BitConverter.ToInt64(bytes, 0);
        }

        public static ulong ToULong(this byte[] bytes) {
            return BitConverter.ToUInt64(bytes, 0);
        }

        public static float ToFloat(this byte[] bytes) {
            return BitConverter.ToSingle(bytes, 0);
        }

        public static double ToDouble(this byte[] bytes) {
            return BitConverter.ToDouble(bytes, 0);
        }

        public static char ToChar(this byte[] bytes) {
            return BitConverter.ToChar(bytes, 0);
        }

        public static string ToUTF8String(this byte[] bytes) {
            return Encoding.UTF8.GetString(bytes);
        }

        // ================================================
        // TO BYTES
        // ================================================
        public static byte[] GetBytes(this bool value) {
            return BitConverter.GetBytes(value);
        }

        public static byte[] GetBytes(this short value) {
            return BitConverter.GetBytes(value);
        }

        public static byte[] GetBytes(this ushort value) {
            return BitConverter.GetBytes(value);
        }

        public static byte[] GetBytes(this int value) {
            return BitConverter.GetBytes(value);
        }

        public static byte[] GetBytes(this uint value) {
            return BitConverter.GetBytes(value);
        }

        public static byte[] GetBytes(this long value) {
            return BitConverter.GetBytes(value);
        }

        public static byte[] GetBytes(this ulong value) {
            return BitConverter.GetBytes(value);
        }

        public static byte[] GetBytes(this float value) {
            return BitConverter.GetBytes(value);
        }

        public static byte[] GetBytes(this double value) {
            return BitConverter.GetBytes(value);
        }

        public static byte[] GetBytes(char val) {
            return BitConverter.GetBytes(val);
        }

        public static byte[] GetUTF8Bytes(string value) {
            return Encoding.UTF8.GetBytes(value);
        }
    }
}
