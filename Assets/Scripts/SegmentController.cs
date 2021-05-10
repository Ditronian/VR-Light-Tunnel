using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SegmentController : MonoBehaviour
{

    private EffectsController effectsController;

    // Start is called before the first frame update
    void Awake()
    {
        GameObject effectsManager = GameObject.Find("EffectsManager");
        effectsController = effectsManager.GetComponent<EffectsController>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.tag == "Player")
        {
            effectsController.playWoosh();
        }
    }


}
