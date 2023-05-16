using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UMUI.Audio
{
    
public class AudioManager : MonoBehaviour
{
    private static AudioManager _singleton;
    public static AudioManager singleton
    {
        get { return _singleton; }
    }
    
    private void InitSingleton()
    {
        if (_singleton != null && _singleton != this)
        {
            Destroy(this);
        }
        else
        {
            _singleton = this;
        }
    }

    private void Awake()
    {
        InitSingleton();
    }
    
    
    public AudioSource musicSource;
    public AudioSource ambienceSource;
    public AudioSource oneShotSource;

    public List<OneShot> audioClips;

    public float audioCoolDown;

    public void PlayOneShot(string name,bool coolDown)
    {
        for (int i = 0; i < audioClips.Count; i++)
        {
            if (audioClips[i].name.Equals(name)&&(audioClips[i].isReady||!coolDown))
            {
                int index = Random.Range(0, audioClips[i].clip.Count);
                oneShotSource.PlayOneShot(audioClips[i].clip[index],audioClips[i].volume);
                StartCoroutine(CoolDownAudioCoroutine(audioClips[i]));
                return;
            }
        }
    }

    private IEnumerator CoolDownAudioCoroutine(OneShot oneShot)
    {
        oneShot.isReady = false;
        yield return new WaitForSeconds(audioCoolDown);
        oneShot.isReady = true;

    }
    
    
    
}
[System.Serializable]
public class OneShot
{
    public string name;
    public List<AudioClip> clip;
    public bool isReady = true;
    [Range(0,1)]
    public float volume;
}
}
