using UnityEngine;

// Synthesised sound effects so the game runs without any imported audio.
public static class ProceduralSfx
{
    const int SampleRate = 44100;

    static AudioClip nailShot, nailPierce, nailCharge;
    public static AudioClip NailShot => nailShot != null ? nailShot : nailShot = BuildImpact("sfx_nailgun_shot", 0.16f, 360f, 70f, 0.95f, 42f);
    public static AudioClip NailPierce => nailPierce != null ? nailPierce : nailPierce = BuildImpact("sfx_nailgun_pierce", 0.36f, 170f, 35f, 1.4f, 18f);
    public static AudioClip NailCharge => nailCharge != null ? nailCharge : nailCharge = BuildNailCharge();

    static AudioClip sonic, sonicHeavy;
    public static AudioClip Sonic => sonic != null ? sonic : sonic = BuildSonic(false);
    public static AudioClip SonicHeavy => sonicHeavy != null ? sonicHeavy : sonicHeavy = BuildSonic(true);

    static AudioClip swing, swingHeavy, hit, hitHeavy, upgrade, shatter, chainsawLoop, snip, squelch;

    public static AudioClip Swing => swing != null ? swing : swing = BuildWhoosh("sfx_swing", 0.2f, 0.4f);
    public static AudioClip SwingHeavy => swingHeavy != null ? swingHeavy : swingHeavy = BuildWhoosh("sfx_swing_heavy", 0.32f, 0.22f);
    public static AudioClip Hit => hit != null ? hit : hit = BuildImpact("sfx_hit", 0.28f, 170f, 70f, 0.55f, 26f);
    public static AudioClip HitHeavy => hitHeavy != null ? hitHeavy : hitHeavy = BuildImpact("sfx_hit_heavy", 0.5f, 110f, 42f, 0.8f, 14f);
    public static AudioClip Upgrade => upgrade != null ? upgrade : upgrade = BuildUpgrade();
    public static AudioClip Shatter => shatter != null ? shatter : shatter = BuildShatter();
    public static AudioClip ChainsawLoop => chainsawLoop != null ? chainsawLoop : chainsawLoop = BuildChainsawLoop();
    public static AudioClip Snip => snip != null ? snip : snip = BuildSnip();
    public static AudioClip Squelch => squelch != null ? squelch : squelch = BuildSquelch();

    static AudioClip BuildNailCharge()
    {
        var samples = new float[SampleRate / 2];
        for (int i = 0; i < samples.Length; i++)
        {
            float t = i / (float)SampleRate;
            float phase = t * 120f * Mathf.PI * 2f;
            float motor = Mathf.Sin(phase) + 0.35f * Mathf.Sin(phase * 2f) + 0.15f * Mathf.Sin(phase * 5f);
            float whine = Mathf.Sin(t * 720f * Mathf.PI * 2f) * 0.22f;
            samples[i] = motor * (0.75f + 0.25f * Mathf.Sin(t * 24f * Mathf.PI * 2f)) + whine;
        }
        return Create("sfx_nailgun_charge_loop", samples);
    }

