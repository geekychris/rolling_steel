using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace RollingSteel
{
    /// Synthesised soundtrack. Nothing is sampled or loaded - each theme is a
    /// step sequence rendered into a looping AudioClip at first use.
    ///
    /// The voicing is aimed at the FM-plus-square sound of early-80s arcade
    /// hardware: a walking square bass, a busy 16th-note arpeggio, a two-operator
    /// FM lead, and noise percussion.
    public static class Music
    {
        public const int Rate = 44100;
        const int StepsPerBar = 16;      // 16th notes
        const int Bars = 8;
        const int TotalSteps = StepsPerBar * Bars;

        enum Wave { Saw, Pulse, Sine, Fm, Tri }

        class Part
        {
            public int[] Steps;          // scale degrees; -1 is a rest
            public Wave Wave = Wave.Pulse;
            public int Transpose;        // semitones
            public float Gain = 0.2f;
            public float Decay = 0.18f;
            public float FmIndex = 2f;
            public float Duty = 0.5f;
            public bool Lowpass;
        }

        class Song
        {
            public float Bpm = 120f;
            public int Root = 57;        // A3
            public int[] Scale = { 0, 2, 3, 5, 7, 8, 10 };   // natural minor
            public readonly List<Part> Parts = new List<Part>();
            public int[] Kick, Snare, Hat;
            public float Swing;          // 0..0.3, delays every other 16th
        }

        static readonly Dictionary<int, AudioClip> cache = new Dictionary<int, AudioClip>();

        public static AudioClip Theme(int index)
        {
            if (cache.TryGetValue(index, out var c) && c != null) return c;

            var song = Build(index);
            float[] data = Render(song);

            var clip = AudioClip.Create($"theme{index}", data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            cache[index] = clip;
            return clip;
        }

        /// Raw samples, for writing a WAV from the command line.
        public static float[] RenderRaw(int index) => Render(Build(index));

        // ---- patterns ------------------------------------------------------

        static int[] Tile(int[] order, params int[][] bars)
        {
            var outp = new int[order.Length * StepsPerBar];
            for (int i = 0; i < order.Length; i++)
                System.Array.Copy(bars[order[i]], 0, outp, i * StepsPerBar, StepsPerBar);
            return outp;
        }

        static readonly int[] Rest16 = { -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 };

        static Song Build(int index)
        {
            switch (index)
            {
                case 1: return Practice();
                case 2: return Beginner();
                case 3: return Intermediate();
                case 4: return Aerial();
                case 5: return Silly();
                case 6: return Ultimate();
                default: return Title();
            }
        }

        // 0 - title: slow and wide, but still has to hold the screen
        static Song Title()
        {
            var s = new Song { Bpm = 100f, Root = 57, Swing = 0.1f };

            s.Parts.Add(new Part   // continuous arpeggio
            {
                Wave = Wave.Pulse, Duty = 0.25f, Gain = 0.13f, Decay = 0.22f,
                Steps = Tile(new[] { 0, 1, 0, 2, 0, 1, 0, 2 },
                    new[] { 0, 4, 7, 4, 9, 7, 4, 7, 0, 4, 7, 4, 11, 9, 7, 4 },
                    new[] { 3, 7, 10, 7, 12, 10, 7, 10, 3, 7, 10, 7, 14, 12, 10, 7 },
                    new[] { 2, 5, 9, 5, 11, 9, 5, 9, 2, 5, 9, 5, 12, 11, 9, 5 }),
            });

            s.Parts.Add(new Part   // pad, long enough to bridge the gaps
            {
                Wave = Wave.Tri, Gain = 0.20f, Decay = 1.7f, Transpose = -12,
                Steps = Tile(new[] { 0, 1, 0, 2, 0, 1, 0, 2 },
                    new[] { 0, -1, -1, -1, -1, -1, -1, -1, 4, -1, -1, -1, -1, -1, -1, -1 },
                    new[] { 3, -1, -1, -1, -1, -1, -1, -1, 5, -1, -1, -1, -1, -1, -1, -1 },
                    new[] { 5, -1, -1, -1, -1, -1, -1, -1, 2, -1, -1, -1, -1, -1, -1, -1 }),
            });

            s.Parts.Add(new Part   // bass
            {
                Wave = Wave.Saw, Gain = 0.24f, Decay = 0.45f, Transpose = -24, Lowpass = true,
                Steps = Tile(new[] { 0, 1, 0, 2, 0, 1, 0, 2 },
                    new[] { 0, -1, -1, -1, 0, -1, -1, -1, 4, -1, -1, -1, 4, -1, -1, -1 },
                    new[] { 3, -1, -1, -1, 3, -1, -1, -1, 5, -1, -1, -1, 5, -1, -1, -1 },
                    new[] { 5, -1, -1, -1, 5, -1, -1, -1, 2, -1, -1, -1, 2, -1, -1, -1 }),
            });

            s.Parts.Add(new Part   // lead
            {
                Wave = Wave.Fm, FmIndex = 1.8f, Gain = 0.15f, Decay = 0.8f, Transpose = 12,
                Steps = Tile(new[] { 3, 0, 3, 1, 3, 0, 2, 1 },
                    new[] { 7, -1, -1, -1, -1, -1, 9, -1, -1, -1, -1, -1, 7, -1, -1, -1 },
                    new[] { 10, -1, -1, -1, 9, -1, -1, -1, 7, -1, -1, -1, -1, -1, -1, -1 },
                    new[] { 4, -1, -1, -1, 5, -1, -1, -1, 4, -1, 2, -1, 0, -1, -1, -1 },
                    Rest16),
            });

            s.Kick = Tile(new[] { 0, 0, 0, 0, 0, 0, 0, 0 },
                new[] { 1, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0 });
            s.Hat = Tile(new[] { 0, 0, 0, 0, 0, 0, 0, 0 },
                new[] { 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1, 1 });
            return s;
        }

        // 1 - practice: bouncy and bright
        static Song Practice()
        {
            var s = new Song { Bpm = 122f, Root = 57, Swing = 0.14f };

            s.Parts.Add(new Part   // bass
            {
                Wave = Wave.Saw, Gain = 0.26f, Decay = 0.16f, Transpose = -24, Lowpass = true,
                Steps = Tile(new[] { 0, 0, 1, 0, 0, 0, 1, 2 },
                    new[] { 0, -1, -1, 0, -1, -1, 7, -1, 3, -1, -1, 3, -1, 5, -1, -1 },
                    new[] { 5, -1, -1, 5, -1, -1, 12, -1, 4, -1, -1, 4, -1, 2, -1, 0 },
                    new[] { 3, -1, 3, -1, 5, -1, 5, -1, 7, -1, 7, -1, 9, -1, 11, -1 }),
            });

            s.Parts.Add(new Part   // arpeggio
            {
                Wave = Wave.Pulse, Duty = 0.3f, Gain = 0.13f, Decay = 0.09f, Transpose = 0,
                Steps = Tile(new[] { 0, 1, 0, 1, 0, 1, 2, 1 },
                    new[] { 0, 2, 4, 7, 4, 2, 0, 2, 4, 7, 9, 7, 4, 2, 0, 2 },
                    new[] { 3, 5, 7, 10, 7, 5, 3, 5, 7, 10, 12, 10, 7, 5, 3, 5 },
                    new[] { 4, 7, 9, 11, 9, 7, 4, 7, 9, 11, 14, 11, 9, 7, 4, 2 }),
            });

            s.Parts.Add(new Part   // lead
            {
                Wave = Wave.Fm, FmIndex = 2.4f, Gain = 0.16f, Decay = 0.4f, Transpose = 12,
                Steps = Tile(new[] { 3, 3, 0, 1, 3, 0, 2, 1 },
                    new[] { 7, -1, -1, -1, 9, -1, -1, -1, 7, -1, 4, -1, 5, -1, -1, -1 },
                    new[] { 4, -1, 2, -1, 0, -1, -1, -1, 2, -1, -1, -1, -1, -1, -1, -1 },
                    new[] { 9, -1, 7, -1, 9, -1, 11, -1, 12, -1, -1, -1, -1, -1, -1, -1 },
                    Rest16),
            });

            s.Kick = Tile(new[] { 0, 0, 0, 1, 0, 0, 0, 1 },
                new[] { 1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 0, 0 },
                new[] { 1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 1, 0, 1 });
            s.Snare = Tile(new[] { 0, 0, 0, 0, 0, 0, 0, 0 },
                new[] { 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0 });
            s.Hat = Tile(new[] { 0, 0, 0, 0, 0, 0, 0, 0 },
                new[] { 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 1 });
            return s;
        }

        // 2 - beginner: driving
        static Song Beginner()
        {
            var s = new Song { Bpm = 134f, Root = 50, Swing = 0.08f };   // D3

            s.Parts.Add(new Part
            {
                Wave = Wave.Saw, Gain = 0.27f, Decay = 0.13f, Transpose = -24, Lowpass = true,
                Steps = Tile(new[] { 0, 0, 1, 1, 0, 0, 2, 1 },
                    new[] { 0, -1, 0, -1, 0, -1, 7, -1, 0, -1, 0, -1, 3, -1, 5, -1 },
                    new[] { 5, -1, 5, -1, 5, -1, 12, -1, 4, -1, 4, -1, 2, -1, 0, -1 },
                    new[] { 3, -1, 3, 3, -1, 3, 5, -1, 7, -1, 7, 7, -1, 9, 10, -1 }),
            });

            s.Parts.Add(new Part
            {
                Wave = Wave.Pulse, Duty = 0.2f, Gain = 0.12f, Decay = 0.07f,
                Steps = Tile(new[] { 0, 1, 0, 1, 2, 1, 0, 1 },
                    new[] { 7, 4, 2, 4, 7, 4, 2, 4, 9, 7, 4, 7, 9, 7, 4, 7 },
                    new[] { 10, 7, 5, 7, 10, 7, 5, 7, 12, 10, 7, 10, 12, 10, 7, 5 },
                    new[] { 11, 9, 7, 9, 11, 9, 7, 9, 14, 12, 9, 12, 14, 12, 9, 7 }),
            });

            s.Parts.Add(new Part
            {
                Wave = Wave.Fm, FmIndex = 3.1f, Gain = 0.15f, Decay = 0.33f, Transpose = 12,
                Steps = Tile(new[] { 3, 0, 3, 1, 0, 2, 1, 3 },
                    new[] { 12, -1, -1, 10, -1, -1, 9, -1, 7, -1, -1, -1, -1, -1, -1, -1 },
                    new[] { 9, -1, 7, -1, 5, -1, 4, -1, 3, -1, -1, -1, -1, -1, -1, -1 },
                    new[] { 7, -1, 9, -1, 10, -1, 12, -1, 14, -1, -1, -1, 12, -1, -1, -1 },
                    Rest16),
            });

            s.Kick = Tile(new[] { 0, 0, 0, 1, 0, 0, 0, 1 },
                new[] { 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0 },
                new[] { 1, 0, 0, 1, 1, 0, 0, 0, 1, 0, 0, 1, 1, 0, 1, 0 });
            s.Snare = Tile(new[] { 0, 0, 0, 0, 0, 0, 0, 0 },
                new[] { 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 1, 0, 1, 0 });
            s.Hat = Tile(new[] { 0, 0, 0, 0, 0, 0, 0, 0 },
                new[] { 1, 1, 1, 0, 1, 1, 1, 0, 1, 1, 1, 0, 1, 1, 1, 1 });
            return s;
        }

        // 3 - intermediate: darker and faster
        static Song Intermediate()
        {
            var s = new Song { Bpm = 144f, Root = 54, Swing = 0f };      // F#3

            s.Parts.Add(new Part
            {
                Wave = Wave.Saw, Gain = 0.28f, Decay = 0.11f, Transpose = -24, Lowpass = true,
                Steps = Tile(new[] { 0, 0, 1, 0, 2, 0, 1, 2 },
                    new[] { 0, 0, -1, 0, -1, 0, -1, 7, 0, 0, -1, 0, -1, 3, -1, 5 },
                    new[] { 5, 5, -1, 5, -1, 5, -1, 12, 4, 4, -1, 4, -1, 2, -1, 0 },
                    new[] { 7, 7, -1, 7, -1, 5, -1, 4, 3, 3, -1, 3, -1, 2, -1, 1 }),
            });

            s.Parts.Add(new Part
            {
                Wave = Wave.Pulse, Duty = 0.15f, Gain = 0.11f, Decay = 0.055f,
                Steps = Tile(new[] { 0, 1, 2, 1, 0, 1, 2, 0 },
                    new[] { 0, 3, 7, 10, 7, 3, 0, 3, 7, 10, 14, 10, 7, 3, 0, 3 },
                    new[] { 2, 5, 9, 12, 9, 5, 2, 5, 9, 12, 16, 12, 9, 5, 2, 5 },
                    new[] { 4, 7, 11, 14, 11, 7, 4, 7, 11, 14, 18, 14, 11, 7, 4, 2 }),
            });

            s.Parts.Add(new Part
            {
                Wave = Wave.Fm, FmIndex = 4.2f, Gain = 0.15f, Decay = 0.28f, Transpose = 12,
                Steps = Tile(new[] { 3, 0, 1, 3, 0, 2, 1, 3 },
                    new[] { 14, -1, 12, -1, 11, -1, 9, -1, 7, -1, -1, -1, -1, -1, -1, -1 },
                    new[] { 7, -1, -1, 9, -1, -1, 11, -1, 12, -1, -1, -1, 14, -1, -1, -1 },
                    new[] { 12, -1, 11, -1, 9, -1, 7, -1, 5, -1, 4, -1, 3, -1, -1, -1 },
                    Rest16),
            });

            s.Kick = Tile(new[] { 0, 0, 1, 0, 0, 0, 1, 1 },
                new[] { 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0 },
                new[] { 1, 0, 1, 0, 1, 0, 0, 1, 1, 0, 1, 0, 1, 0, 1, 1 });
            s.Snare = Tile(new[] { 0, 0, 0, 0, 0, 0, 0, 0 },
                new[] { 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1, 0, 1, 0, 0, 1 });
            s.Hat = Tile(new[] { 0, 0, 0, 0, 0, 0, 0, 0 },
                new[] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 });
            return s;
        }

        // 4 - aerial: high, airy, lots of space under it
        static Song Aerial()
        {
            var s = new Song { Bpm = 126f, Root = 52, Swing = 0.12f };      // E3

            s.Parts.Add(new Part   // bell arpeggio, the voice that carries it
            {
                Wave = Wave.Fm, FmIndex = 1.3f, Gain = 0.15f, Decay = 0.5f, Transpose = 12,
                Steps = Tile(new[] { 0, 1, 0, 2, 0, 1, 2, 2 },
                    new[] { 7, -1, 11, -1, 14, -1, 11, -1, 9, -1, 7, -1, 4, -1, 7, -1 },
                    new[] { 9, -1, 12, -1, 16, -1, 12, -1, 11, -1, 9, -1, 7, -1, 9, -1 },
                    new[] { 4, -1, 7, -1, 11, -1, 7, -1, 6, -1, 4, -1, 2, -1, 4, -1 }),
            });

            s.Parts.Add(new Part   // pad underneath, long
            {
                Wave = Wave.Tri, Gain = 0.19f, Decay = 1.9f, Transpose = -12,
                Steps = Tile(new[] { 0, 1, 0, 2, 0, 1, 2, 2 },
                    new[] { 0, -1, -1, -1, -1, -1, -1, -1, 4, -1, -1, -1, -1, -1, -1, -1 },
                    new[] { 2, -1, -1, -1, -1, -1, -1, -1, 5, -1, -1, -1, -1, -1, -1, -1 },
                    new[] { 4, -1, -1, -1, -1, -1, -1, -1, 0, -1, -1, -1, -1, -1, -1, -1 }),
            });

            s.Parts.Add(new Part   // bass, sparse enough to leave the air
            {
                Wave = Wave.Saw, Gain = 0.24f, Decay = 0.32f, Transpose = -24, Lowpass = true,
                Steps = Tile(new[] { 0, 1, 0, 2, 0, 1, 2, 2 },
                    new[] { 0, -1, -1, 0, -1, -1, -1, 4, -1, -1, 0, -1, -1, -1, 2, -1 },
                    new[] { 2, -1, -1, 2, -1, -1, -1, 5, -1, -1, 2, -1, -1, -1, 4, -1 },
                    new[] { 4, -1, -1, 4, -1, -1, -1, 7, -1, -1, 4, -1, -1, -1, 0, -1 }),
            });

            s.Kick = Tile(new[] { 0, 0, 0, 0, 0, 0, 0, 0 },
                new[] { 1, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1, 0 });
            s.Hat = Tile(new[] { 0, 0, 0, 0, 0, 0, 0, 0 },
                new[] { 0, 0, 1, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 1, 0, 1 });
            return s;
        }

        // 5 - silly: major key, bouncy, deliberately daft
        static Song Silly()
        {
            var s = new Song
            {
                Bpm = 152f, Root = 48, Swing = 0.2f,                        // C3
                Scale = new[] { 0, 2, 4, 5, 7, 9, 11 },                     // major
            };

            s.Parts.Add(new Part   // walking bass
            {
                Wave = Wave.Saw, Gain = 0.26f, Decay = 0.12f, Transpose = -24, Lowpass = true,
                Steps = Tile(new[] { 0, 1, 0, 2, 0, 1, 2, 2 },
                    new[] { 0, -1, 0, -1, 4, -1, 2, -1, 7, -1, 4, -1, 2, -1, 4, -1 },
                    new[] { 3, -1, 3, -1, 7, -1, 5, -1, 9, -1, 7, -1, 5, -1, 2, -1 },
                    new[] { 4, -1, 4, -1, 7, -1, 9, -1, 11, -1, 9, -1, 7, -1, 4, -1 }),
            });

            s.Parts.Add(new Part   // staccato chirps
            {
                Wave = Wave.Pulse, Duty = 0.12f, Gain = 0.12f, Decay = 0.05f, Transpose = 12,
                Steps = Tile(new[] { 0, 0, 1, 0, 2, 0, 1, 2 },
                    new[] { 7, 7, -1, 9, -1, 7, -1, 4, 7, 7, -1, 9, -1, 11, -1, 9 },
                    new[] { 9, 9, -1, 11, -1, 9, -1, 7, 12, 12, -1, 11, -1, 9, -1, 7 },
                    new[] { 4, 4, -1, 2, -1, 4, -1, 7, 9, 9, -1, 7, -1, 4, -1, 2 }),
            });

            s.Parts.Add(new Part   // tune
            {
                Wave = Wave.Fm, FmIndex = 2.2f, Gain = 0.16f, Decay = 0.3f, Transpose = 12,
                Steps = Tile(new[] { 3, 0, 3, 1, 0, 2, 1, 3 },
                    new[] { 4, -1, 7, -1, 9, -1, 7, -1, 11, -1, -1, -1, 9, -1, -1, -1 },
                    new[] { 12, -1, 11, -1, 9, -1, 7, -1, 4, -1, -1, -1, -1, -1, -1, -1 },
                    new[] { 7, -1, 9, -1, 11, -1, 12, -1, 14, -1, 12, -1, 11, -1, 9, -1 },
                    Rest16),
            });

            s.Kick = Tile(new[] { 0, 0, 0, 1, 0, 0, 0, 1 },
                new[] { 1, 0, 0, 0, 0, 0, 1, 0, 1, 0, 0, 0, 0, 0, 1, 0 },
                new[] { 1, 0, 1, 0, 0, 1, 1, 0, 1, 0, 1, 0, 0, 1, 1, 1 });
            s.Snare = Tile(new[] { 0, 0, 0, 0, 0, 0, 0, 0 },
                new[] { 0, 0, 0, 0, 1, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0 });
            s.Hat = Tile(new[] { 0, 0, 0, 0, 0, 0, 0, 0 },
                new[] { 1, 0, 1, 1, 1, 0, 1, 1, 1, 0, 1, 1, 1, 0, 1, 1 });
            return s;
        }

        // 6 - ultimate: relentless, and not friendly about it
        static Song Ultimate()
        {
            var s = new Song { Bpm = 160f, Root = 47, Swing = 0f };         // B2

            s.Parts.Add(new Part   // driving 16th bass
            {
                Wave = Wave.Saw, Gain = 0.29f, Decay = 0.09f, Transpose = -12, Lowpass = true,
                Steps = Tile(new[] { 0, 0, 1, 0, 2, 1, 0, 2 },
                    new[] { 0, 0, 0, -1, 0, 0, -1, 0, 0, 0, 0, -1, 3, -1, 5, -1 },
                    new[] { 5, 5, 5, -1, 5, 5, -1, 5, 4, 4, 4, -1, 2, -1, 0, -1 },
                    new[] { 7, 7, 7, -1, 6, 6, -1, 5, 3, 3, 3, -1, 2, -1, 1, -1 }),
            });

            s.Parts.Add(new Part   // relentless arpeggio
            {
                Wave = Wave.Pulse, Duty = 0.14f, Gain = 0.11f, Decay = 0.05f, Transpose = 12,
                Steps = Tile(new[] { 0, 1, 2, 1, 0, 2, 1, 2 },
                    new[] { 0, 3, 7, 10, 7, 3, 0, 3, 7, 10, 14, 10, 7, 3, 0, 3 },
                    new[] { 3, 7, 10, 14, 10, 7, 3, 7, 10, 14, 17, 14, 10, 7, 3, 7 },
                    new[] { 5, 9, 12, 16, 12, 9, 5, 9, 12, 16, 19, 16, 12, 9, 5, 2 }),
            });

            s.Parts.Add(new Part   // stabs
            {
                Wave = Wave.Fm, FmIndex = 4.6f, Gain = 0.15f, Decay = 0.22f, Transpose = 12,
                Steps = Tile(new[] { 3, 0, 1, 3, 2, 0, 1, 2 },
                    new[] { 14, -1, -1, 12, -1, -1, 11, -1, 9, -1, -1, -1, 7, -1, -1, -1 },
                    new[] { 7, -1, 7, -1, 9, -1, 10, -1, 12, -1, -1, -1, -1, -1, -1, -1 },
                    new[] { 12, -1, 10, -1, 9, -1, 7, -1, 5, -1, 3, -1, 2, -1, 0, -1 },
                    Rest16),
            });

            s.Kick = Tile(new[] { 0, 1, 0, 1, 0, 1, 1, 1 },
                new[] { 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0 },
                new[] { 1, 0, 0, 1, 1, 0, 1, 0, 1, 0, 0, 1, 1, 0, 1, 1 });
            s.Snare = Tile(new[] { 0, 0, 0, 0, 0, 0, 0, 0 },
                new[] { 0, 0, 0, 0, 1, 0, 0, 1, 0, 0, 1, 0, 1, 0, 1, 1 });
            s.Hat = Tile(new[] { 0, 0, 0, 0, 0, 0, 0, 0 },
                new[] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 });
            return s;
        }

        // ---- rendering -----------------------------------------------------

        static float Midi(int note) => 440f * Mathf.Pow(2f, (note - 69) / 12f);

        static float DegreeFreq(Song s, int degree, int transpose)
        {
            int oct = Mathf.FloorToInt(degree / (float)s.Scale.Length);
            int idx = degree - oct * s.Scale.Length;
            return Midi(s.Root + transpose + s.Scale[idx] + 12 * oct);
        }

        static float[] Render(Song s)
        {
            float stepDur = 60f / s.Bpm / 4f;              // one 16th
            int total = Mathf.CeilToInt(stepDur * TotalSteps * Rate);
            var buf = new float[total];

            foreach (var part in s.Parts) RenderPart(buf, s, part, stepDur);
            RenderDrums(buf, s, stepDur);

            // normalise, then soften the peaks rather than clipping them
            float peak = 0f;
            for (int i = 0; i < buf.Length; i++) peak = Mathf.Max(peak, Mathf.Abs(buf[i]));
            float norm = peak > 0.0001f ? 0.82f / peak : 1f;
            for (int i = 0; i < buf.Length; i++)
            {
                float x = buf[i] * norm;
                buf[i] = x / (1f + 0.25f * Mathf.Abs(x));
            }

            // short crossfade so the loop point is inaudible
            int fade = Mathf.Min(1200, buf.Length / 16);
            for (int i = 0; i < fade; i++)
            {
                float k = i / (float)fade;
                buf[i] = Mathf.Lerp(buf[buf.Length - fade + i], buf[i], k);
            }
            return buf;
        }

        static void RenderPart(float[] buf, Song s, Part part, float stepDur)
        {
            int stepSamples = Mathf.RoundToInt(stepDur * Rate);

            for (int step = 0; step < part.Steps.Length && step < TotalSteps; step++)
            {
                int degree = part.Steps[step];
                if (degree < 0) continue;

                float freq = DegreeFreq(s, degree, part.Transpose);

                // swing: nudge the off-beat 16ths later
                int offset = ((step & 1) == 1) ? Mathf.RoundToInt(stepSamples * s.Swing) : 0;
                int start = step * stepSamples + offset;

                int len = Mathf.RoundToInt(part.Decay * 2.2f * Rate);
                if (start + len > buf.Length) len = buf.Length - start;
                if (len <= 0) continue;

                float phase = 0f, mod = 0f, lp = 0f;
                float dt = 1f / Rate;
                const float Attack = 0.004f;

                for (int i = 0; i < len; i++)
                {
                    float t = i * dt;
                    float env = Mathf.Min(t / Attack, 1f) * Mathf.Exp(-t / part.Decay);

                    // Only give up once the note has actually decayed. The attack
                    // ramp starts at zero, so testing before it finishes kills
                    // every note on its first sample.
                    if (t > Attack && env < 0.0005f) break;

                    phase += freq * dt;
                    if (phase > 1f) phase -= 1f;

                    float v;
                    switch (part.Wave)
                    {
                        case Wave.Saw: v = 2f * phase - 1f; break;
                        case Wave.Sine: v = Mathf.Sin(phase * 2f * Mathf.PI); break;
                        case Wave.Tri: v = 4f * Mathf.Abs(phase - 0.5f) - 1f; break;
                        case Wave.Fm:
                            mod += freq * 2f * dt;
                            if (mod > 1f) mod -= 1f;
                            v = Mathf.Sin(phase * 2f * Mathf.PI
                                          + part.FmIndex * env * Mathf.Sin(mod * 2f * Mathf.PI));
                            break;
                        default: v = phase < part.Duty ? 1f : -1f; break;
                    }

                    if (part.Lowpass)
                    {
                        lp += (v - lp) * 0.22f;     // tame the saw into something bass-like
                        v = lp;
                    }

                    buf[start + i] += v * env * part.Gain;
                }
            }
        }

        static void RenderDrums(float[] buf, Song s, float stepDur)
        {
            int stepSamples = Mathf.RoundToInt(stepDur * Rate);
            float dt = 1f / Rate;

            for (int step = 0; step < TotalSteps; step++)
            {
                int start = step * stepSamples;

                if (s.Kick != null && step < s.Kick.Length && s.Kick[step] == 1)
                {
                    int len = Mathf.Min(Mathf.RoundToInt(0.26f * Rate), buf.Length - start);
                    float ph = 0f;
                    for (int i = 0; i < len; i++)
                    {
                        float t = i * dt;
                        float f = Mathf.Lerp(145f, 46f, Mathf.Clamp01(t / 0.07f));
                        ph += f * dt;
                        buf[start + i] += Mathf.Sin(ph * 2f * Mathf.PI) * Mathf.Exp(-t / 0.10f) * 0.5f;
                    }
                }

                if (s.Snare != null && step < s.Snare.Length && s.Snare[step] == 1)
                {
                    int len = Mathf.Min(Mathf.RoundToInt(0.18f * Rate), buf.Length - start);
                    for (int i = 0; i < len; i++)
                    {
                        float t = i * dt;
                        float e = Mathf.Exp(-t / 0.055f);
                        buf[start + i] += (Noise(start + i) * 0.30f
                                         + Mathf.Sin(t * 195f * 2f * Mathf.PI) * 0.12f) * e;
                    }
                }

                if (s.Hat != null && step < s.Hat.Length && s.Hat[step] == 1)
                {
                    int len = Mathf.Min(Mathf.RoundToInt(0.06f * Rate), buf.Length - start);
                    for (int i = 0; i < len; i++)
                    {
                        float t = i * dt;
                        // difference of noise ~ a cheap high-pass, so it stays crisp
                        float n = Noise(start + i) - Noise(start + i - 1);
                        buf[start + i] += n * Mathf.Exp(-t / 0.014f) * 0.16f;
                    }
                }
            }
        }

        /// 16-bit mono WAV, so a rendered theme can be listened to or measured
        /// outside the game.
        public static void WriteWav(string path, float[] data)
        {
            using var fs = new FileStream(path, FileMode.Create);
            using var w = new BinaryWriter(fs);
            int bytes = data.Length * 2;

            w.Write(new[] { 'R', 'I', 'F', 'F' });
            w.Write(36 + bytes);
            w.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
            w.Write(16);
            w.Write((short)1);            // PCM
            w.Write((short)1);            // mono
            w.Write(Rate);
            w.Write(Rate * 2);            // byte rate
            w.Write((short)2);            // block align
            w.Write((short)16);           // bits
            w.Write(new[] { 'd', 'a', 't', 'a' });
            w.Write(bytes);

            foreach (var v in data) w.Write((short)(Mathf.Clamp(v, -1f, 1f) * 32767f));
        }

        static float Noise(int i)
        {
            int x = (i << 13) ^ i;
            return 1f - ((x * (x * x * 15731 + 789221) + 1376312589) & 0x7fffffff) / 1073741824f;
        }
    }
}
