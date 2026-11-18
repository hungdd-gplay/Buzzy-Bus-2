using UnityEngine;

public class PlayOnAwake : MonoBehaviour {
	
    public AudioClip audioClip;
	
	private void OnEnable () {
        SoundManager.PlaySound(audioClip);
    }
	
}
