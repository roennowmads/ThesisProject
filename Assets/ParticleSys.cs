using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using System;
using UnityEngine.Rendering;

public class ParticleSys : MonoBehaviour {

    public ParticleSystem psys;
    private ParticleSystem.Particle[] cloud;
    private int m_pointsCount = 61440;

    // Use this for initialization
    void Start () {


        int numberOfPoints = 1000000;

        Vector3[] ppoints = new Vector3[numberOfPoints];

        int sideLength = (int)Math.Sqrt(numberOfPoints);

        for (int i = 0; i < sideLength; i++) {
            for (int j = 0; j < sideLength; j++) {
                int index = i + j * sideLength;

                ppoints[index] = new Vector3(j / 10.0f,0, i / 10.0f );

            }
        }
        m_pointsCount = ppoints.Length;

        cloud = new ParticleSystem.Particle[m_pointsCount];

        for (int ii = 0; ii < ppoints.Length; ++ii) {
            cloud[ii].position = ppoints[ii];
            cloud[ii].startColor = new Color(ii, 0f, 0f);
            cloud[ii].startSize = 0.1f;
            //cloud[ii].color = colors[ii];
            //cloud[ii].size = 0.1f;
        }

        psys.SetParticles(cloud, cloud.Length);

    }

    // Update is called once per frame
    void Update () {
		
	}
}
