using System;
using System.Collections.Generic;
using UnityEngine;

namespace WaterTurn
{
    /// <summary>
    /// The HTML prototype synthesised every sound with the WebAudio oscillator;
    /// this bakes the same short tones into AudioClips at boot, so the demo needs
    /// no audio assets.  One AudioSource and PlayOneShot is enough for the tiny
    /// SFX set; pitch is used for jump power.
    /// </summary>
    public sealed class JellyRunAudio : MonoBehaviour
    {
        const int Rate = 44100;
        enum Wave { Sine, Triangle, Square, Saw }

        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        AudioSource source;
        public bool Muted { get; private set; }

        public void Init(bool muted)
        {
            Muted = muted;
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 1f;

            clips["jump"] = Tone("jump", 240f, 600f, .17f, Wave.Triangle, .16f);
            clips["splash"] = Tone("splash", 320f, 120f, .14f, Wave.Sine, .14f);
            clips["land"] = Tone("land", 180f, 90f, .10f, Wave.Sine, .11f);
            clips["dbl"] = Tone("dbl", 520f, 820f, .12f, Wave.Sine, .12f);
            clips["die"] = Tone("die", 170f, 46f, .34f, Wave.Saw, .14f);
            clips["switch"] = Tone("switch", 300f, 700f, .20f, Wave.Square, .09f);
            clips["flood"] = Tone("flood", 90f, 300f, .55f, Wave.Sine, .15f);
            clips["nope"] = Tone("nope", 120f, 100f, .12f, Wave.Square, .07f);
            clips["hop"] = Tone("hop", 420f, 300f, .08f, Wave.Sine, .09f);
            clips["enter"] = Sequence("enter", new[]
            {
                (0f, .12f, 440f, 660f, Wave.Sine, .12f),
                (.10f, .22f, 660f, 990f, Wave.Sine, .11f),
            });
            clips["pearl"] = Sequence("pearl", new[]
            {
                (0f, .12f, 880f, 1180f, Wave.Sine, .13f),
                (.08f, .16f, 1320f, 1560f, Wave.Sine, .11f),
            });
            clips["win"] = Sequence("win", new[]
            {
                (0f, .22f, 523f, 523f, Wave.Sine, .13f),
                (.11f, .22f, 659f, 659f, Wave.Sine, .13f),
                (.22f, .22f, 784f, 784f, Wave.Sine, .13f),
                (.33f, .26f, 1046f, 1046f, Wave.Sine, .14f),
            });
        }

        public void SetMuted(bool muted)
        {
            Muted = muted;
            PlayerPrefs.SetInt(JellyRunGame.MutedKey, muted ? 1 : 0);
            PlayerPrefs.Save();
        }

        public void Play(string key, float pitch = 1f, float volume = 1f)
        {
            if (Muted || source == null) return;
            if (!clips.TryGetValue(key, out var clip) || clip == null) return;
            source.pitch = Mathf.Clamp(pitch, .5f, 2f);
            source.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        // ------------------------------------------------------------ synthesis

        static AudioClip Tone(string name, float f0, float f1, float dur, Wave wave, float gain)
        {
            int count = Mathf.Max(1, Mathf.RoundToInt(dur * Rate));
            var data = new float[count];
            AddTone(data, 0f, dur, f0, f1, wave, gain);
            var clip = AudioClip.Create(name, count, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip Sequence(string name, (float start, float dur, float f0, float f1, Wave wave, float gain)[] parts)
        {
            float end = 0f;
            foreach (var p in parts) end = Mathf.Max(end, p.start + p.dur);
            int count = Mathf.Max(1, Mathf.RoundToInt((end + .04f) * Rate));
            var data = new float[count];
            foreach (var p in parts) AddTone(data, p.start, p.dur, p.f0, p.f1, p.wave, p.gain);
            var clip = AudioClip.Create(name, count, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static void AddTone(float[] data, float start, float dur, float f0, float f1, Wave wave, float gain)
        {
            int offset = Mathf.RoundToInt(start * Rate);
            int count = Mathf.RoundToInt(dur * Rate);
            float phase = 0f;
            for (int i = 0; i < count; i++)
            {
                int dst = offset + i;
                if (dst < 0 || dst >= data.Length) break;
                float t = i / (float)count;
                float freq = Mathf.Lerp(f0, f1, t);
                phase += freq * Mathf.PI * 2f / Rate;
                float s;
                switch (wave)
                {
                    case Wave.Triangle: s = 2f / Mathf.PI * Mathf.Asin(Mathf.Sin(phase)); break;
                    case Wave.Square: s = Mathf.Sin(phase) >= 0f ? .6f : -.6f; break;
                    case Wave.Saw: s = 2f * (phase / (Mathf.PI * 2f) - Mathf.Floor(phase / (Mathf.PI * 2f) + .5f)); break;
                    default: s = Mathf.Sin(phase); break;
                }
                float attack = Mathf.Clamp01(i / (Rate * .014f));
                float decay = Mathf.Pow(Mathf.Clamp01(1f - t), 1.5f);
                data[dst] += s * gain * attack * decay;
            }
        }
    }
}
