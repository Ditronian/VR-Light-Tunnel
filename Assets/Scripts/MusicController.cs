using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MusicController : MonoBehaviour
{
    private static MusicController instance = null;
    private AudioSource audioTrack;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            return;
        }
        if (instance == this) return;
        Destroy(gameObject);
    }

    void Start()
    {
        audioTrack = GetComponent<AudioSource>();
        audioTrack.loop = true;
        audioTrack.Play();
    }
    
}
