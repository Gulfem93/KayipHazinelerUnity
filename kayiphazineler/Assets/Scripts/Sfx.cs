using System.Collections.Generic;
using UnityEngine;

namespace KayipHazineler
{
    // Ses efektleri: dosya gerektirmeden çalışma anında sentezlenir
    public static class Sfx
    {
        const int Rate = 44100;
        static AudioSource src;
        static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        static readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();
        public static bool On = true;

        enum Wave { Sine, Square, Saw, Tri, Noise }

        // Bir ses katmanı: başlangıç/bitiş frekansı, süre, gecikme ve dalga biçimi
        struct Tone
        {
            public float f0, f1, dur, delay, vol;
            public Wave wave;
            public Tone(Wave wv, float a, float b, float d, float v = .5f, float del = 0) { wave = wv; f0 = a; f1 = b; dur = d; vol = v; delay = del; }
        }

        public static void Init(GameObject host)
        {
            src = host.AddComponent<AudioSource>();
            src.playOnAwake = false;
            On = Save.AudioOn;
            Make("step", new Tone(Wave.Noise, 900, 300, .04f, .12f));
            Make("swing", new Tone(Wave.Noise, 2400, 600, .12f, .25f));
            Make("hit", new Tone(Wave.Square, 220, 90, .09f, .3f), new Tone(Wave.Noise, 1500, 400, .06f, .2f));
            Make("crit", new Tone(Wave.Square, 880, 1320, .12f, .25f));
            Make("kill", new Tone(Wave.Square, 330, 80, .22f, .3f), new Tone(Wave.Noise, 1000, 200, .2f, .2f));
            Make("coin", new Tone(Wave.Square, 988, 988, .06f, .2f), new Tone(Wave.Square, 1319, 1319, .12f, .2f, .06f));
            Make("key", new Tone(Wave.Tri, 784, 784, .1f, .4f), new Tone(Wave.Tri, 988, 988, .1f, .4f, .1f), new Tone(Wave.Tri, 1319, 1319, .25f, .4f, .2f));
            Make("chest", new Tone(Wave.Square, 523, 523, .08f, .2f), new Tone(Wave.Square, 659, 659, .08f, .2f, .08f), new Tone(Wave.Square, 784, 784, .08f, .2f, .16f), new Tone(Wave.Square, 1047, 1047, .2f, .2f, .24f));
            Make("hurt", new Tone(Wave.Saw, 300, 120, .18f, .35f));
            Make("block", new Tone(Wave.Tri, 1200, 900, .12f, .35f));
            Make("level", new Tone(Wave.Tri, 523, 523, .1f, .4f), new Tone(Wave.Tri, 659, 659, .1f, .4f, .1f), new Tone(Wave.Tri, 784, 784, .1f, .4f, .2f), new Tone(Wave.Tri, 1047, 1047, .35f, .4f, .3f));
            Make("potion", new Tone(Wave.Sine, 400, 900, .25f, .4f));
            Make("lamp", new Tone(Wave.Sine, 600, 1200, .3f, .3f));
            Make("spin", new Tone(Wave.Noise, 600, 3000, .25f, .3f));
            Make("jump", new Tone(Wave.Square, 300, 700, .18f, .2f));
            Make("shield", new Tone(Wave.Tri, 500, 1000, .35f, .35f));
            Make("bolt", new Tone(Wave.Sine, 1400, 500, .2f, .35f));
            Make("portal", new Tone(Wave.Sine, 300, 1200, .5f, .35f), new Tone(Wave.Tri, 450, 1800, .5f, .2f));
            Make("rumble", new Tone(Wave.Noise, 160, 80, .6f, .25f));
            Make("shift", new Tone(Wave.Noise, 300, 60, .45f, .4f), new Tone(Wave.Saw, 70, 40, .4f, .25f));
            Make("turn", new Tone(Wave.Square, 200, 260, .15f, .15f));
            Make("slide", new Tone(Wave.Noise, 500, 1500, .15f, .15f));
            Make("buy", new Tone(Wave.Square, 784, 784, .07f, .2f), new Tone(Wave.Square, 1175, 1175, .15f, .2f, .07f));
            Make("gameover", new Tone(Wave.Tri, 392, 392, .25f, .4f), new Tone(Wave.Tri, 330, 330, .25f, .4f, .25f), new Tone(Wave.Tri, 262, 200, .6f, .4f, .5f));
            Make("click", new Tone(Wave.Square, 660, 660, .03f, .12f));
        }

        static void Make(string name, params Tone[] tones)
        {
            float total = 0;
            foreach (var t in tones) total = Mathf.Max(total, t.delay + t.dur);
            int n = Mathf.CeilToInt(total * Rate);
            var data = new float[n];
            uint seed = 12345;
            foreach (var t in tones)
            {
                int start = (int)(t.delay * Rate), len = (int)(t.dur * Rate);
                float phase = 0, lastNoise = 0;
                for (int i = 0; i < len && start + i < n; i++)
                {
                    float k = (float)i / len;
                    float f = Mathf.Lerp(t.f0, t.f1, k);
                    phase += f / Rate;
                    float p = phase - Mathf.Floor(phase), v;
                    switch (t.wave)
                    {
                        case Wave.Sine: v = Mathf.Sin(p * Mathf.PI * 2); break;
                        case Wave.Square: v = p < .5f ? .6f : -.6f; break;
                        case Wave.Saw: v = (p * 2 - 1) * .7f; break;
                        case Wave.Tri: v = 1 - 4 * Mathf.Abs(p - .5f); break;
                        default:
                            // Frekansla süzülen gürültü: düşük frekans daha boğuk
                            seed = seed * 1664525 + 1013904223;
                            float r = (seed >> 8) / 8388608f - 1f;
                            float a = Mathf.Clamp01(f / 4000f);
                            lastNoise += (r - lastNoise) * a;
                            v = lastNoise * 1.5f;
                            break;
                    }
                    // Kısa atak, doğrusal sönüm
                    float env = Mathf.Min(1, i / (Rate * .004f)) * (1 - k);
                    data[start + i] += v * env * t.vol;
                }
            }
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            clips[name] = clip;
        }

        public static void Play(string name)
        {
            if (!On || src == null || !clips.TryGetValue(name, out var c)) return;
            // Aynı ses çok sık üst üste binmesin
            if (lastPlayed.TryGetValue(name, out var t) && Time.unscaledTime - t < .03f) return;
            lastPlayed[name] = Time.unscaledTime;
            src.PlayOneShot(c, .8f);
        }
    }
}
