using System;
using UnityEngine;

namespace Core.Audio
{
    public enum SoundId
    {
        None = 0,
        BgmBattle,
        BgmLobby,
        BgmVictory,
        BgmLose,
        UiButton,
        PlayerFootstep,
        PlayerKatanaCombo,
        PlayerKatanaHit,
        Player1VoiceAttack,
        Player1VoiceDash,
        Player1VoiceHit,
        DragonAttack1,
        DragonAttack2,
        DragonAttack3,
        DragonBreath,
        DragonFireball,
        DragonFlyUp,
        DragonFlyForward,
        DragonFlyDown,
        DragonScream,
        DragonDead
    }

    [DisallowMultipleComponent]
    public sealed class SoundController : MonoBehaviour
    {
        [Header("Library")]
        [SerializeField] private SoundLibrary _library;

        [Header("Volume")]
        [SerializeField, Range(0f, 1f)] private float _masterVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float _bgmVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float _sfxVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float _uiVolume = 1f;

        [Header("Channels (Optional Override)")]
        [SerializeField] private AudioSource _bgmSource;
        [SerializeField] private AudioSource _uiSource;

        [Header("SFX OneShot Pool")]
        [SerializeField, Min(1)] private int _oneShotPoolSize = 12;

        [Header("Debug (Optional)")]
        [SerializeField] private bool _playOnStart;
        [SerializeField] private string _playOnStartId = string.Empty;
        [SerializeField] private bool _enableSoundDebugLog;

        private AudioSource[] _sfxPool = Array.Empty<AudioSource>();
        private int _nextSfxSourceIndex;

        public static SoundController Instance { get; private set; }
        public SoundLibrary Library => _library;
        public bool HasLibrary => _library != null;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("SoundController: Duplicate instance detected. Destroying latest instance.");
                Destroy(gameObject);
                return;
            }

            Instance = this;
            EnsureAudioChannels();
            ApplyVolumeSnapshot();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Start()
        {
            if (!_playOnStart || string.IsNullOrWhiteSpace(_playOnStartId))
            {
                return;
            }

            Play(_playOnStartId);
        }

        public void SetLibrary(SoundLibrary library)
        {
            _library = library;
        }

        public bool Play(string soundId)
        {
            if (_enableSoundDebugLog)
            {
                Debug.Log($"[SoundController] Play request. id={soundId}");
            }

            if (_library == null)
            {
                LogPlayFailure(soundId, "SoundLibrary is not assigned.");
                return false;
            }

            if (!_library.TryGetEntry(soundId, out SoundEntry entry) || entry == null || !entry.IsValid)
            {
                LogPlayFailure(soundId, "Sound id not found or invalid.");
                return false;
            }

            AudioClip clip = PickRandomClip(entry);
            if (clip == null)
            {
                LogPlayFailure(soundId, "Sound has no playable clip.");
                return false;
            }

            if (_enableSoundDebugLog)
            {
                Debug.Log(
                    $"[SoundController] Play resolved. id={soundId}, clip={clip.name}, category={entry.Category}, entryVolume={entry.Volume:F2}, pitchRange={entry.PitchMin:F2}~{entry.PitchMax:F2}");
            }

            switch (entry.Category)
            {
                case SoundCategory.Bgm:
                    PlayLoopOnBgmChannel(clip, entry);
                    if (_enableSoundDebugLog)
                    {
                        Debug.Log($"[SoundController] Dispatch success. id={soundId}, channel=Bgm");
                    }
                    return true;
                case SoundCategory.Ui:
                    PlayOneShotOnUiChannel(clip, entry);
                    if (_enableSoundDebugLog)
                    {
                        Debug.Log($"[SoundController] Dispatch success. id={soundId}, channel=Ui");
                    }
                    return true;
                default:
                    PlayOneShotOnSfxChannel(clip, entry);
                    if (_enableSoundDebugLog)
                    {
                        Debug.Log($"[SoundController] Dispatch success. id={soundId}, channel=Sfx");
                    }
                    return true;
            }
        }

