using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ParentScript : MonoBehaviour {
    
	void Start ()
    {

	}

	// Update is called once per frame
	void Update () {

        transform.Rotate(new Vector3(0.0f, 1.0f, 0.0f), Time.deltaTime*20.0f);
	}

}
