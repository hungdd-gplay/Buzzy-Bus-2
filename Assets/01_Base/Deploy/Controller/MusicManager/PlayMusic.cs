using UnityEngine;

public class PlayMusic : MonoBehaviour {

	public AudioClip musicClip;

	private void Start()
	{
		SoundManager.PlayMusic(musicClip);
	}

}
