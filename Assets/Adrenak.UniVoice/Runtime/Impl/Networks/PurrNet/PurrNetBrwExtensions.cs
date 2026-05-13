// Author: František Holubec
// Created: 14.05.2026

using System;
using Adrenak.BRW;
using PurrNet;

namespace Adrenak.UniVoice.Networks
{
    public static class PurrNetBrwExtensions
    {
        public static void WritePlayerID(this BytesWriter writer, PlayerID id)
        {
            var bytes = BitConverter.GetBytes(id.id);
            EndianUtility.EndianCorrection(bytes);
            writer.WriteBytes(bytes);
            writer.WriteByte(id.isBot ? (byte) 1 : (byte) 0);
        }

        public static PlayerID ReadPlayerID(this BytesReader reader)
        {
            var idBytes = reader.ReadBytes(8);
            var isBot = reader.ReadBytes(1)[0] == 1;
            EndianUtility.EndianCorrection(idBytes);
            return new PlayerID(BitConverter.ToUInt64(idBytes, 0), isBot);
        }
    }
}
