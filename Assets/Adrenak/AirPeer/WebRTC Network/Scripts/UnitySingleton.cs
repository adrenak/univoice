using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Byn.Common
{
    public class UnitySingleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T sInstance;
        private static bool mIsQuitting = false;
        private static object sLock = new object();

        public static T Instance
        {
            get
            {
                if (mIsQuitting)
                {
                    Debug.LogWarning(typeof(T).Name + " is already destroyed due to shutdown! Returning null.");
                    return null;
                }

                lock (sLock)
                {
                    if (sInstance == null)
                    {
                        sInstance = (T)FindObjectOfType(typeof(T));

                        if (FindObjectsOfType(typeof(T)).Length > 1)
                        {
                            Debug.LogError(typeof(T).Name + " multiple instances in scene! Make sure only one Singleton is in the scene at the same time!");
                            return sInstance;
                        }

                        if (sInstance == null)
                        {
                            GameObject singleton = new GameObject();
                            sInstance = singleton.AddComponent<T>();
                            singleton.name = typeof(T).Name;

                            DontDestroyOnLoad(singleton);
                        }
                    }
                    return sInstance;
                }
            }
        }
        protected virtual void OnDestroy()
        {
            mIsQuitting = true;
        }
    }
}