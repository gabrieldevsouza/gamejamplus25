using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{   
    [SerializeField] string sceneToLoad = "SCN_StageSelect_UI";  // Change to the Game Scene
    
    public void StartGame()
    {
        SceneManager.LoadScene(sceneToLoad);
    }
}
