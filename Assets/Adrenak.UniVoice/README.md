# UniVoice
UniVoice is a voice chat/VoIP solution for Unity.
  
Some features of UniVoice:
- 👥 Group voice chat. Multiple peers can join a chatroom and exchange audio.  

- ⚙ Peer specific settings. Don't want to listen to a peer? Mute them. Don't want someone listening to you? Mute yourself against them.
 
- 🎨 Customize your audio input, output and networking layer. 
  * 🎤 __Configurable Audio Input__: UniVoice is audio input agnostic. It supports mic audio input out of the box and you can change the source of outgoing audio by implementing the `IAudioInput` interrace.  
    
  * 🔊 __Configurable Audio Output__:  UniVoice is audio output agnostic. Out of the box is supports playing peer audio using Unity AudioSource. You can divert incoming audio to anywhere you want by implementing the `IAudioOutput` interface.  

  * 🌐 __Configurable Network__: UniVoice is network agnostic and supports Mirror out of the box. You can implement the `IAudioClient` and `IAudioServer` interfaces using the networking plugin of your choice to make it compatible with it.

  * ✏️ __Audio Filters__: Modify outgoing and incoming audio by implementing the `IAudioFilter` interface. Gaussian blurring for denoising and Opus (Concentus) encoding & decoding for lower bandwidth consumption are provided out of the box.
  
## Installation
⚠️ [OpenUPM](https://openupm.com/packages/com.adrenak.univoice/?subPage=versions) may not have up to date releases. Install using NPM registry instead 👇

Ensure you have the NPM registry in the `manifest.json` file of your Unity project with the following scopes:
```
"scopedRegistries": [
    {
        "name": "npmjs",
        "url": "https://registry.npmjs.org",
        "scopes": [
            "com.npmjs",
            "com.adrenak.univoice",
            "com.adrenak.brw",
            "com.adrenak.unimic",
            "com.adrenak.concentus-unity"
        ]
    }
}
```
Then add `com.adrenak.univoice:x.y.z` to the `dependencies` in your `manifest.json` file (where x.y.z is the version you wish to install). The list of versions is available on [the UniVoice NPM page](https://www.npmjs.com/package/com.adrenak.univoice?activeTab=versions).

## Docs
Am API reference is available: http://www.vatsalambastha.com/univoice

## Samples
This repository contains a sample scene for the Mirror network, which is the best place to see how UniVoice can be integrated into your project.  
  
To try the sample, import Mirror and add the `UNIVOICE_MIRROR_NETWORK` compilation symbol to your project.
  
## Dependencies
[com.adrenak.brw](https://www.github.com/adrenak/brw) for reading and writing messages for communication. See `MirrorServer.cs` and `MirrorClient.cs` where they're used.  

[com.adrenak.unimic](https://www.github.com/adrenak/unimic) for easily capturing audio from any connected mic devices. See `UniMicInput.cs` for usage. Also used for streaming audio playback. See `StreamedAudioSourceOutput.cs` for usage.

[com.adrenak.concentus-unity](https://www.github.com/adrenak/concentus-unity) for Opus encoding and decoding. See `ConcentusEncodeFilter.cs` and `ConcentusDecodeFilter.cs` for usage

## License and Support
This project is under the [MIT license](https://github.com/adrenak/univoice/blob/master/LICENSE).

Community contributions are welcome.
  
## Contact
The developer can be reached at the following links:
  
[Website](http://www.vatsalambastha.com)  
[LinkedIn](https://www.linkedin.com/in/vatsalAmbastha)  
[GitHub](https://www.github.com/adrenak)  
[Twitter](https://www.twitter.com/vatsalAmbastha)  
Discord: `adrenak#1934`