using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PowerUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private LineForce source;              // drag your player here (or auto-find)
    [SerializeField] private Image powerFill;               // Image set to 'Filled' (Horizontal)
    [SerializeField] private TextMeshProUGUI powerText;     // optional
    [SerializeField] private Gradient powerGradient;        // optional (low→high)

    private void Reset()
    {
        // Auto-find a LineForce in scene if not set
        if (!source) source = FindObjectOfType<LineForce>();
    }

    private void OnEnable()
    {
        if (!source) source = FindObjectOfType<LineForce>();
        if (source)
        {
            source.AimStarted      += HandleAimStarted;
            source.AimPowerChanged += HandleAimPowerChanged;
            source.AimEnded        += HandleAimEnded;
        }
        Hide();
    }

    private void OnDisable()
    {
        if (source)
        {
            source.AimStarted      -= HandleAimStarted;
            source.AimPowerChanged -= HandleAimPowerChanged;
            source.AimEnded        -= HandleAimEnded;
        }
    }

    private void HandleAimStarted()
    {
        Show(0f);
    }

    private void HandleAimPowerChanged(float t01)
    {
        Show(t01);
    }

    private void HandleAimEnded()
    {
        Hide();
    }

    // ── UI helpers ───────────────────────────────────────────────────────────────
    private void Show(float t01)
    {
        if (powerFill)
        {
            powerFill.fillAmount = Mathf.Clamp01(t01);
            if (powerGradient != null && (powerGradient.colorKeys.Length > 0 || powerGradient.alphaKeys.Length > 0))
                powerFill.color = powerGradient.Evaluate(powerFill.fillAmount);
            if (!powerFill.gameObject.activeSelf) powerFill.gameObject.SetActive(true);
        }
        if (powerText)
        {
            powerText.text = Mathf.RoundToInt(Mathf.Clamp01(t01) * 100f) + "%";
            if (!powerText.gameObject.activeSelf) powerText.gameObject.SetActive(true);
        }
    }

    private void Hide()
    {
        if (powerFill) { powerFill.fillAmount = 0f; if (powerFill.gameObject.activeSelf) powerFill.gameObject.SetActive(false); }
        if (powerText) { powerText.text = "0%";     if (powerText.gameObject.activeSelf) powerText.gameObject.SetActive(false); }
    }
}