    static AudioClip BuildSonic(bool heavy)
    {
        float duration = heavy ? 0.55f : 0.28f;
        var samples = new float[Mathf.CeilToInt(duration * SampleRate)];
        var rng = new System.Random(heavy ? 74 : 37);
        float phase = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            float t = i / (float)SampleRate;
            float u = t / duration;
            float frequency = Mathf.Lerp(heavy ? 140f : 620f, heavy ? 45f : 280f, u);
            phase += frequency * Mathf.PI * 2f / SampleRate;
            float envelope = Mathf.Min(1f, t / 0.012f) * Mathf.Pow(1f - u, 1.5f);
            float tone = Mathf.Sin(phase) + 0.35f * Mathf.Sin(phase * 2.01f) + 0.15f * Mathf.Sin(phase * 3f);
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0) * (heavy ? 0.35f : 0.08f);
            samples[i] = (tone * 0.6f + noise) * envelope;
        }
        return Create(heavy ? "sfx_sonic_crush" : "sfx_sonic_push", samples);
    }

    static AudioClip Create(string name, float[] data)
    {
        float peak = 0.0001f;
        for (int i = 0; i < data.Length; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        float gain = 0.9f / peak;
        for (int i = 0; i < data.Length; i++) data[i] *= gain;
        var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // Low-passed noise whose cutoff sweeps up and back down.
    static AudioClip BuildWhoosh(string name, float duration, float maxCutoff)
    {
        int n = (int)(SampleRate * duration);
        var d = new float[n];
        var rng = new System.Random(11);
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float u = i / (float)n;
            float arc = Mathf.Sin(u * Mathf.PI);
            float cutoff = Mathf.Lerp(0.03f, maxCutoff, arc);
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            lp += (noise - lp) * cutoff;
            d[i] = lp * Mathf.Pow(arc, 1.6f);
        }
        return Create(name, d);
    }

    // Pitch-dropping thump + short noise crack + 2 ms click.
    static AudioClip BuildImpact(string name, float duration, float f0, float f1, float crack, float decay)
    {
        int n = (int)(SampleRate * duration);
        var d = new float[n];
        var rng = new System.Random(23);
        float phase = 0f, lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SampleRate;
            float u = i / (float)n;
            float f = Mathf.Lerp(f0, f1, Mathf.Sqrt(u));
            phase += 2f * Mathf.PI * f / SampleRate;
            float thump = Mathf.Sin(phase) * Mathf.Exp(-t * decay * 0.5f);

            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            lp += (noise - lp) * 0.5f;
            float crackSample = (noise - lp) * Mathf.Exp(-t * decay * 2.2f) * crack;

            float click = t < 0.002f ? (1f - t / 0.002f) * 0.8f : 0f;
            d[i] = thump + crackSample + click;
        }
        return Create(name, d);
    }

    // Rising shimmer sweep that ends in a bright ping.
    static AudioClip BuildUpgrade()
    {
        const float duration = 0.9f;
        int n = (int)(SampleRate * duration);
        var d = new float[n];
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SampleRate;
            float u = i / (float)n;
            float f = 240f * Mathf.Pow(5f, Mathf.Min(u / 0.7f, 1f));
            phase += 2f * Mathf.PI * f / SampleRate;
            float tone = Mathf.Sin(phase) + 0.4f * Mathf.Sin(phase * 2f) + 0.2f * Mathf.Sin(phase * 3f);
            float shimmer = 0.6f + 0.4f * Mathf.Sin(t * 2f * Mathf.PI * 22f);
            float env = Mathf.Clamp01(t / 0.05f) * (u < 0.7f ? Mathf.Lerp(0.5f, 1f, u / 0.7f) : Mathf.Exp(-(u - 0.7f) * 9f));
            float ping = u > 0.68f ? Mathf.Sin(2f * Mathf.PI * 1760f * (t - 0.68f * duration)) * Mathf.Exp(-(u - 0.68f) * 14f) * 0.8f : 0f;
            d[i] = tone * shimmer * env * 0.5f + ping;
        }
        return Create("sfx_upgrade", d);
    }

    // Engine buzz: a sawtooth-like stack of harmonics, a rasp of random partials and a 24 Hz chug.
    // Every partial has a whole number of cycles in the clip, so it loops without a click.
    static AudioClip BuildChainsawLoop()
    {
        const float duration = 0.5f;
        const float fundamental = 80f;
        int n = (int)(SampleRate * duration);
        var d = new float[n];
        var rng = new System.Random(53);

        var partialFreq = new float[40];
        var partialPhase = new float[40];
        for (int p = 0; p < partialFreq.Length; p++)
        {
            partialFreq[p] = 2f * rng.Next(100, 1500); // multiples of 2 Hz = whole cycles in 0.5 s
            partialPhase[p] = (float)(rng.NextDouble() * Mathf.PI * 2f);
        }

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SampleRate;
            float tone = 0f;
            for (int k = 1; k <= 24; k++) tone += Mathf.Sin(2f * Mathf.PI * k * fundamental * t) / Mathf.Pow(k, 0.8f);

            float rasp = 0f;
            for (int p = 0; p < partialFreq.Length; p++) rasp += Mathf.Sin(2f * Mathf.PI * partialFreq[p] * t + partialPhase[p]);

            float chug = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * 24f * t);
            d[i] = (tone * 0.5f + rasp * 0.12f) * chug;
        }
        return Create("sfx_chainsaw_loop", d);
    }

    // Two blades meeting: a bright metallic ring (inharmonic partials) over a short noise click.
    static AudioClip BuildSnip()
    {
        const float duration = 0.22f;
        int n = (int)(SampleRate * duration);
        var d = new float[n];
        var rng = new System.Random(71);
        float[] partials = { 2350f, 3320f, 4870f, 6140f };
        float[] gains = { 1f, 0.7f, 0.45f, 0.3f };
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SampleRate;
            float ring = 0f;
            for (int p = 0; p < partials.Length; p++)
                ring += Mathf.Sin(2f * Mathf.PI * partials[p] * t) * gains[p] * Mathf.Exp(-t * (28f + 18f * p));
            float click = (float)(rng.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-t * 400f);
            d[i] = ring * 0.6f + click;
        }
        return Create("sfx_snip", d);
    }

    // Wet impact: low-passed noise whose cutoff falls, plus a short low thump.
    static AudioClip BuildSquelch()
    {
        const float duration = 0.32f;
        int n = (int)(SampleRate * duration);
        var d = new float[n];
        var rng = new System.Random(83);
        float lp = 0f, phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SampleRate;
            float u = i / (float)n;
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            lp += (noise - lp) * Mathf.Lerp(0.35f, 0.04f, Mathf.Sqrt(u));
            // Bubbly wobble on the amplitude.
            float wobble = 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 38f * t);
            phase += 2f * Mathf.PI * Mathf.Lerp(120f, 55f, u) / SampleRate;
            d[i] = (lp * 1.4f * wobble + Mathf.Sin(phase) * 0.5f) * Mathf.Exp(-t * 9f);
        }
        return Create("sfx_squelch", d);
    }

    // Big noise burst + sub thump + scattered crack clicks.
    static AudioClip BuildShatter()
    {
        const float duration = 0.8f;
        int n = (int)(SampleRate * duration);
        var d = new float[n];
        var rng = new System.Random(37);
        float lp = 0f, phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SampleRate;
            phase += 2f * Mathf.PI * Mathf.Lerp(90f, 38f, Mathf.Sqrt(t / duration)) / SampleRate;
            float sub = Mathf.Sin(phase) * Mathf.Exp(-t * 5f);

            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            lp += (noise - lp) * Mathf.Lerp(0.7f, 0.08f, t / duration);
            float burst = lp * Mathf.Exp(-t * 6f);

            float click = (rng.NextDouble() < 0.0015 && t > 0.03f) ? (float)(rng.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-t * 3f) : 0f;
            d[i] = sub * 0.9f + burst * 1.2f + click * 1.5f;
        }
        return Create("sfx_shatter", d);
    }
}
