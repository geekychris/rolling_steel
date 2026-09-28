using System;
using System.Collections.Generic;
using UnityEngine;

namespace RollingSteel
{
    /// Every sound is synthesised at startup, so the project needs no audio
    /// assets at all.
    public static class Sfx
    {
        public enum Clip { Start, Goal, Death, Clack, Warn, Fall, Sizzle, Chomp, Shatter }

        const int Rate = 44100;

        static AudioSource oneShot;
        static readonly Dictionary<Clip, AudioClip> bank = new Dictionary<Clip, AudioClip>();
        static AudioClip roll;

        public static AudioClip Roll => roll;

        public static void Init(GameObject host)
        {
            if (oneShot != null) return;

            oneShot = host.AddComponent<AudioSource>();
            oneShot.playOnAwake = false;
            oneShot.spatialBlend = 0f;
            oneShot.volume = 0.45f;

            bank[Clip.Start] = Make("start", 0.45f, t => Env(t, 0.45f, 0.01f, 0.25f) * Chord(t, 330f, 440f, 660f));
            bank[Clip.Goal] = Make("goal", 0.95f, t => Env(t, 0.95f, 0.01f, 0.4f) * Arp(t, new[] { 523f, 659f, 784f, 1047f }, 0.16f));
            bank[Clip.Death] = Make("death", 0.7f, t => Env(t, 0.7f, 0.005f, 0.35f) * Sine(t, Mathf.Lerp(420f, 60f, t / 0.7f)) * 0.9f);
            bank[Clip.Clack] = Make("clack", 0.12f, t => Env(t, 0.12f, 0.002f, 0.05f) * (Noise(t) * 0.5f + Sine(t, 180f) * 0.5f));
            bank[Clip.Warn] = Make("warn", 0.16f, t => Env(t, 0.16f, 0.004f, 0.08f) * Sine(t, 880f));

            // Death sounds. Each one has a falling gesture plus its own texture,
            // so you can tell what killed you without reading the banner.
            float fallPhase = 0f;
            bank[Clip.Fall] = Make("fall", 1.25f, t =>
            {
                float f = Mathf.Lerp(950f, 70f, Mathf.Pow(t / 1.25f, 0.55f));
                fallPhase += f / Rate;            // integrate, or the sweep tears
                return Env(t, 1.25f, 0.005f, 0.5f) *
                       (Mathf.Sin(fallPhase * 2f * Mathf.PI) * 0.7f + Noise(t) * 0.14f);
            });

            float sizzlePhase = 0f;
            bank[Clip.Sizzle] = Make("sizzle", 0.85f, t =>
            {
                float f = Mathf.Lerp(420f, 90f, t / 0.85f);
                sizzlePhase += f / Rate;
                float fizz = (Noise(t) - Noise(t - 1f / Rate)) * 0.55f;
                return Env(t, 0.85f, 0.004f, 0.4f) *
                       (fizz + Mathf.Sin(sizzlePhase * 2f * Mathf.PI) * 0.35f);
            });

            float chompPhase = 0f;
            bank[Clip.Chomp] = Make("chomp", 0.5f, t =>
            {
                float f = Mathf.Lerp(260f, 55f, Mathf.Pow(t / 0.5f, 0.7f));
                chompPhase += f / Rate;
                return Env(t, 0.5f, 0.003f, 0.2f) *
                       (Mathf.Sin(chompPhase * 2f * Mathf.PI) * 0.75f + Noise(t) * 0.3f);
            });

            // metallic shards, five short FM pings over a noise transient
            bank[Clip.Shatter] = Make("shatter", 0.6f, t =>
            {
                float v = Noise(t) * Mathf.Exp(-t / 0.045f) * 0.55f;
                for (int k = 0; k < 5; k++)
                {
                    float u = t - k * 0.032f;
                    if (u < 0f) continue;
                    float f = 1150f + k * 470f;
                    v += Mathf.Sin(u * f * 2f * Mathf.PI
                                   + 3.2f * Mathf.Sin(u * f * 1.7f * 2f * Mathf.PI))
                         * Mathf.Exp(-u / 0.085f) * 0.2f;
                }
                return v;
            });

            roll = Make("roll", 1f, t => Noise(t) * 0.35f + Sine(t, 55f) * 0.2f, loop: true);
        }

        public static void Play(Clip c)
        {
            if (oneShot == null || !bank.TryGetValue(c, out var clip)) return;
            oneShot.PlayOneShot(clip);
        }

        // ---- tiny synth --------------------------------------------------

        static AudioClip Make(string name, float dur, Func<float, float> fn, bool loop = false)
        {
            int n = Mathf.Max(1, (int)(Rate * dur));
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(fn((float)i / Rate), -1f, 1f);

            if (loop)
            {
                // crossfade the tail into the head so the loop doesn't click
                int fade = Mathf.Min(n / 8, 2000);
                for (int i = 0; i < fade; i++)
                {
                    float k = i / (float)fade;
                    data[i] = Mathf.Lerp(data[n - fade + i], data[i], k);
                }
            }

            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float Sine(float t, float f) => Mathf.Sin(t * f * 2f * Mathf.PI);

        static float Chord(float t, params float[] f)
        {
            float s = 0f;
            foreach (var x in f) s += Sine(t, x);
            return s / f.Length;
        }

        static float Arp(float t, float[] notes, float step)
        {
            int i = Mathf.Clamp((int)(t / step), 0, notes.Length - 1);
            return Sine(t, notes[i]);
        }

        static float Noise(float t)
        {
            // cheap deterministic hash noise; Random would make captures unrepeatable
            int x = (int)(t * Rate);
            x = (x << 13) ^ x;
            return 1f - ((x * (x * x * 15731 + 789221) + 1376312589) & 0x7fffffff) / 1073741824f;
        }

        static float Env(float t, float dur, float attack, float release)
        {
            if (t < attack) return t / attack;
            if (t > dur - release) return Mathf.Max(0f, (dur - t) / release);
            return 1f;
        }
    }
}
