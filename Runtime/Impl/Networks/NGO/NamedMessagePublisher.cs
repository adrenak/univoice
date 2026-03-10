#if UNITY_NETCODE_GAMEOBJECTS
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using Unity.Collections;
using Unity.Netcode;

using UnityEngine;

namespace Adrenak.UniVoice {
    public static class CustomMessagingManagerExtensions {
        // One NamedMessagePublisher per CustomMessagingManager instance.
        private static readonly ConditionalWeakTable<CustomMessagingManager, NamedMessagePublisher> _publishers = new();

        public static NamedMessagePublisher GetPublisher(this CustomMessagingManager messagingManager) {
            if (messagingManager == null)
                throw new ArgumentNullException(nameof(messagingManager));

            return _publishers.GetValue(
                messagingManager,
                static manager => new NamedMessagePublisher(manager));
        }
    }

    /// <summary>
    /// Allows registering multiple handles to a Netcode named message 
    /// </summary>
    public sealed class NamedMessagePublisher : IDisposable {
        private readonly CustomMessagingManager _messagingManager;

        // One NGO handler per message name.
        // That handler fans out to all local subscribers.
        private readonly Dictionary<string, MessageFanout> _fanouts = new();

        private bool _disposed;

        public NamedMessagePublisher(CustomMessagingManager messagingManager) {
            _messagingManager = messagingManager ?? throw new ArgumentNullException(nameof(messagingManager));
        }

        /// <summary>
        /// Subscribe to a named message. Multiple subscribers can listen to the same message name.
        /// Returns an IDisposable token you can dispose to unsubscribe.
        /// </summary>
        public IDisposable Subscribe(string messageName, NamedMessageHandler handler) {
            ThrowIfDisposed();

            if (string.IsNullOrWhiteSpace(messageName))
                throw new ArgumentException("Message name cannot be null or empty.", nameof(messageName));

            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            if (!_fanouts.TryGetValue(messageName, out var fanout)) {
                fanout = new MessageFanout(messageName, _messagingManager, OnLastSubscriberRemoved);
                _fanouts.Add(messageName, fanout);
            }

            return fanout.AddSubscriber(handler);
        }

        public void Unsubscribe(string messageName, NamedMessageHandler handler) {
            ThrowIfDisposed();

            if (string.IsNullOrWhiteSpace(messageName))
                throw new ArgumentException("Message name cannot be null or empty.", nameof(messageName));

            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            if (_fanouts.TryGetValue(messageName, out var fanout)) {
                fanout.RemoveSubscriber(handler);

                if (fanout.SubscriberCount == 0) {
                    _fanouts.Remove(messageName);
                }
            }
        }

        /// <summary>
        /// Unsubscribes everyone from a specific message and unregisters the NGO handler.
        /// </summary>
        public void ClearMessage(string messageName) {
            ThrowIfDisposed();

            if (_fanouts.TryGetValue(messageName, out var fanout)) {
                fanout.Dispose();
                _fanouts.Remove(messageName);
            }
        }

        /// <summary>
        /// Unsubscribes all subscribers from all messages and unregisters all NGO handlers.
        /// </summary>
        public void Dispose() {
            if (_disposed)
                return;

            _disposed = true;

            foreach (var fanout in _fanouts.Values) {
                fanout.Dispose();
            }

            _fanouts.Clear();
        }

        private void OnLastSubscriberRemoved(string messageName) {
            _fanouts.Remove(messageName);
        }

        private void ThrowIfDisposed() {
            if (_disposed)
                throw new ObjectDisposedException(nameof(NamedMessagePublisher));
        }

        public delegate void NamedMessageHandler(ulong senderClientId, FastBufferReader reader);

        private sealed class MessageFanout : IDisposable {
            private readonly string _messageName;
            private readonly CustomMessagingManager _messagingManager;
            private readonly Action<string> _onEmpty;
            private readonly List<NamedMessageHandler> _subscribers = new();

            private bool _registered;
            private bool _disposed;

            public MessageFanout(
                string messageName,
                CustomMessagingManager messagingManager,
                Action<string> onEmpty) {
                _messageName = messageName;
                _messagingManager = messagingManager;
                _onEmpty = onEmpty;

                Register();
            }

            public int SubscriberCount => _subscribers == null ? 0 : _subscribers.Count;

            public IDisposable AddSubscriber(NamedMessageHandler handler) {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(MessageFanout));

                _subscribers.Add(handler);
                return new Subscription(this, handler);
            }

            internal void RemoveSubscriber(NamedMessageHandler handler) {
                if (_disposed)
                    return;

                _subscribers.Remove(handler);

                if (_subscribers.Count == 0) {
                    Dispose();
                    _onEmpty?.Invoke(_messageName);
                }
            }

            public void Dispose() {
                if (_disposed)
                    return;

                _disposed = true;
                _subscribers.Clear();

                if (_registered) {
                    _messagingManager.UnregisterNamedMessageHandler(_messageName);
                    _registered = false;
                }
            }

            private void Register() {
                if (_registered) {
                    Debug.LogWarning($"[Fanout] '{_messageName}' already registered.");
                    return;
                }

                _messagingManager.RegisterNamedMessageHandler(_messageName, HandleMessage);
                _registered = true;
            }

            private void HandleMessage(ulong senderClientId, FastBufferReader reader) {
                if (_disposed || _subscribers.Count == 0)
                    return;

                var remaining = reader.Length - reader.Position;
                var bytes = new byte[remaining];
                if (remaining > 0)
                    reader.ReadBytesSafe(ref bytes, remaining);

                var subscribers = _subscribers.ToArray();

                foreach (var subscriber in subscribers) {
                    try {
                        using var localReader = new FastBufferReader(bytes, Allocator.Temp);
                        subscriber(senderClientId, localReader);
                    }
                    catch (Exception ex) {
                        Debug.LogException(ex);
                    }
                }
            }

            private sealed class Subscription : IDisposable {
                private MessageFanout _owner;
                private NamedMessageHandler _handler;
                private bool _disposed;

                public Subscription(MessageFanout owner, NamedMessageHandler handler) {
                    _owner = owner;
                    _handler = handler;
                }

                public void Dispose() {
                    if (_disposed)
                        return;

                    _disposed = true;
                    _owner?.RemoveSubscriber(_handler);
                    _owner = null;
                    _handler = null;
                }
            }
        }
    }
}
#endif