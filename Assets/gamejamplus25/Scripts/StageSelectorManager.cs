using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class StageSelectorManager : MonoBehaviour
{
    [SerializeField] string stageName;
    
    public void LoadStage(string sceneName)
    {
        SceneManager.LoadScene(sceneName); 
        //Change to stage name on button inspector and add stage on Build Profile
    }
}
