using UnityEngine;
using UnityEngine.SceneManagement;

public class Projection : MonoBehaviour
{
    [Header("Scene Setup")]
    [SerializeField] private Transform _obstaclesParent;     // Parent containing the bouncy blocks
    [SerializeField] private LineRenderer _line;             // LineRenderer for the trajectory

    [Header("Simulation Settings")]
    [SerializeField] private int _maxPhysicsFrameIterations = 100;

    private Scene _simulationScene;
    private PhysicsScene _physicsScene;

    private Rigidbody _rb;

    private void Start()
    {
        _rb = GetComponent<Rigidbody>();
        Vector3 startPos = transform.position;
            Vector3 startVel = _rb != null ? _rb.linearVelocity : Vector3.zero;
        CreatePhysicsScene();
        SimulateTrajectory(startPos, startVel);
    }

    private void CreatePhysicsScene()
    {
        _simulationScene = SceneManager.CreateScene(
            "Simulation",
            new CreateSceneParameters(LocalPhysicsMode.Physics3D)
        );

        _physicsScene = _simulationScene.GetPhysicsScene();

        // Duplicate each obstacle into the simulation scene
        foreach (Transform obj in _obstaclesParent)
        {
            var ghostObj = Instantiate(obj.gameObject, obj.position, obj.rotation);
            var renderer = ghostObj.GetComponent<Renderer>();
            if (renderer != null)
                renderer.enabled = false;

            SceneManager.MoveGameObjectToScene(ghostObj, _simulationScene);
        }
    }

    public void SimulateTrajectory(Vector3 initialPosition, Vector3 initialVelocity)
{
    // Criar cópia fantasma da esfera
    var ghostSphere = Instantiate(gameObject, initialPosition, transform.rotation);
    SceneManager.MoveGameObjectToScene(ghostSphere, _simulationScene);

    // Remover scripts Projection
    foreach (var proj in ghostSphere.GetComponents<Projection>())
        DestroyImmediate(proj);

    Rigidbody rb = ghostSphere.GetComponent<Rigidbody>();
    if (rb == null)
        rb = ghostSphere.AddComponent<Rigidbody>();

    rb.linearVelocity = initialVelocity; // 🔥 agora vem do parâmetro
    rb.useGravity = true;

    _line.positionCount = _maxPhysicsFrameIterations;

    for (int i = 0; i < _maxPhysicsFrameIterations; i++)
    {
        _physicsScene.Simulate(Time.fixedDeltaTime);
        _line.SetPosition(i, ghostSphere.transform.position);
    }

    Destroy(ghostSphere);
}

}
