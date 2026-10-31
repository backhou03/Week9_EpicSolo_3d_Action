using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class Enemy : MonoBehaviour
{
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    // A hit keeps the blade counted as touching for this long (continuous-cut ticks come every 0.05 s).
    const float SawContactHold = 0.12f;
    // Seconds of sawing lost per second once the blade is out of the enemy.
    const float SawDecay = 0.5f;

    [SerializeField] int maxHp = 30;
    [SerializeField] float moveSpeed = 3.2f;
    [SerializeField] float stopDistance = 1.4f;
    [SerializeField] float turnSpeed = 540f;
    [SerializeField] float gravity = -25f;
    [SerializeField] float knockbackDrag = 7f;
    [SerializeField] float respawnDelay = 3f;
    [SerializeField] Transform visual;
    [SerializeField] Renderer bodyRenderer;
    [SerializeField] Material cutMaterial;
    [SerializeField] Material bloodMaterial;

    CharacterController cc;
    Transform target;
    Vector3 spawnPosition;
    Quaternion spawnRotation;
    Vector3 visualBaseScale;
    Vector3 visualBasePosition;
    Color baseColor;
    MaterialPropertyBlock block;
    HitInfo sawInfo;
    Vector3 sawOffset;
    float sawTime;
    float sawContact;

    Transform nailPinOwner;
    readonly List<GameObject> embeddedNails = new List<GameObject>();
    Mesh originalBodyMesh, perforatedBodyMesh;
    Material[] originalBodyMaterials;
    public int NailHits { get; private set; }

    int hp;
    float staggerTimer;
    float flashTimer;
    float squash;
    Vector3 hitDirection = Vector3.forward;
    Vector3 knockbackVelocity;
    float verticalVelocity;

    public bool IsDead { get; private set; }
    public Vector3 Center => cc.bounds.center;
    public float Radius => cc.radius;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        spawnPosition = transform.position;
        spawnRotation = transform.rotation;
        visualBaseScale = visual.localScale;
        visualBasePosition = visual.localPosition;
        baseColor = bodyRenderer.sharedMaterial.GetColor(BaseColorId);
        block = new MaterialPropertyBlock();
        hp = maxHp;
        originalBodyMesh = visual.GetComponent<MeshFilter>().sharedMesh;
        originalBodyMaterials = bodyRenderer.sharedMaterials;
    }

    void Start()
    {
        var player = FindFirstObjectByType<PlayerController>();
        if (player != null) target = player.transform;
        else
        {
            var avatar = FindFirstObjectByType<WeaponAvatar>();
            if (avatar != null) target = avatar.transform;
        }
    }

    public Vector3 NailAimPoint(bool head) => transform.position + Vector3.up * (head ? 1.72f : 1.05f);

    public Vector3 NailSurfacePoint(bool head, Vector3 direction)
    {
        Vector3 aim = NailAimPoint(head);
        Vector3 origin = visual.InverseTransformPoint(aim - direction * 3f);
        Vector3 localDirection = visual.InverseTransformVector(direction).normalized;
        if (NailgunGeometry.CapsuleSurface(originalBodyMesh.bounds, origin, localDirection, out Vector3 localPoint))
            return visual.TransformPoint(localPoint) - direction * 0.035f;
        return aim - direction * (Radius + 0.035f);
    }

    public bool TryPinForNail(Transform owner)
    {
        if (IsDead || (nailPinOwner != null && nailPinOwner != owner)) return false;
        nailPinOwner = owner;
        knockbackVelocity = Vector3.zero;
        verticalVelocity = 0f;
        return true;
    }

    public void ReleaseNailPin(Transform owner)
    {
        if (nailPinOwner == owner) nailPinOwner = null;
    }

    public void TakeContactNail(Vector3 point, Vector3 direction, bool primer)
    {
        if (IsDead) return;
        if (!primer) NailHits++;
        TakeHit(new HitInfo
        {
            damage = primer ? 0 : Mathf.CeilToInt(maxHp / 3f),
            direction = direction, point = point, stagger = 0.55f, tick = true,
        });
        if (primer) return;
        var nail = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        nail.name = "EmbeddedHeadNail";
        Destroy(nail.GetComponent<Collider>());
        nail.transform.position = point + direction * 0.04f;
        nail.transform.rotation = Quaternion.FromToRotation(Vector3.up, direction);
        nail.transform.localScale = new Vector3(0.025f, 0.1f, 0.025f);
        nail.GetComponent<Renderer>().sharedMaterial = cutMaterial;
        nail.transform.SetParent(visual, true);
        embeddedNails.Add(nail);
    }

    public void TakePiercingNail(Vector3 point, Vector3 direction, float radius)
    {
        if (IsDead) return;
        visual.localScale = visualBaseScale;
        visual.localPosition = visualBasePosition;
        visual.localRotation = Quaternion.identity;
        Vector3 localPoint = visual.InverseTransformPoint(point);
        Vector3 localDirection = visual.InverseTransformDirection(direction).normalized;
        perforatedBodyMesh = NailHoleMesh.Create(originalBodyMesh.bounds, localPoint, localDirection, radius);
        visual.GetComponent<MeshFilter>().sharedMesh = perforatedBodyMesh;
        bodyRenderer.sharedMaterials = new[] { originalBodyMaterials[0], bloodMaterial != null ? bloodMaterial : cutMaterial };
        hitDirection = direction;
        hp = 0;
        ResetSaw();
        StartCoroutine(DieRoutine());
    }

    void ClearNailDamage()
    {
        NailHits = 0;
        nailPinOwner = null;
        foreach (var nail in embeddedNails) if (nail != null) Destroy(nail);
        embeddedNails.Clear();
        if (perforatedBodyMesh != null)
        {
            visual.GetComponent<MeshFilter>().sharedMesh = originalBodyMesh;
            bodyRenderer.sharedMaterials = originalBodyMaterials;
            Destroy(perforatedBodyMesh);
            perforatedBodyMesh = null;
        }
    }

    void OnDestroy()
    {
        if (perforatedBodyMesh != null) Destroy(perforatedBodyMesh);
    }

    public void TakeHit(HitInfo info)
    {
        if (IsDead) return;
        if (info.crush)
        {
            StartCoroutine(SonicBurstRoutine(info.direction));
            return;
        }

        hp -= info.damage;
        hitDirection = info.direction;
        staggerTimer = info.stagger;
        flashTimer = 0.1f;

        bool killed = hp <= 0;
        bool sawing = info.slice && info.sliceDelay > 0f;
        float knock = info.knockback * (killed ? 1.5f : 1f);

        // Ticks of a continuous cut keep the enemy shaking and slowly shoved along the blade instead of
        // re-launching it every 0.05 s.
        if (info.tick)
        {
            squash = Mathf.Max(squash, 0.45f);
            // A saw keeps the enemy pinned on the blade instead of shoving it off.
            if (!sawing) knockbackVelocity += info.direction * knock;
        }
        else
        {
            squash = info.heavy ? 1.4f : 1f;
            knockbackVelocity = info.direction * knock;
            verticalVelocity = killed ? 6f : (info.heavy ? 4f : 2.5f);
        }

        bool cutNow = info.slice && info.sliceNow;
        if (!killed && !cutNow)
        {
            if (sawing) Saw(info);
            return;
        }

        ResetSaw();
        if (info.slice && Slice(info)) StartCoroutine(RespawnAfterSlice());
        else StartCoroutine(DieRoutine());
    }

    // Sawing: every hit that keeps the blade in the enemy refreshes the contact; the time in contact adds up to
    // the cut time and the enemy splits along the plane of the first cut (kept relative to its body, so
    // knockback in between does not move the cut).
    void Saw(HitInfo info)
    {
        if (sawTime <= 0f && sawContact <= 0f)
        {
            sawInfo = info;
            sawOffset = info.cutPoint - Center;
        }
        sawContact = SawContactHold;
    }

    void UpdateSaw(float dt)
    {
        if (IsDead || sawInfo.sliceDelay <= 0f) return;

        if (sawContact > 0f)
        {
            sawContact -= dt;
            sawTime += dt;
            staggerTimer = Mathf.Max(staggerTimer, 0.1f);
            squash = Mathf.Max(squash, 0.3f);
        }
        else
        {
            sawTime = Mathf.Max(0f, sawTime - SawDecay * dt);
            if (sawTime <= 0f)
            {
                sawInfo = default;
                return;
            }
        }

        if (sawTime < sawInfo.sliceDelay) return;

        HitInfo info = sawInfo;
        Vector3 centre = Center;
        info.cutPoint = centre + sawOffset;
        ResetSaw();
        if (Slice(info))
        {
            if (HitFeedback.Instance != null) HitFeedback.Instance.PlaySlice(info.cutPoint, info.direction, info.bloody);
            StartCoroutine(RespawnAfterSlice());
            BurstSlice(info, centre);
        }
        else
        {
            StartCoroutine(DieRoutine());
        }
    }

    // Every other enemy within `info.sliceRadius` of `centre` is cut at once, along the plane of the first cut.
    void BurstSlice(HitInfo info, Vector3 centre)
    {
        if (info.sliceRadius <= 0f) return;

        foreach (var other in FindObjectsByType<Enemy>(FindObjectsSortMode.None))
        {
            if (other == this || other.IsDead) continue;
            Vector3 to = other.Center - centre;
            if (to.sqrMagnitude > info.sliceRadius * info.sliceRadius) continue;

            var burst = info;
            burst.damage = 1;
            burst.tick = false;
            burst.sliceNow = true;
            burst.sliceDelay = 0f;
            burst.cutPoint = other.Center;
            to.y = 0f;
            burst.direction = to.sqrMagnitude > 0.01f ? to.normalized : info.direction;
            other.TakeHit(burst);
            if (other.IsDead && HitFeedback.Instance != null) HitFeedback.Instance.PlaySlice(other.Center, burst.direction, burst.bloody);
        }
    }

    void ResetSaw()
    {
        sawInfo = default;
        sawTime = 0f;
        sawContact = 0f;
    }

    // A point on the blade plane, pulled toward the body axis (but keeping the cut height) so a vertical cut
    // cannot shave a sliver.
    Vector3 CutPlanePoint(HitInfo info, Vector3 n)
    {
        float halfExtent = Mathf.Abs(n.y) * cc.height * 0.5f + Mathf.Sqrt(Mathf.Max(0f, 1f - n.y * n.y)) * cc.radius;
        float offset = Mathf.Clamp(Vector3.Dot(info.cutPoint - Center, n), -0.5f * halfExtent, 0.5f * halfExtent);
        return Center + n * offset;
    }

    // Splits the capsule along the blade plane into two physics pieces. Returns false if the plane misses.
    bool Slice(HitInfo info)
    {
        visual.localScale = visualBaseScale;
        visual.localPosition = visualBasePosition;
        visual.rotation = transform.rotation;

        Vector3 n = info.cutNormal.normalized;
        Vector3 worldPoint = CutPlanePoint(info, n);

        var worldPlane = new Plane(n, worldPoint);
        var localPlane = new Plane(visual.InverseTransformDirection(n).normalized, visual.InverseTransformPoint(worldPoint));
        var sourceMesh = visual.GetComponent<MeshFilter>().sharedMesh;
        if (!MeshSlicer.Slice(sourceMesh, localPlane, out Mesh above, out Mesh below)) return false;

        IsDead = true;
        SpawnPiece(above, n, info, worldPlane);
        SpawnPiece(below, -n, info, worldPlane);

        visual.gameObject.SetActive(false);
        cc.enabled = false;
        return true;
    }

    void SpawnPiece(Mesh mesh, Vector3 side, HitInfo info, Plane worldPlane)
    {
        var go = new GameObject("EnemyPiece");
        go.transform.SetPositionAndRotation(visual.position, visual.rotation);
        go.transform.localScale = visual.lossyScale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        Material face = info.bloody && bloodMaterial != null ? bloodMaterial : cutMaterial;
        renderer.sharedMaterials = new[] { bodyRenderer.sharedMaterial, face != null ? face : bodyRenderer.sharedMaterial };

        // A box is enough for a piece that only has to tumble to the ground; a convex hull of the cut mesh
        // exceeds the 256-polygon limit.
        var col = go.AddComponent<BoxCollider>();
        col.center = mesh.bounds.center;
        col.size = mesh.bounds.size;
        foreach (var other in FindObjectsByType<CharacterController>(FindObjectsSortMode.None))
            Physics.IgnoreCollision(col, other);

        var body = go.AddComponent<Rigidbody>();
        body.mass = 1f;
        body.linearVelocity = side * 2.5f + info.direction * (info.knockback * 0.6f) + Vector3.up * 2.5f;
        body.angularVelocity = Random.insideUnitSphere * 6f;

        // The visor is a child of the (now hidden) body; give the piece on its side a copy.
        var visor = visual.Find("Visor");
        if (visor != null && Mathf.Sign(worldPlane.GetDistanceToPoint(visor.position)) == Mathf.Sign(Vector3.Dot(side, worldPlane.normal)))
        {
            var copy = Instantiate(visor.gameObject, go.transform);
            copy.transform.SetPositionAndRotation(visor.position, visor.rotation);
            copy.transform.localScale = visor.lossyScale;
        }

        Destroy(go, 2.5f);
    }

    IEnumerator SonicBurstRoutine(Vector3 direction)
    {
        IsDead = true;
        ResetSaw();
        Vector3 centre = Center;
        cc.enabled = false;
        Vector3 fromScale = visual.localScale;
        Vector3 fromPosition = visual.localPosition;
        const float delay = 0.4f;
        // The target is visibly overloaded until its own 0.4-second fuse expires.
        float elapsed = 0f;
        while (elapsed < delay)
        {
            float u = elapsed / delay;
            visual.localScale = fromScale * (1f + 0.3f * u * u);
            visual.localPosition = fromPosition + Vector3.right * (Mathf.Sin(elapsed * 95f) * 0.025f * u);
            SetColor(Color.Lerp(baseColor, new Color(1f, 0.65f, 0.12f), u));
            yield return null;
            elapsed += Time.unscaledDeltaTime;
        }
        HitFeedback.Instance?.PlaySonicExplosion(centre, direction);
        SpawnBurstDebris(centre, direction);
        visual.gameObject.SetActive(false);
        yield return new WaitForSeconds(respawnDelay);
        Respawn();
    }

    void SpawnBurstDebris(Vector3 centre, Vector3 direction)
    {
        var characters = FindObjectsByType<CharacterController>(FindObjectsSortMode.None);
        for (int i = 0; i < 12; i++)
        {
            var piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            piece.name = "SonicBurst_Debris";
            Vector3 radial = Random.onUnitSphere;
            piece.transform.position = centre + radial * 0.3f;
            piece.transform.rotation = Random.rotation;
            piece.transform.localScale = new Vector3(Random.Range(0.15f, 0.3f), Random.Range(0.15f, 0.4f), Random.Range(0.15f, 0.3f));
            piece.GetComponent<Renderer>().sharedMaterial = bodyRenderer.sharedMaterial;
            var collider = piece.GetComponent<Collider>();
            foreach (var character in characters) Physics.IgnoreCollision(collider, character);
            var body = piece.AddComponent<Rigidbody>();
            body.mass = 0.2f;
            body.linearVelocity = radial * 6f + direction * 3f + Vector3.up * 3f;
            body.angularVelocity = Random.insideUnitSphere * 12f;
            Destroy(piece, 2f);
        }
    }

    IEnumerator RespawnAfterSlice()
    {
        yield return new WaitForSeconds(respawnDelay);
        Respawn();
    }

    void Update()
    {
        float dt = Time.deltaTime;
        UpdateSaw(dt);
        MoveBody(dt);
        if (!IsDead) AnimateVisual(dt);
    }

    void MoveBody(float dt)
    {
        if (!cc.enabled || nailPinOwner != null) return;

        Vector3 chase = Vector3.zero;
        staggerTimer -= dt;
        if (!IsDead && staggerTimer <= 0f && target != null)
        {
            Vector3 to = target.position - transform.position;
            to.y = 0f;
            if (to.magnitude > stopDistance) chase = to.normalized * moveSpeed;
            if (to.sqrMagnitude > 0.01f)
            {
                var face = Quaternion.LookRotation(to);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, face, turnSpeed * dt);
            }
        }

        knockbackVelocity *= Mathf.Exp(-knockbackDrag * dt);
        if (cc.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
        verticalVelocity += gravity * dt;

        cc.Move((chase + knockbackVelocity + Vector3.up * verticalVelocity) * dt);
    }

    void AnimateVisual(float dt)
    {
        if (IsDead) return;

        // Squash/stretch + lean away from the hit, all driven by one decaying value.
        squash = Mathf.Lerp(squash, 0f, 1f - Mathf.Exp(-11f * dt));
        visual.localScale = new Vector3(
            visualBaseScale.x * (1f + squash * 0.22f),
            visualBaseScale.y * (1f - squash * 0.28f),
            visualBaseScale.z * (1f + squash * 0.22f));

        Vector3 axis = Vector3.Cross(Vector3.up, hitDirection);
        visual.rotation = Quaternion.AngleAxis(squash * 22f, axis) * transform.rotation;
        visual.localPosition = visualBasePosition + (staggerTimer > 0f ? Random.insideUnitSphere * (0.05f * Mathf.Min(squash, 1f)) : Vector3.zero);

        if (flashTimer > 0f)
        {
            flashTimer -= dt;
            SetColor(Color.white);
        }
        else
        {
            SetColor(baseColor);
        }
    }

    void SetColor(Color c)
    {
        bodyRenderer.GetPropertyBlock(block);
        block.SetColor(BaseColorId, c);
        bodyRenderer.SetPropertyBlock(block);
    }

    IEnumerator DieRoutine()
    {
        IsDead = true;
        SetColor(Color.white);
        visual.localScale = visualBaseScale;
        visual.localPosition = visualBasePosition;

        // Fall over away from the hit.
        Vector3 axis = Vector3.Cross(Vector3.up, hitDirection);
        Quaternion start = Quaternion.AngleAxis(20f, axis) * transform.rotation;
        Quaternion end = Quaternion.AngleAxis(90f, axis) * transform.rotation;
        const float fall = 0.3f;
        for (float t = 0f; t < fall; t += Time.deltaTime)
        {
            float u = t / fall;
            visual.rotation = Quaternion.Slerp(start, end, u * u);
            if (t > 0.05f) SetColor(baseColor);
            yield return null;
        }
        visual.rotation = end;

        yield return new WaitForSeconds(0.9f);
        cc.enabled = false;

        const float shrink = 0.25f;
        for (float t = 0f; t < shrink; t += Time.deltaTime)
        {
            visual.localScale = visualBaseScale * (1f - t / shrink);
            yield return null;
        }
        visual.gameObject.SetActive(false);

        yield return new WaitForSeconds(respawnDelay);
        Respawn();
    }

    void Respawn()
    {
        ClearNailDamage();
        transform.SetPositionAndRotation(spawnPosition, spawnRotation);
        hp = maxHp;
        staggerTimer = 0f;
        knockbackVelocity = Vector3.zero;
        verticalVelocity = 0f;
        squash = 0f;
        ResetSaw();
        visual.localScale = visualBaseScale;
        visual.localPosition = visualBasePosition;
        visual.rotation = transform.rotation;
        visual.gameObject.SetActive(true);
        cc.enabled = true;
        IsDead = false;
    }
}
