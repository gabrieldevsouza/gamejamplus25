using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PowerUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private LineForce lineForce;
    [SerializeField] private CanvasGroup canvasGroup; // optional for fade/show
    [SerializeField] private Image fillImage;         // must have a sprite, type=Filled
    [SerializeField] private TextMeshProUGUI percentText;

    [Header("Behavior")]
    [SerializeField] private bool hideWhenIdle = true;

    private void Reset()
    {
        lineForce = FindObjectOfType<LineForce>();
        canvasGroup = GetComponentInChildren<CanvasGroup>();
        if (!fillImage) fillImage = GetComponentInChildren<Image>();
        if (!percentText) percentText = GetComponentInChildren<TextMeshProUGUI>();
    }

    private void OnEnable()
    {
        if (!lineForce) lineForce = FindObjectOfType<LineForce>();
        if (lineForce != null)
        {
            lineForce.AimStarted += OnAimStarted;
            lineForce.AimPowerChanged += OnAimPowerChanged;
            lineForce.AimEnded += OnAimEnded;
        }

        if (hideWhenIdle) SetVisible(false);
        if (fillImage) fillImage.fillAmount = 0f;
        if (percentText) percentText.text = "0%";
    }

    private void OnDisable()
    {
        if (lineForce != null)
        {
            lineForce.AimStarted -= OnAimStarted;
            lineForce.AimPowerChanged -= OnAimPowerChanged;
            lineForce.AimEnded -= OnAimEnded;
        }
    }

    private void OnAimStarted()
    {
        SetVisible(true);
        UpdatePower(lineForce != null ? lineForce.CurrentPowerT : 0f);
    }

    private void OnAimPowerChanged(float t)
    {
        UpdatePower(t);
    }

    private void OnAimEnded()
    {
        if (hideWhenIdle) SetVisible(false);
        UpdatePower(0f);
    }

    private void UpdatePower(float t)
    {
        if (fillImage) fillImage.fillAmount = Mathf.Clamp01(t);
        if (percentText) percentText.text = Mathf.RoundToInt(Mathf.Clamp01(t) * 100f) + "%";
    }

    private void SetVisible(bool show)
    {
        if (canvasGroup)
        {
            canvasGroup.alpha = show ? 1f : 0f;
            canvasGroup.interactable = show;
            canvasGroup.blocksRaycasts = show;
        }
        else if (fillImage)
        {
            fillImage.enabled = show;
            if (percentText) percentText.enabled = show;
        }
    }
}
