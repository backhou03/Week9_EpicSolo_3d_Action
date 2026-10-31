using UnityEngine;

// Rifle-only procedural effects: muzzle flash, tracers, shells, blasts, dash streak and machine gun aura.
// Particles are emitted into a few persistent systems so rapid fire creates no objects per shot.
public class RifleFx : MonoBehaviour
{
    const int TracerCount = 10;
    const int RingCount = 6;
    const int RingSegments = 40;
    const int VoiceCount = 6;
    const float TracerSpeed = 220f;
    const float AuraWarning = 6f;
    static readonly Color NormalColor = new Color(1f, 0.88f, 0.45f, 1f);
    static readonly Color ExplosiveColor = new Color(1f, 0.45f, 0.12f, 1f);
    static readonly Color MachineColor = new Color(1f, 0.6f, 0.25f, 1f);
    static readonly Color BrassColor = new Color(1f, 0.78f, 0.3f, 1f);

    class Tracer
    {
        public LineRenderer line;
        public Vector3 start, end;
        public float age, life, tail;
        public bool active;
    }

    class RingState
    {
        public LineRenderer line;
        public Vector3 center;
        public Color color;
        public float radius, width, age, life;
        public bool active;
    }

    static AudioClip shotClip, machineShotClip, clickClip;

    ThirdPersonCamera cam;
    Material softMaterial, lineMaterial;
    Texture2D softTexture;
    AudioSource[] voices;
    Tracer[] tracers;
    RingState[] rings;
    Vector3[] ringPoints;
    ParticleSystem flashPs, sparkPs, shellPs, firePs, smokePs, emberPs;
    Light muzzleLight, blastLight;
    TrailRenderer dashTrail;
    float muzzleLeft, muzzleTime, muzzleIntensity, blastLeft, blastTime, blastIntensity;
    float dashLeft, auraAccumulator, machineLeft;
    int tracerNext, ringNext, voiceNext;
    bool machineOn;
    Transform follow;

    static AudioClip ShotClip => shotClip != null ? shotClip : shotClip = BuildShot("sfx_rifle_shot", 0.18f, 85f, 55f);
    static AudioClip MachineShotClip => machineShotClip != null ? machineShotClip : machineShotClip = BuildShot("sfx_machine_shot", 0.16f, 65f, 45f);
    static AudioClip ClickClip => clickClip != null ? clickClip : clickClip = BuildClick();

    public void Configure(ThirdPersonCamera camera) => cam = camera;

