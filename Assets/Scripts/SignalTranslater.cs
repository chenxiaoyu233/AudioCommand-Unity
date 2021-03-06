﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SignalTranslater : MonoBehaviour {
	public string[] signal;
	public GameObject[] prefabs;
	Queue <GameObject> que = new Queue<GameObject>();
	public Text Order;
	void Start() {
		signal = new string[AudioControl.PosteriorHandler.caseNumber];
		signal[0] = "four";
		signal[1] = "wow";
		signal[2] = "two";
		signal[3] = "cat";
		signal[4] = "learn";
		signal[5] = "bird";
		signal[6] = "on";
		signal[7] = "tree";
		signal[8] = "go";
		signal[9] = "eight";
		que.Clear();
		Order.text = "|";
	}
	void Update() {
		int[] sig = AudioControl.PredictPool.GetArray();
		for (int i = que.Count; i < sig.Length; i++) {
			if(sig[i] == 8) {
				AudioControl.PredictPool.Init();
				foreach (GameObject cur in que) {
					Destroy(cur);
				}
				que.Clear();
				break;
			} else {
				que.Enqueue(
					Instantiate(prefabs[sig[i]], new Vector3(i * 3 - 6, 0, 0), new Quaternion(0, 0, 0, 0))
				);
			}
		}
		Order.text = "|";
		for (int i = 0; i < sig.Length; i++) {
			Order.text += signal[sig[i]] + "|";
		}
	}
}
