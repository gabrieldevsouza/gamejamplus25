using System;
using UnityEngine;

public class TestPlayerLosing : MonoBehaviour
{
    [SerializeField] private bool playerLosing = false;
    [SerializeField] private GameObject losePanel;

    private void FixedUpdate()
    {
        if(playerLosing)
            losePanel.SetActive(true);
    }
}
