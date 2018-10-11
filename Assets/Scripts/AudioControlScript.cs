using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AudioControl;
using SavWav;

public class AudioControlScript : MonoBehaviour {

	public bool hasAccess = false; // 是否能够使用录音设备进行录音
	public string curDevice = "Uninitialized"; // 录制使用的录音设备
	public bool isRecording { // 当前是否正在录制
		get { return Microphone.IsRecording(curDevice); }
	}
	public bool hasRecorded = false; // 之前是否发起过录制
	public AudioClip clip;
	AudioController audioController = new AudioController();

	IEnumerator askForMicrophone() {
		yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
        hasAccess = Application.HasUserAuthorization(UserAuthorization.Microphone);
	}

	void setDevice() {
		if (Microphone.devices.Length > 0) {
			curDevice = Microphone.devices[0];
		} else {
			hasAccess = false;
		}
	}

	// Use this for initialization
	void Start () {
		askForMicrophone();
		setDevice();
		audioController.Init();

		// Debug
		//audioController.InstructionAsync(clip);
		//SavWav.SavWav.Save("/Users/chenxiaoyu/Desktop/newWav", clip);
	}
	
	// Update is called once per frame
	void Update () {
		if (Input.GetMouseButton(0)) {
			if (!hasRecorded) {
				hasRecorded = true;
				clip = Microphone.Start(curDevice, false, 1000, 16000);
				audioController.InstructionAsync(clip, curDevice);
			}
			audioController.MainThreadSyncDataWithCilp();
			//Debug.Log("Pressed primary button."); // check if pressed the button
			//Debug.Log(Microphone.GetPosition(curDevice));
		} else {
			Microphone.End(curDevice);
			if (hasRecorded) {
				//audioController.InstructionAsync(clip);
				audioController.isRec = false;
				hasRecorded = false;
			}
		}
	}
}
