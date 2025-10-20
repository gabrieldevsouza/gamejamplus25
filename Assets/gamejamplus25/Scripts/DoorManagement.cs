using UnityEngine;

public class DoorManagement : MonoBehaviour
{
    [SerializeField] private Transform doorsObjects;
    [SerializeField] private float fadeDuration = 0.03f;

    //[Header("UI")]
    //[SerializeField] private KeyCounterUI keyCounter;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("Player pegou a chave!");

            // Atualiza UI
           // if (keyCounter != null)
             //   keyCounter.AddKey();

            // Esconde visualmente a chave
            HideVisuals();

            // Abrir portas
            openAll();

            // Destruir o objeto depois do fade
            GetComponent<Collider>().enabled = false;
        }
    }

    private void HideVisuals()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (Renderer rend in renderers)
            rend.enabled = false;
    }

    private void openAll()
    {
        foreach (Transform door in doorsObjects)
            StartCoroutine(FadeAndDestroy(door.gameObject));
    }

    private System.Collections.IEnumerator FadeAndDestroy(GameObject door)
    {
        Renderer renderer = door.GetComponent<Renderer>();
        Material material = renderer.material;
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

        color.a = 0f;
        material.color = color;

        Destroy(door);
    }
}
