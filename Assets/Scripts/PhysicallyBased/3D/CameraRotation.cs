using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraRotation : MonoBehaviour {

    public Vector3 centerPos = new Vector3(0.5f, 0.5f, 0.5f);
    public float speed = 1;
    public float radius = 1;

	// Use this for initialization
	void Start () {
		
	}

    float time = 0;
	void Update () {
        time += Time.deltaTime;
        transform.position = new Vector3(centerPos.x + radius * Mathf.Cos(speed * time), centerPos.z, centerPos.z + radius * Mathf.Sin(speed * time));
        transform.LookAt(centerPos);
	}
}