        public bool Play(SoundId soundId)
        {
            if (!TryResolveSoundKey(soundId, out string soundKey))
            {
                if (_enableSoundDebugLog)
                {
                    Debug.LogWarning($"[SoundController] Play failure. SoundId is not mapped. soundId={soundId}");
                }
                Debug.LogWarning($"SoundController: Unsupported SoundId={soundId}");
                return false;
            }

            return Play(soundKey);
        }

        public void StopBgm()
        {
            if (_bgmSource == null)
            {
                return;
            }

            _bgmSource.Stop();
            _bgmSource.clip = null;
        }

        public void StopAllSfx()
        {
            for (int i = 0; i < _sfxPool.Length; i++)
            {
                AudioSource source = _sfxPool[i];
                if (source == null)
                {
                    continue;
                }

                source.Stop();
            }
        }

        public void SetMasterVolume(float value)
        {
            _masterVolume = Mathf.Clamp01(value);
            ApplyVolumeSnapshot();
        }

        public void SetBgmVolume(float value)
        {
            _bgmVolume = Mathf.Clamp01(value);
            ApplyVolumeSnapshot();
        }

        public void SetSfxVolume(float value)
        {
            _sfxVolume = Mathf.Clamp01(value);
        }

        public void SetUiVolume(float value)
        {
            _uiVolume = Mathf.Clamp01(value);
            ApplyVolumeSnapshot();
        }

        private void EnsureAudioChannels()
        {
            if (_bgmSource == null)
            {
                _bgmSource = CreateOrGetChildAudioSource("BGM_AudioSource");
            }

            ConfigureChannelSource(_bgmSource, loop: true, playOnAwake: false);

            if (_uiSource == null)
            {
                _uiSource = CreateOrGetChildAudioSource("UI_AudioSource");
            }

            ConfigureChannelSource(_uiSource, loop: false, playOnAwake: false);

            if (_oneShotPoolSize < 1)
            {
                _oneShotPoolSize = 1;
            }

            if (_sfxPool.Length != _oneShotPoolSize)
            {
                _sfxPool = new AudioSource[_oneShotPoolSize];
            }

            for (int i = 0; i < _oneShotPoolSize; i++)
            {
                AudioSource pooledSource = _sfxPool[i];
                if (pooledSource == null)
                {
                    pooledSource = CreateOrGetChildAudioSource($"SFX_AudioSource_{i}");
                    _sfxPool[i] = pooledSource;
                }

                ConfigureChannelSource(pooledSource, loop: false, playOnAwake: false);
            }
        }

        private void ApplyVolumeSnapshot()
        {
            if (_bgmSource != null)
            {
                _bgmSource.volume = _masterVolume * _bgmVolume;
            }

            if (_uiSource != null)
            {
                _uiSource.volume = _masterVolume * _uiVolume;
            }
        }

        private void PlayLoopOnBgmChannel(AudioClip clip, SoundEntry entry)
        {
            if (_bgmSource == null)
            {
                return;
            }

            _bgmSource.loop = true;
            _bgmSource.clip = clip;
            _bgmSource.pitch = UnityEngine.Random.Range(entry.PitchMin, entry.PitchMax);
            _bgmSource.volume = _masterVolume * _bgmVolume * entry.Volume;
            _bgmSource.Play();
        }

        private void PlayOneShotOnUiChannel(AudioClip clip, SoundEntry entry)
        {
            if (_uiSource == null)
            {
                return;
            }

            _uiSource.pitch = UnityEngine.Random.Range(entry.PitchMin, entry.PitchMax);
            _uiSource.volume = _masterVolume * _uiVolume * entry.Volume;
            _uiSource.PlayOneShot(clip);
        }

