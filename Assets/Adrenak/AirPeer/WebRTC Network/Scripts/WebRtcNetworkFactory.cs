using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Byn.Common;
using System;

namespace Byn.Net {

    //TODO: List that keeps track of created networks will be moved into the
    //underlaying factories
    public class WebRtcNetworkFactory : UnitySingleton<WebRtcNetworkFactory> {

        //android needs a static init process. 
        /// <summary>
        /// True if the platform specific init process was tried
        /// </summary>
        private static bool sStaticInitTried = false;

        /// <summary>
        /// true if the static init process was successful. false if not yet tried or failed.
        /// </summary>
        private static bool sStaticInitSuccessful = false;

        private IWebRtcNetworkFactory mFactory = null;
        private List<IBasicNetwork> mCreatedNetworks = new List<IBasicNetwork>();

        public static bool StaticInitSuccessful {
            get {
                return sStaticInitSuccessful;
            }
        }

        private WebRtcNetworkFactory() {

            //try to setup (this also checks if the platform is even supported)
            TryStaticInitialize();
            //setup failed? factory will be null so nothing can be created
            if (sStaticInitSuccessful == false) {
                Debug.LogError("Initialization of the webrtc plugin failed. StaticInitSuccessful is false. ");
                mFactory = null;
                return;
            }
#if UNITY_WEBGL && !UNITY_EDITOR
            
            mFactory = new Byn.Net.BrowserWebRtcNetworkFactory();
            Debug.Log("Using BrowserWebRtcNetworkFactory");
            
#else
            LogNativeSupportInfo();
            Byn.Net.Native.NativeWebRtcNetworkFactory factory = new Byn.Net.Native.NativeWebRtcNetworkFactory();
            factory.Initialize();
            mFactory = factory;
            Debug.Log("Using Wrapper: " + WebRtcCSharp.WebRtcWrap.GetVersion() + " WebRTC: " + WebRtcCSharp.WebRtcWrap.GetWebRtcVersion());

#endif

        }

        /// <summary>
        /// Internal use only!
        /// Used to check if the current platform is supported
        /// Used for other libraries building on top of WebRtcNetwork.
        /// </summary>
        /// <returns>True if the platform is supported, false if not.</returns>
        public static bool CheckNativeSupport() {
            //do not access any platform specific code here. only check if there if the platform is supported
            //keep up to date with LogNativeSupportInfo()
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            return true;
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            return true;
#elif UNITY_ANDROID
            return true;
#else
            return false;
#endif
        }

        /// <summary>
        /// Prints platform specific info to the unity log
        /// </summary>
        internal static void LogNativeSupportInfo() {

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            Debug.Log("Initializing native webrtc for windows ...");
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            Debug.LogWarning("Trying to initialize native webrtc for OSX. Note that this OS isn't fully supported yet!");
#elif UNITY_ANDROID
            Debug.LogWarning("Trying to initialize native webrtc for Android.  Note that this OS isn't fully supported yet!");
#else
            Debug.LogError("Platform not supported! Platform: " + Application.platform);
#endif
        }

        /// <summary>
        /// Internally used. No need to manually call this.
        /// 
        /// This function will initialize the wrapper (if needed) before the first webrtc factory can be created.
        /// 
        /// It will set sStaticInitSuccessful to false if the platform isn't supported or the init process failed.
        /// 
        /// </summary>
        public static void TryStaticInitialize() {
            //make sure it is called only once. no need for multiple static inits...
            if (sStaticInitTried)
                return;
            sStaticInitTried = true;

            Debug.Log("Using workaround for the SslStream.AuthenticateAsClient unity bug");
            //activate workaround for unity bug
            DefaultValues.AuthenticateAsClientBugWorkaround = true;

#if UNITY_WEBGL && !UNITY_EDITOR

            //check if the java script part is available
            if (BrowserWebRtcNetwork.IsAvailable() == false)
            {
                //js part is missing -> inject the code into the browser
                BrowserWebRtcNetwork.InjectJsCode();
            }
            //if still not available something failed. setting sStaticInitSuccessful to false
            //will block the use of the factories
            sStaticInitSuccessful = BrowserWebRtcNetwork.IsAvailable();
            if(sStaticInitSuccessful == false)
            {
                Debug.LogError("Failed to initialize the platform dependent part of the WebRtcNetworkFactory. " +
                    " This might be because of browser incompatibility, the webrtcnetworkplugin is missing or an unsupported Unity version is used!");
                return;
            }
#else

            LogNativeSupportInfo();
            Debug.Log("Version info: [" + Native.NativeWebRtcNetworkFactory.GetVersion() + "] / [" + Native.NativeWebRtcNetworkFactory.GetWrapperVersion() + "] / [" + Native.NativeWebRtcNetworkFactory.GetWebRtcVersion() + "]");
            sStaticInitSuccessful = CheckNativeSupport();
            if (sStaticInitSuccessful == false)
                return;
#endif

            //android version needs a special init method in addition
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                sStaticInitSuccessful = TryInitAndroid();
            }catch(Exception e)
            {
                sStaticInitSuccessful = false;
                Debug.LogError("Android specific init process failed due to an exception.");
                Debug.LogException(e);
            }
            if (sStaticInitSuccessful == false)
                return;
#endif
        }
        //#if UNITY_ANDROID && !UNITY_EDITOR
        private static bool TryInitAndroid() {


            Debug.Log("TryInitAndroid");


            //try loading jingle_peerconnection_so
            //this will fail in normal builds but is needed to ensure the loading order
            //if the dynamic webrtc library was used to build
            try {
                AndroidJavaClass androidSystem = new AndroidJavaClass("java.lang.System");
                androidSystem.CallStatic("loadLibrary", "jingle_peerconnection_so");
            }
            catch (AndroidJavaException) {
            }

            Debug.Log("get activity");
            AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            AndroidJavaObject context = activity.Call<AndroidJavaObject>("getApplicationContext");

            Debug.Log("call InitAndroidContext");
            bool successful = WebRtcCSharp.RTCPeerConnectionFactory.InitAndroidContext(context.GetRawObject());

            if (successful) {
                Debug.Log("Android plugin successful initialized.");
            }
            else {
                Debug.LogError("Failed to initialize android plugin.");
            }
            return successful;
        }
        //#endif
        public IBasicNetwork CreateDefault(string websocketUrl, IceServer[] urls = null) {
            IBasicNetwork network = mFactory.CreateDefault(websocketUrl, urls);
            mCreatedNetworks.Add(network);
            return network;
        }


        protected override void OnDestroy() {
            base.OnDestroy();

            Debug.Log("Network factory is being destroyed. All created basic networks will be destroyed as well!");
            foreach (IBasicNetwork net in mCreatedNetworks) {
                net.Dispose();
            }
            mCreatedNetworks.Clear();

            //cleanup
            if (mFactory != null) {
                mFactory.Dispose();
            }
        }
    }
}