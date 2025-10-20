using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class MusicDirector : MonoBehaviour
{
    public static MusicDirector Instance { get; private set; }

    [System.Serializable]
    public struct SceneOverride
    {
        public string sceneName;   // exact name from Build Settings
        public AudioClip clip;     // level-specific music
    }

    [Header("Core Clips")]
    [SerializeField] private AudioClip menuClip;
    [SerializeField] private AudioClip defaultLevelClip;

    [Header("Scene Overrides (optional)")]
    [Tooltip("Use these to assign specific tracks to specific levels/scenes.")]
    [SerializeField] private SceneOverride[] perSceneOverrides;

    [Header("Which scenes are Menus?")]
    [Tooltip("Any scene with a name in this list will use menuClip automatically.")]
    [SerializeField] private string[] menuSceneNames = new[] { "MainMenu", "Menu", "Title" };

    [Header("Playback")]
    [SerializeField, Range(0f, 1f)] private float volume = 0.8f;
    [SerializeField] private float crossfadeTime = 0.75f;
    [SerializeField] private bool playOnStart = true;

    AudioSource _a, _b;   // A/B sources for crossfades
    AudioSource _active;  // currently audible

    void Awake()
    {
        if (Instance && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _a = gameObject.AddComponent<AudioSource>();
        _b = gameObject.AddComponent<AudioSource>();
        SetupSource(_a);
        SetupSource(_b);
        _active = _a;

        SceneManager.activeSceneChanged += OnActiveSceneChanged;
    }

    void Start()
    {
        if (!playOnStart) return;

        // Pick music for the initial scene
        string scene = SceneManager.GetActiveScene().name;
        AutoPickForScene(scene, instantIfSame: true);
    }

    void OnDestroy()
    {
        if (Instance == this)
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
    }

    void SetupSource(AudioSource s)
    {
        s.loop = true;
        s.playOnAwake = false;
        s.ignoreListenerPause = true; // unaffected by Time.timeScale (pause menus)
        s.volume = 0f;
    }

    void OnActiveSceneChanged(Scene from, Scene to)
    {
        // Auto-switch when scenes change (e.g., last level → menu)
        AutoPickForScene(to.name, instantIfSame: true);
    }

    // -------- Public API --------

    /// <summary>Force menu music (use this right before/after loading menu).</summary>
    public void PlayMenu()
    {
        if (!menuClip) return;
        // Avoid restarting if already playing this
        if (_active.clip == menuClip && _active.isPlaying) return;
        StartCoroutine(CrossfadeTo(menuClip));
    }

    /// <summary>Force default level music (or keep playing if it’s already the same).</summary>
    public void PlayLevel()
    {
        var clip = defaultLevelClip;
        if (!clip) return;
        if (_active.clip == clip && _active.isPlaying) return;
        StartCoroutine(CrossfadeTo(clip));
    }

    /// <summary>Play an explicit clip (e.g., a special boss track).</summary>
    public void PlayClip(AudioClip clip)
    {
        if (!clip) return;
        if (_active.clip == clip && _active.isPlaying) return;
        StartCoroutine(CrossfadeTo(clip));
    }

    /// <summary>Set master music volume [0..1].</summary>
    public void SetVolume(float v)
    {
        volume = Mathf.Clamp01(v);
        if (_active) _active.volume = volume;
    }

    // -------- Internals --------

    void AutoPickForScene(string sceneName, bool instantIfSame)
    {
        // Is this a menu scene?
        for (int i = 0; i < menuSceneNames.Length; i++)
        {
            if (!string.IsNullOrEmpty(menuSceneNames[i]) && sceneName == menuSceneNames[i])
            {
                if (instantIfSame && _active.clip == menuClip && _active.isPlaying) return;
                StartCoroutine(CrossfadeTo(menuClip));
                return;
            }
        }

        // Level-specific override?
        for (int i = 0; i < perSceneOverrides.Length; i++)
        {
            if (perSceneOverrides[i].sceneName == sceneName && perSceneOverrides[i].clip)
            {
                if (instantIfSame && _active.clip == perSceneOverrides[i].clip && _active.isPlaying) return;
                StartCoroutine(CrossfadeTo(perSceneOverrides[i].clip));
                return;
            }
        }

        // Fallback to default level clip
        if (defaultLevelClip)
        {
            if (instantIfSame && _active.clip == defaultLevelClip && _active.isPlaying) return;
            StartCoroutine(CrossfadeTo(defaultLevelClip));
        }
    }

    IEnumerator CrossfadeTo(AudioClip next)
    {
        if (!next)
            yield break;

        AudioSource from = _active;
        AudioSource to   = (from == _a) ? _b : _a;

        to.clip = next;
        to.volume = 0f;
        to.Play();

        // If nothing is playing yet → snap in
        if (!from.clip || !from.isPlaying || from.volume <= 0.0001f || crossfadeTime <= 0.01f)
        {
            from.Stop();
            to.volume = volume;
            _active = to;
            yield break;
        }

        float t = 0f;
        float dur = crossfadeTime;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime; // respects pause menus
            float k = Mathf.Clamp01(t / dur);
            from.volume = Mathf.Lerp(volume, 0f, k);
            to.volume   = Mathf.Lerp(0f,      volume, k);
            yield return null;
        }

        from.Stop();
        from.volume = volume; // reset for next time
        to.volume   = volume;
        _active     = to;
    }
}
