using UnityEngine;

public class DoorManagement : MonoBehaviour
{
    [SerializeField] private Transform doorsObjects;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    
    private void OnTriggerStay(Collider other) {
        if (other.CompareTag("Player"))
        {
            openAll();
            Destroy(this);
        }
    }

    private void openAll()
    {
		foreach (Transform door in doorsObjects)
		{
            Destroy(door.gameObject);
		}
	}
}