    void Awake()
    {
        softTexture = BuildSoftTexture();
        softMaterial = new Material(Shader.Find("Sprites/Default")) { mainTexture = softTexture };
        lineMaterial = new Material(Shader.Find("Sprites/Default"));

        voices = new AudioSource[VoiceCount];
        for (int i = 0; i < VoiceCount; i++)
        {
            voices[i] = gameObject.AddComponent<AudioSource>();
            voices[i].spatialBlend = 0f;
            voices[i].playOnAwake = false;
        }

        tracers = new Tracer[TracerCount];
        for (int i = 0; i < TracerCount; i++) tracers[i] = new Tracer { line = MakeLine("RifleTracer", 2, false) };
        rings = new RingState[RingCount];
        for (int i = 0; i < RingCount; i++) rings[i] = new RingState { line = MakeLine("RifleRing", RingSegments, true) };
        ringPoints = new Vector3[RingSegments];

        var white = Ramp(Color.white, Color.white, Color.white, 1f);
        var fire = Ramp(new Color(1f, 0.95f, 0.6f), new Color(1f, 0.5f, 0.12f), new Color(0.25f, 0.08f, 0.05f), 1f);
        var smoke = Ramp(new Color(0.55f, 0.55f, 0.55f), new Color(0.38f, 0.38f, 0.38f), new Color(0.2f, 0.2f, 0.2f), 0.5f);
        var ember = Ramp(new Color(1f, 0.9f, 0.4f), new Color(1f, 0.5f, 0.15f), new Color(0.8f, 0.15f, 0.05f), 1f);
        var shrink = Curve(1f, 0.25f);
        flashPs = MakePs("Flash", false, 0f, white, shrink, 0f);
        sparkPs = MakePs("Sparks", true, 0.8f, white, shrink, 0f);
        shellPs = MakePs("Shells", false, 3f, white, Curve(1f, 0.9f), 720f);
        firePs = MakePs("Fire", false, -0.1f, fire, Curve(0.5f, 1.2f), 0f);
        smokePs = MakePs("Smoke", false, -0.05f, smoke, Curve(0.4f, 1.4f), 40f);
        emberPs = MakePs("Embers", false, 0f, ember, Curve(1f, 0f), 0f);

        muzzleLight = MakeLight("RifleMuzzleLight");
        blastLight = MakeLight("RifleBlastLight");

        var trailGo = new GameObject("RifleDashTrail");
        trailGo.transform.SetParent(transform, false);
        dashTrail = trailGo.AddComponent<TrailRenderer>();
        dashTrail.sharedMaterial = lineMaterial;
        dashTrail.time = 0.28f;
        dashTrail.minVertexDistance = 0.05f;
        dashTrail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(1f, 0f));
        dashTrail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        dashTrail.receiveShadows = false;
        dashTrail.emitting = false;
    }

    void OnDestroy()
    {
        if (softMaterial != null) Destroy(softMaterial);
        if (lineMaterial != null) Destroy(lineMaterial);
        if (softTexture != null) Destroy(softTexture);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        foreach (var t in tracers)
        {
            if (!t.active) continue;
            t.age += dt;
            float p = t.age / t.life * (1f + t.tail);
            float tail = Mathf.Clamp01(p - t.tail);
            if (tail >= 1f)
            {
                t.active = false;
                t.line.enabled = false;
                continue;
            }
            t.line.SetPosition(0, Vector3.Lerp(t.start, t.end, tail));
            t.line.SetPosition(1, Vector3.Lerp(t.start, t.end, Mathf.Min(1f, p)));
        }
        foreach (var r in rings)
        {
            if (!r.active) continue;
            r.age += dt;
            UpdateRing(r);
        }
        FadeLight(muzzleLight, ref muzzleLeft, muzzleTime, muzzleIntensity, dt);
        FadeLight(blastLight, ref blastLeft, blastTime, blastIntensity, dt);
        TickAura(dt);
    }

    void LateUpdate()
    {
        if (dashLeft <= 0f) return;
        dashLeft -= Time.deltaTime;
        if (follow != null) dashTrail.transform.position = follow.position + Vector3.up * 0.9f;
        if (dashLeft <= 0f) dashTrail.emitting = false;
    }

    // ---- Shots -----------------------------------------------------------

    // One bullet leaving the muzzle: flash, light, streak, ejected shell and the crack.
    // shake > 0 adds a camera kick; boost scales the blast for propulsion and dash shots.
    public void Shot(Vector3 muzzle, Vector3 dir, Vector3 end, bool explosive, bool machine, float shake, float boost = 1f)
    {
        Color color = explosive ? ExplosiveColor : machine ? MachineColor : NormalColor;
        float scale = (explosive ? 1.35f : 1f) * (machine ? 1.25f : 1f) * boost;

        Emit(flashPs, muzzle + dir * 0.12f, Vector3.zero, Color.Lerp(color, Color.white, 0.6f), 0.4f * scale, 0.05f);
        int streaks = 3 + (explosive ? 2 : 0) + (boost > 1f ? 2 : 0);
        for (int i = 0; i < streaks; i++)
            Emit(sparkPs, muzzle, (dir + Random.insideUnitSphere * 0.3f).normalized * Random.Range(10f, 16f) * scale, color, 0.035f * scale, 0.05f);
        SetLight(muzzleLight, ref muzzleLeft, ref muzzleTime, ref muzzleIntensity, muzzle + dir * 0.2f, color, 1.6f * scale, 3.5f * scale, 0.05f);

        // Brass flies out to the right, a little up and back.
        Vector3 right = Vector3.Cross(Vector3.up, dir);
        right = right.sqrMagnitude > 0.01f ? right.normalized : Vector3.right;
        Vector3 shellVelocity = right * Random.Range(1.8f, 3f) + Vector3.up * Random.Range(1.5f, 2.5f) - dir * 0.6f + Random.insideUnitSphere * 0.4f;
        Emit(shellPs, muzzle - dir * 0.5f, shellVelocity, BrassColor, 0.04f, 0.7f);
        if (machine && Random.value < 0.35f)
            Emit(smokePs, muzzle + dir * 0.15f, dir * 0.8f + Vector3.up * 0.5f, Color.white, 0.22f, 0.6f);

        SpawnTracer(muzzle, end, color, 0.03f * (explosive ? 1.6f : 1f) * (machine ? 1.3f : 1f) * Mathf.Sqrt(boost));
        Play(machine ? MachineShotClip : ShotClip, machine ? 0.5f : 0.6f, Random.Range(0.95f, 1.08f));
        if (shake > 0f) Punch(shake * (machine ? 1.3f : 1f), 0.06f, 0.6f, 0.1f);
    }

    // A bullet striking the world: a few sparks bouncing back and a dust puff.
    public void Impact(Vector3 point, Vector3 dir, bool ring = false)
    {
        Vector3 back = -dir;
        for (int i = 0; i < 4; i++)
            Emit(sparkPs, point, (back + Random.insideUnitSphere * 0.6f + Vector3.up * 0.3f).normalized * Random.Range(3f, 7f), NormalColor, 0.03f, Random.Range(0.12f, 0.22f));
        Emit(smokePs, point, back * 0.4f + Vector3.up * 0.6f, Color.white, 0.2f, 0.45f);
        if (ring) Ring(point + Vector3.up * 0.05f, 0.9f, 0.18f, new Color(1f, 1f, 1f, 0.6f), 0.06f);
    }

    // ---- Skills ----------------------------------------------------------

    // Explosive round bursting: core flash, fireball, smoke, sparks, shockwave and a boom.
    public void Explosion(Vector3 point, float radius, bool machine)
    {
        Emit(firePs, point, Vector3.zero, Color.white, radius * 1.4f, 0.1f);
        for (int i = 0; i < 5; i++)
            Emit(firePs, point + Random.insideUnitSphere * radius * 0.25f, Random.insideUnitSphere * radius * 1.4f + Vector3.up * 0.8f,
                Color.white, radius * Random.Range(0.35f, 0.6f), Random.Range(0.3f, 0.45f));
        for (int i = 0; i < 2; i++)
            Emit(smokePs, point + Random.insideUnitSphere * 0.3f, Random.insideUnitSphere + Vector3.up * 1.2f, Color.white, radius * 0.5f, 0.9f);
        for (int i = 0; i < 12; i++)
            Emit(sparkPs, point, (Random.onUnitSphere + Vector3.up * 0.5f) * Random.Range(6f, 12f), ExplosiveColor, 0.05f, Random.Range(0.25f, 0.4f));
        Ring(point, radius * 1.15f, 0.26f, new Color(1f, 0.6f, 0.2f, 0.9f), 0.16f);
        SetLight(blastLight, ref blastLeft, ref blastTime, ref blastIntensity, point + Vector3.up * 0.5f, ExplosiveColor, radius * 2.5f, 6f, 0.15f);
        Play(ProceduralSfx.HitHeavy, 0.55f, Random.Range(0.8f, 0.95f));
        Play(ProceduralSfx.Shatter, 0.3f, Random.Range(1f, 1.2f));
        Punch(machine ? 0.14f : 0.1f, 0.14f, 0f, 0.1f);
    }

    // E: the magazine is swapped for explosive rounds; embers spiral up around the avatar.
    public void Cast(Vector3 position, Color color, float radius)
    {
        Ring(position + Vector3.up * 0.05f, radius, 0.35f, color, 0.14f);
        EmberBurst(position, radius * 0.4f, 18);
        SetLight(blastLight, ref blastLeft, ref blastTime, ref blastIntensity, position + Vector3.up, color, radius * 2f, 3f, 0.2f);
        Play(ClickClip, 0.8f, 0.6f);
        Play(ProceduralSfx.Upgrade, 0.3f, 1.4f);
        Punch(0.06f, 0.15f, 1.5f, 0.2f);
    }

    // Q: the machine gun roars to life.
    public void Ultimate(Vector3 position)
    {
        Ring(position + Vector3.up * 0.05f, 4.5f, 0.45f, new Color(1f, 0.45f, 0.12f, 0.95f), 0.25f);
        Ring(position + Vector3.up * 0.05f, 2.8f, 0.3f, new Color(1f, 1f, 0.8f, 0.9f), 0.12f);
        EmberBurst(position, 1f, 45);
        Emit(firePs, position + Vector3.up, Vector3.zero, Color.white, 2.2f, 0.12f);
        SetLight(blastLight, ref blastLeft, ref blastTime, ref blastIntensity, position + Vector3.up, ExplosiveColor, 7f, 6f, 0.3f);
        Play(ProceduralSfx.HitHeavy, 0.9f, 0.7f);
        Play(ProceduralSfx.Shatter, 0.5f, 0.9f);
        Punch(0.3f, 0.35f, -6f, 0.4f);
    }

    // Expanding ring for entrance bursts; callers pick the size and tone.
    public void Ring(Vector3 center, float radius, float life, Color color, float width)
    {
        var r = rings[ringNext];
        ringNext = (ringNext + 1) % rings.Length;
        r.active = true;
        r.center = center;
        r.radius = radius;
        r.life = life;
        r.color = color;
        r.width = width;
        r.age = 0f;
        r.line.enabled = true;
        UpdateRing(r);
    }

    public void Punch(float shake, float shakeTime, float fov, float fovTime)
    {
        if (cam == null) return;
        if (shake > 0f) cam.Shake(shake, shakeTime);
        if (fov != 0f) cam.FovPunch(fov, fovTime);
    }

    // ---- Reload ----------------------------------------------------------

    public void Click(float pitch, float volume) => Play(ClickClip, volume, pitch * Random.Range(0.97f, 1.03f));

    // Reload finished: a small spark at the muzzle.
    public void Ready(Vector3 muzzle)
    {
        Emit(flashPs, muzzle, Vector3.zero, Color.white, 0.25f, 0.08f);
        for (int i = 0; i < 4; i++)
            Emit(sparkPs, muzzle, (Vector3.up + Random.insideUnitSphere * 0.6f) * Random.Range(2f, 4f), Color.white, 0.03f, 0.18f);
    }

    // ---- Dash ------------------------------------------------------------

    public void DashStart(Vector3 feet, Vector3 moveDir, Transform target, float duration, bool machine)
    {
        Color color = machine ? new Color(1f, 0.3f, 0.1f, 0.8f) : new Color(1f, 0.7f, 0.3f, 0.7f);
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
            new[] { new GradientAlphaKey(color.a, 0f), new GradientAlphaKey(0f, 1f) });
        dashTrail.colorGradient = gradient;
        follow = target;
        dashTrail.emitting = false;
        dashTrail.transform.position = feet + Vector3.up * 0.9f;
        dashTrail.Clear();
        dashTrail.emitting = true;
        dashLeft = duration;
        // Dust kicked up behind the avatar.
        for (int i = 0; i < 4; i++)
            Emit(smokePs, feet + Vector3.up * 0.1f, -moveDir * Random.Range(0.5f, 1.5f) + Random.insideUnitSphere * 0.4f, Color.white, 0.3f, 0.5f);
        Ring(feet + Vector3.up * 0.05f, 1.4f, 0.2f, new Color(1f, 1f, 1f, 0.5f), 0.07f);
        Punch(0.1f, 0.12f, -3.5f, 0.22f);
    }

    // ---- Machine gun aura ------------------------------------------------

    // Called every frame with the remaining machine gun time; the end puff plays when it reaches zero.
    public void SetMachineGun(Transform target, float remaining)
    {
        if (remaining <= 0f)
        {
            if (!machineOn) return;
            machineOn = false;
            blastLight.enabled = false;
            Vector3 position = target.position;
            Ring(position + Vector3.up * 0.05f, 2f, 0.4f, new Color(0.7f, 0.7f, 0.7f, 0.7f), 0.1f);
            for (int i = 0; i < 5; i++)
                Emit(smokePs, position + Vector3.up * Random.Range(0.3f, 1.2f), Random.insideUnitSphere + Vector3.up * 0.8f, Color.white, 0.5f, 1f);
            Play(ProceduralSfx.Swing, 0.5f, 0.6f);
            return;
        }
        machineOn = true;
        follow = target;
        machineLeft = remaining;
    }

    void TickAura(float dt)
    {
        if (!machineOn || follow == null) return;
        bool warning = machineLeft < AuraWarning;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * (warning ? 18f : 6f));
        // The aura rides on the blast light unless a blast is flashing.
        if (blastLeft <= 0f)
        {
            blastLight.enabled = true;
            blastLight.color = ExplosiveColor;
            blastLight.range = 3.5f;
            blastLight.transform.position = follow.position + Vector3.up;
            blastLight.intensity = Mathf.Lerp(0.6f, 1.8f, pulse) * (warning && pulse < 0.3f ? 0.3f : 1f);
        }
        auraAccumulator += dt * (warning ? 30f : 22f);
        while (auraAccumulator >= 1f)
        {
            auraAccumulator -= 1f;
            Vector2 around = Random.insideUnitCircle * 0.55f;
            Vector3 position = follow.position + new Vector3(around.x, Random.Range(0.1f, 1.3f), around.y);
            Emit(emberPs, position, new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(1.2f, 2f), Random.Range(-0.3f, 0.3f)),
                Color.Lerp(new Color(1f, 0.85f, 0.3f), new Color(1f, 0.35f, 0.1f), Random.value), Random.Range(0.05f, 0.09f), Random.Range(0.7f, 1.1f));
        }
    }

    // ---- Internals -------------------------------------------------------

    void EmberBurst(Vector3 position, float radius, int count)
    {
        for (int i = 0; i < count; i++)
        {
            Vector2 around = Random.insideUnitCircle * radius;
            Emit(emberPs, position + new Vector3(around.x, Random.Range(0.05f, 0.8f), around.y),
                new Vector3(around.x, Random.Range(2f, 4f), around.y),
                Color.Lerp(new Color(1f, 0.9f, 0.4f), new Color(1f, 0.4f, 0.1f), Random.value), Random.Range(0.05f, 0.1f), Random.Range(0.5f, 0.9f));
        }
    }

    void SpawnTracer(Vector3 start, Vector3 end, Color color, float width)
    {
        var t = tracers[tracerNext];
        tracerNext = (tracerNext + 1) % tracers.Length;
        float distance = Mathf.Max(0.1f, Vector3.Distance(start, end));
        t.start = start;
        t.end = end;
        t.age = 0f;
        t.life = Mathf.Clamp(distance / TracerSpeed, 0.03f, 0.15f);
        t.tail = Mathf.Min(0.5f, 6f / distance);
        t.active = true;
        t.line.startWidth = width * 0.3f;
        t.line.endWidth = width;
        t.line.startColor = new Color(color.r, color.g, color.b, 0.1f);
        t.line.endColor = Color.Lerp(color, Color.white, 0.6f);
        t.line.SetPosition(0, start);
        t.line.SetPosition(1, start);
        t.line.enabled = true;
    }

    void UpdateRing(RingState r)
    {
        float k = Mathf.Clamp01(r.age / r.life);
        if (k >= 1f)
        {
            r.active = false;
            r.line.enabled = false;
            return;
        }
        float radius = Mathf.Lerp(r.radius * 0.25f, r.radius, 1f - (1f - k) * (1f - k));
        for (int i = 0; i < RingSegments; i++)
        {
            float angle = i * Mathf.PI * 2f / RingSegments;
            ringPoints[i] = r.center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
        }
        r.line.SetPositions(ringPoints);
        r.line.widthMultiplier = r.width * (1f - k);
        var c = new Color(r.color.r, r.color.g, r.color.b, r.color.a * (1f - k));
        r.line.startColor = r.line.endColor = c;
    }

    static void SetLight(Light light, ref float left, ref float time, ref float baseIntensity, Vector3 position, Color color, float range, float intensity, float duration)
    {
        light.transform.position = position;
        light.color = color;
        light.range = range;
        light.intensity = intensity;
        light.enabled = true;
        left = time = duration;
        baseIntensity = intensity;
    }

    static void FadeLight(Light light, ref float left, float time, float baseIntensity, float dt)
    {
        if (left <= 0f) return;
        left -= dt;
        if (left <= 0f) light.enabled = false;
        else light.intensity = baseIntensity * left / time;
    }

    void Play(AudioClip clip, float volume, float pitch)
    {
        var voice = voices[voiceNext];
        voiceNext = (voiceNext + 1) % voices.Length;
        voice.pitch = pitch;
        voice.PlayOneShot(clip, volume);
    }

    static void Emit(ParticleSystem ps, Vector3 position, Vector3 velocity, Color color, float size, float life)
    {
        var p = new ParticleSystem.EmitParams
        {
            position = position,
            velocity = velocity,
            startColor = color,
            startSize = size,
            startLifetime = life,
            rotation = Random.Range(0f, 360f),
        };
        ps.Emit(p, 1);
    }

    LineRenderer MakeLine(string name, int points, bool loop)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var line = go.AddComponent<LineRenderer>();
        line.sharedMaterial = lineMaterial;
        line.useWorldSpace = true;
        line.positionCount = points;
        line.loop = loop;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.enabled = false;
        return line;
    }

    Light MakeLight(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.shadows = LightShadows.None;
        light.enabled = false;
        return light;
    }

    ParticleSystem MakePs(string name, bool stretch, float gravity, Gradient color, AnimationCurve size, float spin)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = gravity;
        main.maxParticles = 256;
        var emission = ps.emission;
        emission.enabled = false;
        var shape = ps.shape;
        shape.enabled = false;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = color;
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, size);
        if (spin != 0f)
        {
            var rol = ps.rotationOverLifetime;
            rol.enabled = true;
            rol.z = new ParticleSystem.MinMaxCurve(-spin * Mathf.Deg2Rad, spin * Mathf.Deg2Rad);
        }

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = softMaterial;
        r.renderMode = stretch ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
        r.velocityScale = 0.05f;
        r.lengthScale = 3f;
        ps.Play();
        return ps;
    }

    static Gradient Ramp(Color start, Color mid, Color end, float peakAlpha)
    {
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(start, 0f), new GradientColorKey(mid, 0.4f), new GradientColorKey(end, 1f) },
            new[] { new GradientAlphaKey(peakAlpha, 0f), new GradientAlphaKey(peakAlpha, 0.5f), new GradientAlphaKey(0f, 1f) });
        return g;
    }

    static AnimationCurve Curve(float from, float to) => new AnimationCurve(new Keyframe(0f, from), new Keyframe(1f, to));

    // Soft round blob so quads read as glowing puffs instead of squares.
    static Texture2D BuildSoftTexture()
    {
        const int size = 32;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f;
                float dy = (y + 0.5f) / size * 2f - 1f;
                float alpha = Mathf.SmoothStep(1f, 0f, Mathf.Sqrt(dx * dx + dy * dy));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();
        return tex;
    }

    // Noise snap on top of a falling low thump.
    static AudioClip BuildShot(string name, float length, float thump, float snap)
    {
        const int rate = 44100;
        int count = (int)(rate * length);
        var data = new float[count];
        var rng = new System.Random(7);
        float phase = 0f;
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)rate;
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            phase += 2f * Mathf.PI * Mathf.Lerp(thump * 2.2f, thump, Mathf.Clamp01(t * 12f)) / rate;
            float s = noise * Mathf.Exp(-t * snap) * 0.8f + Mathf.Sin(phase) * Mathf.Exp(-t * 28f) * 0.9f;
            data[i] = Mathf.Clamp(s * 0.9f, -1f, 1f);
        }
        var clip = AudioClip.Create(name, count, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // Short metallic tick for magazine and bolt sounds.
    static AudioClip BuildClick()
    {
        const int rate = 44100;
        int count = (int)(rate * 0.07f);
        var data = new float[count];
        var rng = new System.Random(11);
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)rate;
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            data[i] = noise * Mathf.Exp(-t * 260f) * 0.5f + Mathf.Sin(2f * Mathf.PI * 1900f * t) * Mathf.Exp(-t * 140f) * 0.5f;
        }
        var clip = AudioClip.Create("sfx_rifle_click", count, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
