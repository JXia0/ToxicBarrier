using System.Collections.Generic;
using UnityEngine;

/// <summary>One persistent music bed and rate-limited, non-spatial game feedback.</summary>
public sealed class GameAudio : MonoBehaviour
{
    public static GameAudio Instance { get; private set; }
    private AudioSource _music, _effects;
    private readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
    private readonly Dictionary<string, float> _nextSound = new Dictionary<string, float>();
    public float MusicVolume { get; private set; }
    public float EffectsVolume { get; private set; }

    private void Awake()
    {
        Instance = this;
        _music = gameObject.AddComponent<AudioSource>();
        _effects = gameObject.AddComponent<AudioSource>();
        _music.playOnAwake = _effects.playOnAwake = false;
        _music.loop = true;
        _music.clip = Resources.Load<AudioClip>("Audio/Refuge");
        SetVolumes(PlayerPrefs.GetFloat("MusicVolume", .35f), PlayerPrefs.GetFloat("EffectsVolume", .65f));
        _music.Play();
    }

    private void Update()
    {
        bool quiet = GameFlowManager.Instance != null && GameFlowManager.Instance.CurrentState == GameState.Paused;
        _music.volume = Mathf.MoveTowards(_music.volume, MusicVolume * (quiet ? .4f : 1f), Time.unscaledDeltaTime);
    }

    public void SetVolumes(float music, float effects)
    {
        MusicVolume = Mathf.Clamp01(music); EffectsVolume = Mathf.Clamp01(effects);
        _music.volume = MusicVolume; _effects.volume = EffectsVolume;
    }

    public static void Play(string sound, float interval = .08f)
    {
        var audio = Instance;
        if (audio == null) return;
        if (audio._nextSound.TryGetValue(sound, out float next) && Time.unscaledTime < next) return;
        audio._nextSound[sound] = Time.unscaledTime + interval;
        if (!audio._clips.TryGetValue(sound, out AudioClip clip))
        {
            clip = Resources.Load<AudioClip>("Audio/" + sound);
            audio._clips[sound] = clip;
        }
        if (clip != null) audio._effects.PlayOneShot(clip);
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }
}
