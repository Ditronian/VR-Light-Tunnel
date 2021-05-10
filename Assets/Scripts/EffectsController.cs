using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EffectsController : MonoBehaviour
{
    private static EffectsController instance = null;
    public AudioSource woosh;
    public AudioSource damageTrack;
    public AudioSource extraLife;

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
        damageTrack.loop = false;
        woosh.loop = false;
        extraLife.loop = false;
    }

    public void playDamage()
    {
        damageTrack.Play();
    }

    public void playWoosh()
    {
        woosh.Play();
    }

    public void playExtraLife()
    {
        extraLife.Play();
    }
}
