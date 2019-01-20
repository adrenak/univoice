## UniVoice
A Peer to Peer Voice Over IP (VoIP) solution for Unity

## Intro
UniVoice uses [UniMic](https://www.github.com/adrenak/unimic) to capture audio and [AirPeer](https://www.github.com/adrenak/airpeer) to form WebRTC based peer to peer connections between which audio data can be exchanged.

## Usage
### Basic
- `Voice.New(AudioSource src)` to create a new `Voice` instance. `src` is the `AudioSource` component that will play the incoming audio.  
```
var voice = Voice.New(GetComponent<AudioSource>());
```
  
- `Voice.Create(string name, Action<bool> callback)` creates a new room for voice chat. `name` should be unique (globally), `callback` is true if the room was created successfully, else false.  
```
voice.Create("a3b4cd", success => Debug.Log("Room create success: " + success));
```

- `Voice.Join(string name, Action<bool> callback)` attempts to join an existing room. `name` should be the one to be joined. `callback` is true if the join was successful, else false.
```
voice.Join("a3b4cd", success => Debug.Log("Room join success: " + success));
```
- `Voice.OnJoin(ConnectionId id)` event fired on a `Voice` instance that is serving as a host (ie. `.Create` was called on it) everytime a peer joins the room
  
- `Voice.OnLeave(ConnectionId id)` event fired on a `Voice` instance that is serving as a host (ie. `.Create` was called on it) everytime a peer leaves the room
  
- `Voice.OnSendVoiceSegment(int index, float[] segment)` event fired everytime an audio segment was sent over the network. `segment` is the `float` representation of the audio and `index` represents the index of the segment. Eg. The first segment is indexed as 0
  
- `Voice.OnGetVoiceSegment(int index, float[] segment)` event fired everytime an audio segment is received over the network. `segment` is the `float` representation of the audio and `index` represents the index of the segment. Eg. The first segment is indexed as 0

## Contact
[@www](http://www.vatsalambastha.com)  
[@github](https://www.github.com/adrenak)