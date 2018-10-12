using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;

public class ArrayListTest : MonoBehaviour {

	// Use this for initialization
	void Start () {
		Debug.Log(Thread.CurrentThread.ManagedThreadId);
		Debug.Log(Thread.CurrentThread.Name);
	}
	
	// Update is called once per frame
	void Update () {
		
	}
}