        private void PlayOneShotOnSfxChannel(AudioClip clip, SoundEntry entry)
        {
            AudioSource source = AcquireNextSfxSource();
            if (source == null)
            {
                return;
            }

            source.pitch = UnityEngine.Random.Range(entry.PitchMin, entry.PitchMax);
            source.volume = _masterVolume * _sfxVolume * entry.Volume;
            source.clip = clip;
            source.Play();
        }

        private AudioSource AcquireNextSfxSource()
        {
            if (_sfxPool == null || _sfxPool.Length == 0)
            {
                return null;
            }

            for (int i = 0; i < _sfxPool.Length; i++)
            {
                int index = (_nextSfxSourceIndex + i) % _sfxPool.Length;
                AudioSource candidate = _sfxPool[index];
                if (candidate == null || candidate.isPlaying)
                {
                    continue;
                }

                _nextSfxSourceIndex = (index + 1) % _sfxPool.Length;
                return candidate;
            }

            AudioSource fallback = _sfxPool[_nextSfxSourceIndex];
            _nextSfxSourceIndex = (_nextSfxSourceIndex + 1) % _sfxPool.Length;
            return fallback;
        }

        private static AudioClip PickRandomClip(SoundEntry entry)
        {
            if (entry.Clips == null || entry.Clips.Count == 0)
            {
                return null;
            }

            int index = UnityEngine.Random.Range(0, entry.Clips.Count);
            return entry.Clips[index];
        }

        private void LogPlayFailure(string soundId, string reason)
        {
            if (_enableSoundDebugLog)
            {
                Debug.LogWarning($"[SoundController] Play failure. id={soundId}, reason={reason}");
            }

            Debug.LogWarning($"SoundController: {reason} playId={soundId}");
        }

        private AudioSource CreateOrGetChildAudioSource(string childName)
        {
            Transform child = transform.Find(childName);
            if (child == null)
            {
                GameObject childObject = new GameObject(childName);
                childObject.transform.SetParent(transform, false);
                child = childObject.transform;
            }

            AudioSource source = child.GetComponent<AudioSource>();
            if (source == null)
            {
                source = child.gameObject.AddComponent<AudioSource>();
            }

            return source;
        }

        private static bool TryResolveSoundKey(SoundId soundId, out string soundKey)
        {
            soundKey = soundId switch
            {
                SoundId.BgmBattle => "bgm_battle",
                SoundId.BgmLobby => "bgm_lobby",
                SoundId.BgmVictory => "bgm_victory",
                SoundId.BgmLose => "bgm_lose",
                SoundId.UiButton => "ui_button",
                SoundId.PlayerFootstep => "player_footstep",
                SoundId.PlayerKatanaCombo => "player_katana_combo",
                SoundId.PlayerKatanaHit => "player_katana_hit",
                SoundId.Player1VoiceAttack => "player1_voice_attack",
                SoundId.Player1VoiceDash => "player1_voice_dash",
                SoundId.Player1VoiceHit => "player1_voice_hit",
                SoundId.DragonAttack1 => "dragon_attack1",
                SoundId.DragonAttack2 => "dragon_attack2",
                SoundId.DragonAttack3 => "dragon_attack3",
                SoundId.DragonBreath => "dragon_breath",
                SoundId.DragonFireball => "dragon_fireball",
                SoundId.DragonFlyUp => "dragon_fly_up",
                SoundId.DragonFlyForward => "dragon_fly_forward",
                SoundId.DragonFlyDown => "dragon_fly_down",
                SoundId.DragonScream => "dragon_scream",
                SoundId.DragonDead => "dragon_dead",
                _ => string.Empty
            };

            return !string.IsNullOrEmpty(soundKey);
        }

        private static void ConfigureChannelSource(AudioSource source, bool loop, bool playOnAwake)
        {
            if (source == null)
            {
                return;
            }

            source.playOnAwake = playOnAwake;
            source.loop = loop;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.rolloffMode = AudioRolloffMode.Linear;
        }
    }
}
