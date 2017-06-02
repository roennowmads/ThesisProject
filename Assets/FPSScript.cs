using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FPSScript : MonoBehaviour {

    public GameObject m_particlesObject;

    public float m_FPSUpdateFrequency = 5.0f;
    public Text uGUIText;
    private int m_currentFPS;
    private int m_framesSinceUpdate;
    private float m_accumulation;
    private float m_currentTime;

	// Use this for initialization
	void Start () {
		m_currentFPS = 0;
        m_framesSinceUpdate = 0;
        m_currentTime = 0.0f;

        //Screen.SetResolution(640, 480, true);
	}
	
	// Update is called once per frame
	void Update () {
		m_currentTime += Time.deltaTime;
        ++m_framesSinceUpdate;
        m_accumulation += Time.timeScale / Time.deltaTime;

        PointCloud script = m_particlesObject.GetComponent<PointCloud>();
        

        if (m_currentTime >= m_FPSUpdateFrequency)
        {
            m_currentFPS = (int)(m_accumulation / m_framesSinceUpdate);
            m_currentTime = 0.0f;
            m_framesSinceUpdate = 0;
            m_accumulation = 0.0f;
            Debug.Log("FPS: " + m_currentFPS);
            uGUIText.text = "" + m_currentFPS + "\n" + script.getPointCount() * 6 * 6 * 6; 
        }
	}
}
