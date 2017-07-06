using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TerrainScript : MonoBehaviour {

	// Use this for initialization
	void Start () {
		
        Vector4 trans = transform.position;
        Terrain.activeTerrain.materialTemplate.SetFloat("_Height", trans.y);



	}
	
	// Update is called once per frame
	void Update () {
		
	}
}
