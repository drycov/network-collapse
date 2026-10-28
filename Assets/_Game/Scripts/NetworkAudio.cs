using System;
using System.Collections.Generic;
using UnityEngine;

namespace NetworkCollapse
{
    public enum SoundCue { Click, Select, Confirm, Error, Alert, LinkBuilt, Repair, Mission, Achievement }

    // Plays user-imported WAV/OGG from Resources/Audio or synthesized original fallback tones.
    // No third-party sound files are bundled.
    public sealed class NetworkAudio : MonoBehaviour
    {
        private AudioSource source;
        private readonly Dictionary<SoundCue, AudioClip> clips = new Dictionary<SoundCue, AudioClip>();
        public float Volume { get; private set; } = .48f;
        public bool Muted { get; private set; }
        private const int SampleRate = 22050;

        private void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            foreach (SoundCue cue in Enum.GetValues(typeof(SoundCue)))
            {
                string key = cue.ToString().ToLowerInvariant();
                clips[cue] = Resources.Load<AudioClip>("Audio/" + key) ?? Synthesize(cue);
            }
        }

        public void SetVolume(float value) { Volume = Mathf.Clamp01(value); }
        public void SetMuted(bool value) { Muted = value; }
        public void Play(SoundCue cue)
        {
            if (Muted || Volume <= 0 || source == null) return;
            if (clips.TryGetValue(cue, out var clip) && clip != null)
                source.PlayOneShot(clip, Volume);
        }

        private static AudioClip Synthesize(SoundCue cue)
        {
            float duration = cue == SoundCue.Alert ? .65f : cue == SoundCue.Mission || cue == SoundCue.Achievement ? .48f : .15f;
            int count = Mathf.CeilToInt(duration * SampleRate);
            float[] samples = new float[count];
            float f0 = cue == SoundCue.Error ? 190f : cue == SoundCue.Alert ? 480f :
                cue == SoundCue.Mission || cue == SoundCue.Achievement ? 510f :
                cue == SoundCue.Select ? 720f : cue == SoundCue.Click ? 890f : 610f;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float normalized = t / duration;
                float env = Mathf.Min(1f, t * 130f) * Mathf.Pow(1f - normalized, 2.0f);
                float sweep = cue == SoundCue.Error ? -90f * normalized : 120f * normalized;
                if (cue == SoundCue.Alert) sweep += Mathf.Sin(t * 18f) * 75f;
                if (cue == SoundCue.Mission || cue == SoundCue.Achievement)
                    sweep += normalized > .55f ? 250f : normalized > .25f ? 130f : 0f;
                float wave = Mathf.Sin(2f * Mathf.PI * (f0 + sweep) * t);
                wave += .24f * Mathf.Sin(2f * Mathf.PI * (f0 * 2f + sweep) * t);
                samples[i] = Mathf.Clamp(wave * env * .18f, -.9f, .9f);
            }
            var clip = AudioClip.Create("NC_" + cue, count, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
        private void OnDestroy()
        {
            foreach (var pair in clips)
                if (pair.Value != null && pair.Value.name.StartsWith("NC_", StringComparison.Ordinal))
                    Destroy(pair.Value);
        }
    }
}
