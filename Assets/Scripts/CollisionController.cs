using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CollisionController : MonoBehaviour
{
    private GameObject gameManager;
    private GameController gameController;




    void Awake()
    {
        gameManager = GameObject.Find("GameManager");
        gameController = gameManager.GetComponent<GameController>();
    }

    public void OnTriggerEnter(Collider other)
    {
        
        if (other.tag == "Player")
        {
            gameController.DamagePlayer();
        }
    }
}
