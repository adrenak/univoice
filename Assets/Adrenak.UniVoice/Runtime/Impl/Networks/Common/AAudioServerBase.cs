using System;
using System.Collections.Generic;
using System.Linq;
using Adrenak.BRW;
using UnityEngine;

namespace Adrenak.UniVoice.Networks
{
    /// <summary>
    /// Network-agnostic base for <see cref="IAudioServer{T}"/> implementations.
    /// Owns the client roster and per-client <see cref="VoiceSettings"/>, applies
    /// mute/deafen filtering, and re-broadcasts <see cref="AudioBroadcastTags.AUDIO_FRAME"/>
    /// payloads to the appropriate recipients.
    /// <para>
    /// Constrained to <c>int</c> client ids because <see cref="VoiceSettings"/> itself stores
    /// mute/deafen lists as <c>List&lt;int&gt;</c>. Frameworks whose native id type is not
    /// <c>int</c> (e.g. PurrNet's <c>PlayerID</c>) convert at the boundary before calling
    /// <see cref="HandleClientConnected"/> / <see cref="HandleClientDisconnected"/> /
    /// <see cref="HandleIncomingPayload"/>.
    /// </para>
    /// </summary>
    public abstract class AAudioServerBase : IAudioServer<int>
    {
        public event Action OnServerStart;
        public event Action OnServerStop;
        public event Action OnClientVoiceSettingsUpdated;

        public List<int> ClientIDs { get; private set; } = new();
        public Dictionary<int, VoiceSettings> ClientVoiceSettings { get; private set; } = new();

        protected abstract string Tag { get; }
        protected abstract void SendToClient(int clientId, byte[] data, bool reliable);

        public virtual void Dispose()
        {
            RaiseServerStopped();
        }

        protected void RaiseServerStarted()
        {
            OnServerStart?.Invoke();
        }

        protected void RaiseServerStopped()
        {
            ClientIDs.Clear();
            ClientVoiceSettings.Clear();
            OnServerStop?.Invoke();
        }

        protected void HandleClientConnected(int clientId)
        {
            if (ClientIDs.Contains(clientId)) return;
            ClientIDs.Add(clientId);
            Debug.unityLogger.Log(LogType.Log, Tag,
                $"Client {clientId} connected. IDs now: {string.Join(", ", ClientIDs)}");
            OnAfterClientConnected(clientId);
        }

        protected void HandleClientDisconnected(int clientId)
        {
            if (!ClientIDs.Remove(clientId)) return;
            ClientVoiceSettings.Remove(clientId);
            Debug.unityLogger.Log(LogType.Log, Tag,
                $"Client {clientId} disconnected. IDs now: {string.Join(", ", ClientIDs)}");
            OnAfterClientDisconnected(clientId);
        }

        /// <summary>
        /// Hook for backends that need to push state to peers when a client connects
        /// (e.g. Mirror's PEER_INIT / PEER_JOINED notifications). Default is no-op.
        /// <see cref="ClientIDs"/> already contains <paramref name="clientId"/> when this fires.
        /// </summary>
        protected virtual void OnAfterClientConnected(int clientId) { }

        /// <summary>
        /// Hook for backends that need to push state to peers when a client disconnects
        /// (e.g. Mirror's PEER_LEFT notification). Default is no-op.
        /// <paramref name="clientId"/> has already been removed from <see cref="ClientIDs"/>
        /// when this fires.
        /// </summary>
        protected virtual void OnAfterClientDisconnected(int clientId) { }

        protected void HandleIncomingPayload(int clientId, byte[] data)
        {
            var reader = new BytesReader(data);
            var tag = reader.ReadString();

            switch (tag)
            {
                case AudioBroadcastTags.AUDIO_FRAME: 
                    ForwardAudioFrame(clientId, data); 
                    break;
                case AudioBroadcastTags.VOICE_SETTINGS: 
                    UpdateClientVoiceSettings(clientId, reader);
                    break;
            }
        }

        private void ForwardAudioFrame(int senderId, byte[] data)
        {
            var recipients = ClientIDs.Where(x => x != senderId);

            ClientVoiceSettings.TryGetValue(senderId, out var senderSettings);
            if (senderSettings != null)
            {
                // Sender has deafened everyone — drop the frame entirely.
                if (senderSettings.deafenAll) return;

                // Drop recipients the sender has deafened by id.
                recipients = recipients.Where(x => !senderSettings.deafenedPeers.Contains(x));

                // Drop recipients the sender has deafened by tag.
                recipients = recipients.Where(peer =>
                {
                    if (!ClientVoiceSettings.TryGetValue(peer, out var peerVoiceSettings))
                        return true;
                    return !senderSettings.deafenedTags.Intersect(peerVoiceSettings.myTags).Any();
                });
            }

            foreach (var recipient in recipients)
            {
                if (ClientVoiceSettings.TryGetValue(recipient, out var recipientSettings))
                {
                    if (recipientSettings.muteAll) continue;
                    if (recipientSettings.mutedPeers.Contains(senderId)) continue;
                    if (senderSettings != null &&
                        recipientSettings.mutedTags.Intersect(senderSettings.myTags).Any())
                        continue;
                }
                SendToClient(recipient, data, reliable: false);
            }
        }

        private void UpdateClientVoiceSettings(int clientId, BytesReader reader)
        {
            var muteAll = reader.ReadInt() == 1;
            var mutedPeers = reader.ReadIntArray().ToList();
            var deafenAll = reader.ReadInt() == 1;
            var deafenedPeers = reader.ReadIntArray().ToList();
            var myTags = reader.ReadStringArray().ToList();
            var mutedTags = reader.ReadStringArray().ToList();
            var deafenedTags = reader.ReadStringArray().ToList();

            ClientVoiceSettings[clientId] = new VoiceSettings
            {
                muteAll = muteAll,
                mutedPeers = mutedPeers,
                deafenAll = deafenAll,
                deafenedPeers = deafenedPeers,
                myTags = myTags,
                mutedTags = mutedTags,
                deafenedTags = deafenedTags
            };
            OnClientVoiceSettingsUpdated?.Invoke();
        }
    }
}
