# UniVoice
UniVoice is a voice chat/VoIP solution for Unity.
  
Some features of UniVoice: 
- 🎨 Customize your audio input, output and networking layers. 
  * 🌐 __Configurable Network__: 
    - UniVoice is networking agnostic. Implement the `IAudioClient` and `IAudioServer` interfaces using the networking plugin of your choice to have it send audio data over any networking solution. 
    - Built-in support for:
        - [Mirror networking](https://mirror-networking.com/)
        - [Fish Networking](https://fish-networking.gitbook.io/docs)

  * 🎤 __Configurable Audio Input__: 
    - UniVoice is audio input agnostic. You can change the source of outgoing audio by implementing the `IAudioInput` interface.  
    - Built-in support for:
        - Capturing Mic audio as device input.  
    
  * 🔊 __Configurable Audio Output__:  
    - UniVoice is audio output agnostic. You can divert incoming audio to anywhere you want by implementing the `IAudioOutput` interface.
    - Built-in support for:
        - Playing incoming audio using Unity AudioSource.  

  * ✏️ __Audio Filters__: 
    - Modify outgoing and incoming audio by implementing the `IAudioFilter` interface. 
    - Built-in support for:
        - Opus (Concentus) encoding & decoding.
        - RNNoise based noise removal.
        - Gaussian blurring for minor denoising.

- 👥 Easy integration with your existing networking solution
    - Whether you're using Mirror or FishNet, UniVoice runs in the background in sync with your networking lifecycle
    - A basic integration involves just initializing it on start.
    - For advanced usage like teams, chatrooms, lobbies, you can use the UniVoice API to create runtime behaviour.

- ⚙ Fine control over audio data flow. 
    * Don't want to listen to a peer? Mute them. Don't want someone listening to you? Deafen them.  
    * Group players using tags and control audio flow between them. For example:
        - "red", "blue" and "spectator" tags for two teams playing against each other.
            - Red and Blue teams can only hear each other
            - Spectators can hear everyone
        - clients with "contestant", "judge" and "audience" tags for a virtual talent show. 
            - Contestant can be heard by everyone, but don't hear anyone else (for focus) 
            - Judges can talk to and hear each other for discussions. They can hear the contestant. But not the audience (for less noise)
            - Audience can hear and talk to each other. They can hear the performer. But they cannot hear the judges.
  
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

## Useful links
* API reference is available here: http://www.vatsalambastha.com/univoice
* UniVoice blog: https://blog.vatsalambastha.com/search/label/univoice
* Discord server: https://discord.gg/NGvkEVbdjQ

## Integration
UniVoice isn't currently very drag-and-drop/low-code. The best way to integrate is to have some code perform a one time setup when your app starts and provides access to relevant objects that you can use throughout the rest of the apps runtime.

## Samples
This repository contains two samples:
* `UniVoiceMirrorSetupSample.cs` is a drag and drop component, a simple integration sample script. You can add it to your Mirror NetworkManager to get voice chat to work. No code required, it's as simple as that! It'll work as long as you have setup your project properly. For more instructions see the top of the `UniVoiceMirrorSetupSample.cs` file. 
* `UniVoiceFishNetSetypSample.cs` is also very similar. Just drag and drop and it should work!
* A sample scene that shows the other clients in a UI as well as allows you to mute yourself/them. This sample is Mirror based.
  
> UniVoice currently only supports Mirror and FishNetworking out of the box. Follow the instructions in the "Activating non-packaged dependencies" section below before trying it out the samples. 
  
## Dependencies
[com.adrenak.brw](https://www.github.com/adrenak/brw) for reading and writing messages for communication. See `MirrorServer.cs` and `MirrorClient.cs` where they're used.  

[com.adrenak.unimic](https://www.github.com/adrenak/unimic) for easily capturing audio from any connected mic devices. See `UniMicInput.cs` for usage. Also used for streaming audio playback. See `StreamedAudioSourceOutput.cs` for usage.

[com.adrenak.concentus-unity](https://www.github.com/adrenak/concentus-unity) for Opus encoding and decoding. See `ConcentusEncodeFilter.cs` and `ConcentusDecodeFilter.cs` for usage

## Activating non-packaged dependencies
UniVoice includes and installs the dependencies mentioned above along with itself. The following implementations are available out of the box when you install it:
* Opus encoding/decoding filter (via Contentus-Unity)
* GaussianAudioBlur filter (plain C#, no dependencies used)
* Mic audio capture input (via UniMic)
* AudioSource based playback output (via UniMic)

UniVoice has code that uses dependencies that you have to install and sometimes enable via compilation symbols as they are _not_ UniVoice dependencies and _don't_ get installed along with UniVoice. This is because they are either third party modules or based on native libraries (not plain C#) that can pose build issues.  
* RNNoise Noise removal filter:
    * To enable, ensure the [RNNoise4Unity](https://github.com/adrenak/RNNoise4Unity) package is in your project and add `UNIVOICE_FILTER_RNNOISE4UNITY` to activate it
* Mirror network:
    * Just add the Mirror package to your project. UniVoice will detect it.
* Fish Networking:
    * Just install FishNet package in your project. UniVoice will detect it.

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