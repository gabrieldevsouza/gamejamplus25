using UnityEngine;

public class DoorManagement : MonoBehaviour
{
    [SerializeField] private Transform doorsObjects;
    [SerializeField] private float fadeDuration = 0.01f; // Duration in seconds

    private void OnTriggerEnter(Collider other) 
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("Player pegou a chave!");

            // Deixar visual da chave invisível imediatamente
            HideVisuals();

            // Abrir portas
            openAll();

            // Destruir o objeto depois do fade
            Destroy(gameObject, fadeDuration);
        }
    }

    private void HideVisuals()
    {
        // Desliga todos os renderers do objeto (incluindo filhos)
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (Renderer rend in renderers)
        {
            rend.enabled = false;
        }
    }

    private void openAll()
    {
        foreach (Transform door in doorsObjects)
        {
            StartCoroutine(FadeAndDestroy(door.gameObject));
        }
    }

    private System.Collections.IEnumerator FadeAndDestroy(GameObject door)
    {
        Renderer renderer = door.GetComponent<Renderer>();
        Material material = renderer.material; // Use instance, not shared
        Color color = material.color;

        float startAlpha = color.a;
        float time = 0f;

        while (time < fadeDuration)
        {
            time += Time.deltaTime;
            float alpha = Mathf.Lerp(startAlpha, 0f, time / fadeDuration);
            color.a = alpha;
            material.color = color;
            yield return null;
        }

        // Ensure it's fully transparent
        color.a = 0f;
        material.color = color;

        Destroy(door);
    }
}
