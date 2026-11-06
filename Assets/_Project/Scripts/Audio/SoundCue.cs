using System;
using UnityEngine;

namespace AdventureGame.Audio
{
    // Inspector'dan ayarlanan tek bir ses olayı: birden fazla klipten rastgele biri, hafif perde farkıyla çalınır.
    [Serializable]
    public class SoundCue
    {
        public AudioClip[] clips;
        [Range(0f, 1f)] public float volume = 1f;
        [Range(0f, 0.5f)] public float pitchVariation = 0.08f;

        public bool HasClips => clips != null && clips.Length > 0;

        public void Play(AudioSource source, float basePitch = 1f)
        {
            if (source == null || !HasClips) return;

            AudioClip clip = clips[UnityEngine.Random.Range(0, clips.Length)];
            if (clip == null) return;

            source.pitch = basePitch + UnityEngine.Random.Range(-pitchVariation, pitchVariation);
            source.PlayOneShot(clip, volume);
        }

        // Objede AudioSource yoksa eklenir; spatial = true ise ses dünyadaki konumdan gelir
        public static AudioSource EnsureSource(GameObject owner, bool spatial)
        {
            var source = owner.GetComponent<AudioSource>();
            if (source != null) return source;

            source = owner.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = spatial ? 1f : 0f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 3f;
            source.maxDistance = 30f;
            return source;
        }
    }
}
