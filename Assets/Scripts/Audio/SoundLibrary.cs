using System;
using System.Collections.Generic;
using UnityEngine;

namespace Core.Audio
{
    public enum SoundCategory
    {
        Bgm = 0,
        Sfx = 1,
        Ui = 2
    }

    [Serializable]
    public sealed class SoundEntry
    {
        [SerializeField] private string _id = "new_sound";
        [SerializeField] private SoundCategory _category = SoundCategory.Sfx;
        [SerializeField] private AudioClip[] _clips = Array.Empty<AudioClip>();
        [SerializeField, Range(0f, 1f)] private float _volume = 1f;
        [SerializeField, Range(-3f, 3f)] private float _pitchMin = 1f;
        [SerializeField, Range(-3f, 3f)] private float _pitchMax = 1f;
        [SerializeField] private bool _loop;

        public string Id => _id;
        public SoundCategory Category => _category;
        public IReadOnlyList<AudioClip> Clips => _clips;
        public float Volume => _volume;
        public float PitchMin => Mathf.Min(_pitchMin, _pitchMax);
        public float PitchMax => Mathf.Max(_pitchMin, _pitchMax);
        public bool Loop => _loop;

        public bool IsValid => !string.IsNullOrWhiteSpace(_id) && _clips != null && _clips.Length > 0;
    }

    [CreateAssetMenu(menuName = "Boss Raid/Audio/Sound Library")]
    public sealed class SoundLibrary : ScriptableObject
    {
        [SerializeField] private List<SoundEntry> _entries = new();

        public IReadOnlyList<SoundEntry> Entries => _entries;

        public bool TryGetEntry(string soundId, out SoundEntry entry)
        {
            if (string.IsNullOrWhiteSpace(soundId))
            {
                entry = null;
                return false;
            }

            for (int i = 0; i < _entries.Count; i++)
            {
                SoundEntry candidate = _entries[i];
                if (candidate == null)
                {
                    continue;
                }

                if (!string.Equals(candidate.Id, soundId, StringComparison.Ordinal))
                {
                    continue;
                }

                entry = candidate;
                return true;
            }

            entry = null;
            return false;
        }
    }
}
