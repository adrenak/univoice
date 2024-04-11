using System;
using System.Collections.Generic;
using System.Linq;

using Mirror;
using Mirror.RemoteCalls;

using UnityEngine;

namespace Adrenak.UniVoice.Networks {
    //public static class NetworkServerX {
    //    public static void SendToAll<T>(T message, int connId, int channelId = Channels.Reliable, bool sendToReadyOnly = false)
    //        where T : struct, NetworkMessage {
    //        if (!NetworkServer.active) {
    //            Debug.LogWarning("Can not send using NetworkServer.SendToAll<T>(T msg) because NetworkServer is not active");
    //            return;
    //        }

    //        // Debug.Log($"Server.SendToAll {typeof(T)}");
    //        using (NetworkWriterPooled writer = NetworkWriterPool.Get()) {
    //            // pack message only once
    //            NetworkMessages.Pack(message, writer);
    //            ArraySegment<byte> segment = writer.ToArraySegment();

    //            // validate packet size immediately.
    //            // we know how much can fit into one batch at max.
    //            // if it's larger, log an error immediately with the type <T>.
    //            // previously we only logged in Update() when processing batches,
    //            // but there we don't have type information anymore.
    //            int max = NetworkMessages.MaxMessageSize(channelId);
    //            if (writer.Position > max) {
    //                Debug.LogError($"NetworkServer.SendToAll: message of type {typeof(T)} with a size of {writer.Position} bytes is larger than the max allowed message size in one batch: {max}.\nThe message was dropped, please make it smaller.");
    //                return;
    //            }

    //            // filter and then send to all internet connections at once
    //            // -> makes code more complicated, but is HIGHLY worth it to
    //            //    avoid allocations, allow for multicast, etc.
    //            int count = 0;
    //            foreach (NetworkConnectionToClient conn in NetworkServer.connections.Values) {
    //                if (sendToReadyOnly && !conn.isReady)
    //                    continue;

    //                if(conn.connectionId != connId)
    //                    continue;

    //                count++;
    //                conn.Send(segment, channelId);
    //            }

    //            NetworkDiagnostics.OnSend(message, channelId, segment.Count, count);
    //        }
    //    }
    //}
}
