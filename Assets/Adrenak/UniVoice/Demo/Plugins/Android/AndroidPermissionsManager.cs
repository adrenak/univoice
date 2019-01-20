using System;
using UnityEngine;

public class AndroidPermissionCallback : AndroidJavaProxy {
	private event Action<string> OnPermissionGrantedAction;
	private event Action<string> OnPermissionDeniedAction;

	public AndroidPermissionCallback(Action<string> onGrantedCallback, Action<string> onDeniedCallback)
		: base("com.unity3d.plugin.UnityAndroidPermissions$IPermissionRequestResult") {
		if (onGrantedCallback != null) {
			OnPermissionGrantedAction += onGrantedCallback;
		}
		if (onDeniedCallback != null) {
			OnPermissionDeniedAction += onDeniedCallback;
		}
	}

	// Handle permission granted
	public virtual void OnPermissionGranted(string permissionName) {
		//Debug.Log("Permission " + permissionName + " GRANTED");
		if (OnPermissionGrantedAction != null) {
			OnPermissionGrantedAction(permissionName);
		}
	}

	// Handle permission denied
	public virtual void OnPermissionDenied(string permissionName) {
		//Debug.Log("Permission " + permissionName + " DENIED!");
		if (OnPermissionDeniedAction != null) {
			OnPermissionDeniedAction(permissionName);
		}
	}
}

public class AndroidPermissionsManager {
	private static AndroidJavaObject m_Activity;
	private static AndroidJavaObject m_PermissionService;

	private static AndroidJavaObject GetActivity() {
		if (m_Activity == null) {
			var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
			m_Activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
		}
		return m_Activity;
	}

	private static AndroidJavaObject GetPermissionsService() {
		return m_PermissionService ??
			(m_PermissionService = new AndroidJavaObject("com.unity3d.plugin.UnityAndroidPermissions"));
	}

	public static bool IsPermissionGranted(string permissionName) {
		return GetPermissionsService().Call<bool>("IsPermissionGranted", GetActivity(), permissionName);
	}

	public static void RequestPermission(string permissionName, AndroidPermissionCallback callback) {
		RequestPermission(new string[] { permissionName }, callback);
	}

	public static void RequestPermission(string[] permissionNames, AndroidPermissionCallback callback) {
		GetPermissionsService().Call("RequestPermissionAsync", GetActivity(), permissionNames, callback);
	}
}
