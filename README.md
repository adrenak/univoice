# UniVoice
An extensible voice chat/VoIP solution for Unity.
  
# Features
- ## 👥 Group Voice Chat
  * Multiple peers can join a chatroom and exchange audio. 
  * APIs to mute audio on a per peer basis  
- ## 📦 Plug-And-Play 
  * No need to write audio and networking code for most use cases  
  * Support for multiple network types, including PUN2 and WLAN
- ## 🎛️ Customizable
  * UniVoice is pretty much collection of interfaces, you can make your own implementations
  * For more info, see the Customization section below

# Installation  
The recommended installation is via the UniVoice UPM package named `com.adrenak.univoice` available on npmjs.org  
  
1. _Add the NPM registry with UniVoice as a scope_. To do this, go to `Edit/Project Settings/Package Manager`, add the NPM scope registry with the URL `https://registry.npmjs.org` and add `com.adrenak.univoice` as a scope. 

1. _Install the package from NPM registry_ To do this, go to `Window/Package Manager` and refresh packages. Select in the `My Registries` view (located at the top left, the default selection is `Unity Registry`), locate UniVoice and click install. After installation, the package will show up in the `In Project` view as well.
# Usage
## Creating a chatroom agent
To be able to host and join voice chatrooms, you need a `ChatroomAgent` instance.
  
```
var agent = new ChatroomAgent(IChatroomNetwork network, IAudioInput audioInput, IAudioOutputFactory audioOutputFactory);
```

## Hosting and joining chatrooms
`agent.Network` is the most commonly accessed object to do things with UniVoice.  

```
// Host a chatroom 
agent.Network.HostChatroom(optional_data);

// Join an existing
agent.Network.JoinChatroom(optional_data);

// Leave the chatroom
agent.Network.LeaveChatroom(optional_data);

// Closes a chatroom
agent.Network.CloseChatroom(optional_data);

```

The current mode of the `ChatroomAgent` can be accessed using `agent.Network.CurrentMode`, this will return a `ChatroomAgentMode` enum object, the possible values are:
- `Unconnected`: The agent is neither hosting or currently a guest of a chatroom
- `Host`: The agent is currently hosting a chatroom
- `Guest`: The agent is currently a guest in a chatroom

## Interacting with peers
Everyone in the chatroom is assigned an ID by the host. And everyone has a list of IDs of their peers. An ID in UniVoice is a C# `short` that is unique for each member in the chatroom  

To get your ID  
```
short myID = agent.Network.OwnID;
```
  
To get a list of the other peers in the chatroom, use this:  
```
short others = agent.Network.PeerIDs
```

To mute everyone in the chatroom, use `agent.MuteOthers = true;`, set it to `false` to unmute. This will stop audio from every peer in the chatroom.  
  
To mute yourself to everyone in the chatroom use `agent.MuteSelf = true;` set it to `false` to unmute. This will stop sending your audio to the peers in the chatroom.

For muting a specific peer, first get the peers settings object using this:  
```
agent.PeerSettings[id].muteThem = true; // where id belongs to the peer you want to mute
```
  
If you want to mute yourself towards a specific peer, use this:  
```
agent.PeerSettings[id].muteSelf = true; // where id belongs to the peer you don't want to hear you
```
  
## Events
`agent.Network` provides several network related events. Refer to the [API reference](http://www.vatsalambastha.com/univoice/api/Adrenak.UniVoice.ChatroomAgent.html) for them.


# ⚙️ Customization
UniVoice is a plug-and-play solution with several pre-existing modules. The most common use case (which is probably 3D/spatial audio group chat) doesn't need you to write any audio or networking code yourself
  
But it also offers you ways to extend and modify it.  

## 🎤 Audio Input
UniVoice is audio input source agnostic. This means, it doesn't care where it is getting the audio from. It only cares about getting the audio data, giving you freedom to change where it gets that from. 
- transmit real-time mic input
- send audio by reading an mp3 file from disk
- send the audio track of a video file.  
- send in-game audio
  
The most common input source is real-time mic input. If this is what you're looking for, an official input implementation based on [UniMic](https://github.com/adrenak/unimic) is available [here](https://github.com/adrenak/univoice-unimic-input/) which can be used for this.  

The `IAudioInput` interface API reference is [here](http://www.vatsalambastha.com/univoice/api/Adrenak.UniVoice.IAudioInput.html)  
      
## 🔊 Audio Output  
UniVoice is also audio output agnostic. This means, it doesn't care what you do with the audio you receive from peers. It just gives you the data and forgets about it, leaving it up to you what you want to do with it.
- you can play it back in Unity using an AudioSource
- you can save it to disk
- you can use it as input for a speech-to-text 
- stream it to a server
  
The most common use output source is playing it inside your app. An official output implementation that plays peer audio on Unity AudioSource is available [here](https://github.com/adrenak/univoice-audiosource-output)  

Creating an audio output requires implementing two interfaces. They are [`IAudioOutput`](http://www.vatsalambastha.com/univoice/api/Adrenak.UniVoice.IAudioOutput.html) and [`IAudioOutputFactory`](http://www.vatsalambastha.com/univoice/api/Adrenak.UniVoice.IAudioOutputFactory.html)  

## 🌐 Network
You guessed it. UniVoice is network agnostic. This means, it doesn't care how the audio is exchanged between peers. It just needs a networking implementation of its `IChatroomNetwork` interface, which allows you to adapt it to different kinds of network infra.
- you can send audio over WLAN  
- or webrtc
- or popular networking solution providers such as Photon
- or your own custom backend solution

<!-- Currently the following networks implementations are available:
- [PUN2 based](https://github.com/adrenak/univoice-pun2-network). Learn more about PUN2 [here](https://assetstore.unity.com/packages/tools/network/pun-2-free-119922)
- [Telepathy based](https://github.com/adrenak/univoice-telepathy-network). Telepathy is a TCP based networking library, available [here](https://github.com/vis2k/Telepathy). Currently only tested on and made with WLAN in mind. 
- [AirPeer based](https://github.com/adrenak/univoice-airpeer-network/). AirPeer is a P2P networking library, available [here](https://github.com/adrenak/airpeer) -->

Creating your own network requires you to implement the [`IChatroomNetwork` interface](http://www.vatsalambastha.com/univoice/api/Adrenak.UniVoice.IChatroomNetwork.html)
  
# Docs
Manuals and sample projects are not available yet. For the API reference, please visit http://www.vatsalambastha.com/univoice
  
# License and Support
This project is under the [MIT license](https://github.com/adrenak/univoice/blob/master/LICENSE).

Updates and maintenance are not guaranteed and the project is maintained by the original developer in his free time. Community contributions are welcome.
  
__Commercial consultation and development can be arranged__ but is subject to schedule and availability.  
  
# Contact
The developer can be reached at the following links:
  
[Website](http://www.vatsalambastha.com)  
[LinkedIn](https://www.linkedin.com/in/vatsalAmbastha)  
[GitHub](https://www.github.com/adrenak)  
[Twitter](https://www.twitter.com/vatsalAmbastha)  
