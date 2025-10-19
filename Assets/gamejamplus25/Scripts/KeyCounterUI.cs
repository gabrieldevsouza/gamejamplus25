using UnityEngine;
using TMPro;

public class KeyCounterUI : MonoBehaviour
{
    [SerializeField] private TMP_Text keyText;
    private int keyCount = 0;

    // Função para incrementar o contador
    public void AddKey(int amount = 1)
    {
        keyCount += amount;
        UpdateUI();
    }

    private void UpdateUI()
    {
        keyText.text = "x" + keyCount;
    }
}
