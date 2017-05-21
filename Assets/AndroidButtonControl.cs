using UnityEngine;
using System.Collections.Generic;

public static class CommonAndroid {

    private static AndroidJavaObject s_mainActivity = null;
    private static AndroidJavaObject s_androidAudioManager = null;

    public static AndroidJavaObject GetAndroidAudioManager()
    {
        if (s_androidAudioManager == null)
        {
            s_androidAudioManager = GetMainActivity().Call<AndroidJavaObject>("getSystemService", "audio");
        }
        return s_androidAudioManager;
    }

    public static AndroidJavaObject GetMainActivity()
    {
        if (s_mainActivity == null)
        {
            var unityPlayerClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            s_mainActivity = unityPlayerClass.GetStatic<AndroidJavaObject>("currentActivity");
        }
        return s_mainActivity;
    }

    public static bool IsRunningOnAndroid()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return true;
#else
        return false;
#endif
    }
}


public interface VolumeListener
{
     void OnVolumeUp();
     void OnVolumeDown();
}

public abstract class VolumeListenerGO : MonoBehaviour, VolumeListener
{
    public abstract void OnVolumeUp();
    public abstract void OnVolumeDown();
}


public class AndroidButtonControl : MonoBehaviour {

    public bool m_bGetVolumeFromPhone = true;
    //for non gameobject listener
    public List<VolumeListener> m_vVolumeListener = null;
    //for gameobject listener (easier to edit in scene mode)
    public VolumeListenerGO[] m_vVolumeListenerGO = null;

    private float m_fPrevVolume = -1;
    private bool m_bShutDown = false;

    public GameObject m_particlesObject;
    public GameObject m_particlesObject2;
    public GameObject m_particlesObject3;
    public GameObject m_particlesObject4;
    public GameObject m_particlesObject5;
    public GameObject m_particlesObject6;

    //quick access to reference
    private static AndroidButtonControl s_instance = null;


    public static AndroidButtonControl Get()
    {
        return s_instance;
    }

    //Get max volume phone
    public float GetMaxVolume()
    {
        if (m_bGetVolumeFromPhone && CommonAndroid.IsRunningOnAndroid())
        {
            AndroidJavaObject audioManager = CommonAndroid.GetAndroidAudioManager();
            return audioManager.Call<int>("getStreamMaxVolume", 3);
        }
        else
        {
            return 1;
        }
    }

    //Get phone volume if running or android or application volume if running on pc
    //(or wanted by user)
    public float GetVolume()
    {
        if(m_bGetVolumeFromPhone && CommonAndroid.IsRunningOnAndroid())
        {
            AndroidJavaObject audioManager = CommonAndroid.GetAndroidAudioManager();
            return audioManager.Call<int>("getStreamVolume", 3);
        }
        else
        {
            return AudioListener.volume;
        }

    }

    //set phone or application volume (according if running on android or if user want application volume)
    public void SetVolume(float a_fVolume)
    {
        if (m_bGetVolumeFromPhone && CommonAndroid.IsRunningOnAndroid())
        {
            AndroidJavaObject audioManager = CommonAndroid.GetAndroidAudioManager();
            audioManager.Call("setStreamVolume", 3, (int)a_fVolume, 0);
        }
        else
        {
            AudioListener.volume = a_fVolume;
        }
    }

    private void ResetVolume()
    {
        SetVolume(m_fPrevVolume);
    }

    void Start () {
        s_instance = this;
        PowerOn();
    }

    void OnVolumeDown()
    {
        if (m_vVolumeListener != null)
        {
            Debug.Log("volume down!");
            foreach (VolumeListener listener in m_vVolumeListener)
            {
                listener.OnVolumeDown();
            }
        }
        if (m_vVolumeListenerGO != null)
        {
            Debug.Log("volume down go!");
            //GameObject particlesObject = GameObject.FindWithTag("PointCloud");
            PointCloud script = m_particlesObject.GetComponent<PointCloud>();
            script.changePointsCount(-10000);
            PointCloud script2 = m_particlesObject2.GetComponent<PointCloud>();
            script2.changePointsCount(-10000);
            PointCloud script3 = m_particlesObject3.GetComponent<PointCloud>();
            script3.changePointsCount(-10000);
            PointCloud script4 = m_particlesObject4.GetComponent<PointCloud>();
            script4.changePointsCount(-10000);
            PointCloud script5 = m_particlesObject5.GetComponent<PointCloud>();
            script5.changePointsCount(-10000);
            PointCloud script6 = m_particlesObject6.GetComponent<PointCloud>();
            script6.changePointsCount(-10000);
            Debug.Log(script.getPointCount());
            
            foreach (VolumeListener listener in m_vVolumeListenerGO)
            {
                listener.OnVolumeDown();
            }
        }
    }

    void OnVolumeUp()
    {
        if (m_vVolumeListener != null)
        {
            Debug.Log("volume up!");
            foreach (VolumeListener listener in m_vVolumeListener)
            {
                listener.OnVolumeUp();
            }
        }
        if (m_vVolumeListenerGO != null)
        {
            Debug.Log("volume up go!");
            //GameObject particlesObject = GameObject.FindWithTag("PointCloud");
            PointCloud script = m_particlesObject.GetComponent<PointCloud>();
            script.changePointsCount(10000);
            PointCloud script2 = m_particlesObject2.GetComponent<PointCloud>();
            script2.changePointsCount(10000);
            PointCloud script3 = m_particlesObject3.GetComponent<PointCloud>();
            script3.changePointsCount(10000);
            PointCloud script4 = m_particlesObject4.GetComponent<PointCloud>();
            script4.changePointsCount(10000);
            PointCloud script5 = m_particlesObject5.GetComponent<PointCloud>();
            script5.changePointsCount(10000);
            PointCloud script6 = m_particlesObject6.GetComponent<PointCloud>();
            script6.changePointsCount(10000);
            Debug.Log(script.getPointCount());

            foreach (VolumeListener listener in m_vVolumeListenerGO)
            {
                listener.OnVolumeUp();
            }
        }

    }

    //If user want to change volume, he has to mute this script first
    //else the script will interpret this has a user input and resetvolume
    public void ShutDown()
    {
        m_bShutDown = true;
    }

    //to unmute the script
    public void PowerOn()
    {
         m_bShutDown = false;
        //get the volume to avoid interpretating previous change (when script was muted) as user input
        m_fPrevVolume = GetVolume();

        //if volume is set to max, reduce it -> if not, there will be no detection for volume up
        if (m_fPrevVolume == GetMaxVolume())
        {
            --m_fPrevVolume;
            SetVolume(m_fPrevVolume);
        }
    }

    // Update is called once per frame
    void Update () {
        if (m_bShutDown)
            return;

        float fCurrentVolume = GetVolume();
        float fDiff = fCurrentVolume - m_fPrevVolume;

        //if volume change, compute the difference and call listener according to
        if(fDiff < 0)
        { 
            ResetVolume();
            OnVolumeDown(); 
        }
        else if(fDiff > 0)
        {
            ResetVolume();
            OnVolumeUp();
        }
    }

}

