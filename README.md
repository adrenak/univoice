# UniVoice
UniVoice is a voice chat/VoIP solution for Unity.
  
It comes with ready-to-use P2P (peer to peer) conenctivity which allows devices to communicate nearly free of cost*. For the underlying P2P solution, please visit [AirPeer](https://www.github.com/adrenak/airpeer)  

Some features of UniVoice:
- 👥 Group voice chat. Multiple peers can join a chatroom and exchange audio.  

- ⚙ Peer specific settings. Don't want to listen to a peer? Mute them. Don't want someone listening to you? Mute yourself against them.

- ✍ Edit outgoing and incoming audio with filters and effects. (No filters or effects provided out of the box currently)
  
- 🎨 Customise your audio input, output and networking layer. 
  * 🎤 __Configurable Audio Input__: Decide what the input of your outgoing audio is. Let it be from [Unity's Microphone](https://docs.unity3d.com/ScriptReference/Microphone.html) class, or a live streaming audio, or an MP4 file on the disk.
    
  * 🔊 __Configurable Audio Output__:  Decide where the incoming peer audio goes. Let the output of incoming audio be [Unity AudioSource](https://docs.unity3d.com/ScriptReference/AudioSource.html) to play the audio in-game, or write it into an MP4 on the disk, or stream it to an online service.

  * 🌐 __Configurable Network__: Want to use UniVoice in a WLAN project using [Telepathy?](https://github.com/vis2k/Telepathy) Just adapt its API for UniVoice with a simple the `IChatroomNetwork` interface. Using your own backend for multiplayer? Create and expose your audio API and write a UniVoice implementation, again with the same interface.
  
- 📦 Provides out-of-the-box implementation for audio input, output and networking. Just run the group chat sample in Unity. UniVoice comes packaged with:
  * 🎤 __Audio Input__: based on [UniMic](https://www.github.com/adrenak/unimic) which sends your microphone input over the network.  

  * 🔊 __Audio Output__: source that plays incoming peer audio on [Unity AudioSource](https://docs.unity3d.com/ScriptReference/AudioSource.html)  

  * 🌐 __P2P network__: implementation based on [AirPeer](https://www.github.com/adrenak/airpeer) which uses WebRTC for free-of-cost networking between peers. 
  
    Plus, to get started you don't need to worry about hosting your own WebRTC signalling server as a server that's good enough for testing is already available. (See the project samples for more details)

_*signalling server costs still apply, but they are minimal and sometimes free on platforms such as Heroku_

# Documentation
For the API documentation, please visit http://www.vatsalambastha.com/univoice
  
Manuals, Wiki, Tutorials, etc. are not available yet.
  
# Usage
## Creating a chatroom agent
- To be able to host and join voice chatrooms, you need a `ChatroomAgent` instance. To get the ready-to-use inbuilt implementation, use this
  
```
var agent = new InbuiltChatroomAgentFactory(SIGNALLING_SERVER_URL).Create();
// Don't worry, a signalling server URL is available inside the repositories samples code. 
```

## Hosting and joining chatrooms
Every peer in the chatroom is assigned an ID by the host. And every peer has a peer list, representing the other peers in the chatroom.
  
- To get your ID  
`agent.ID;`
  
`ChatroomAgent` exposes `Network`, an implementation of `IChatroomNetwork`
  
- To get a list of the other peers in the chatroom, use this:  
`agent.Network.Peers`

`agent.Network` also provides methods to host or join a chatroom. Here is how you use them:
  
```
// Host a chatroom using a name
agent.Network.HostChatroom("ROOM_NAME"); 

// Join an existing chatroom using a name
agent.Network.JoinChatroom("ROOM_NAME");

// Leave the chatroom, if connected to one
agent.Network.LeaveChatroom();

// Closes a chatroom, if was hosting one
agent.Network.CloseChatroom();

```
## Muting Audio
To mute everyone in the chatroom, use `agent.MuteOthers = true;` or set it to `false` to unmute them all.  
  
To mute yourself use `agent.MuteSelf = true;` or set it to `false` to unmute yourself. This will stop sending your audio to all the peers in the chatroom.

For muting a specific peer, first get the peers settings object using this:  
```
var settings = agent.PeerSettings[id]; // where id belongs to the peer in question
settings.muteThem = true;
```
  
If you want to mute yourself towards a specific peer, use this:
```
var settings = agent.PeerSettings[id]; // where id belongs to the peer in question
settings.muteSelf = true;
```

## Events
`agent.Network` provides several network related events. Refer to the API reference for them.

# Known Issues
UniVoice is based on [AirPeer](https://www.github.com/adrenak/airpeer) which currently has an issue where peers on different networks are often unable to connect.
    
Eg. two mobile phone in different geographical locations are trying to have a voice chat. Both are connected to their respective WiFi. One hosts and waits for the other. The one trying to join may fail and only succeed when the connection is changed from its Wifi to cellular data.
    
This issue will be addressed inside AirPeer itself. For more see the 'Connectivity issues' section at the [AirPeer Homepage](http://www.vatsalambastha.com/airpeer) 

Alternatively, a [Unity WebRTC](https://github.com/Unity-Technologies/com.unity.webrtc) based implementation of UniVoice's network interface may be directly introduced.

In either case, there a need to have a newer underlying networking solution.

# License and Support
This project under the [MIT license](https://github.com/adrenak/univoice/blob/master/LICENSE).

Updates and maintenance are not guaranteed and the project is maintained by the original developer in his free time. Community contributions are welcome.
  
__Commercial consultation and development can be arranged__ but is subject to schedule and availability.  
  
# Contact
The developer can be reached at the following links:
  
[Website](http://www.vatsalambastha.com)  
[LinkedIn](https://www.linkedin.com/in/vatsalAmbastha)  
[GitHub](https://www.github.com/adrenak)  
[Twitter](https://www.twitter.com/vatsalAmbastha)  
