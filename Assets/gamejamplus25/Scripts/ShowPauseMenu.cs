using UnityEngine;

public class ShowPauseMenu : MonoBehaviour
{
    [SerializeField] private bool playingStage= false;
    [SerializeField] private bool showPauseMenu= false;
    [SerializeField] private GameObject pauseMenuPanel;

    private void Update()
    {
        if (playingStage && !showPauseMenu && Input.GetKeyDown(KeyCode.P))
        {
            pauseMenuPanel.SetActive(true);
            showPauseMenu = true;
        } else if (playingStage && showPauseMenu && Input.GetKeyDown(KeyCode.P))
        {
            pauseMenuPanel.SetActive(false);
            showPauseMenu = false;
        }
    }
            
}
