using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{   
    [SerializeField] string sceneToLoad = "SCN_TestLevel_SKT";  // Change to the Game Scene
    
    public void StartGame()
    {
        SceneManager.LoadScene(sceneToLoad);
    }
}
