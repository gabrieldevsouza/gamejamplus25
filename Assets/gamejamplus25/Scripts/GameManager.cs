using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    [Header("UI Elements")]
    public GameObject panelCredits;   // Painel com os créditos
    public Button buttonStart;        // Botão "Iniciar Jogo"
    public Button buttonCredits;      // Botão "Créditos"
    public Button buttonBack;         // Botão "Voltar" dentro dos créditos

    [Header("Scene Settings")]
    public string sceneToLoad = "Level1";  // Nome da cena principal do jogo

    private void Start()
    {
        // Garantir que o painel de créditos comece desativado
        if (panelCredits != null)
            panelCredits.SetActive(false);

        // Adicionar eventos aos botões
        buttonStart.onClick.AddListener(StartGame);
        buttonCredits.onClick.AddListener(ShowCredits);
        buttonBack.onClick.AddListener(BackToMenu);
    }

    // Inicia o jogo carregando a próxima cena
    public void StartGame()
    {
        SceneManager.LoadScene(sceneToLoad);
    }

    // Mostra o painel de créditos
    public void ShowCredits()
    {
        panelCredits.SetActive(true);
    }

    // Fecha o painel de créditos
    public void BackToMenu()
    {
        panelCredits.SetActive(false);
    }
}
