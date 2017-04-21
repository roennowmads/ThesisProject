using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class bla : MonoBehaviour {

	// Use this for initialization
	void Start () {

        Mesh m = GetComponent<MeshFilter>().mesh;

        //Renderer r = GetComponent<MeshRenderer>();
        //m.GetIndices(0);
        m.SetIndices(m.GetIndices(0), MeshTopology.Lines, 0);
	}
	
	// Update is called once per frame
	void Update () {
		
	}
}
