using UnityEngine;
using UnityEngine.InputSystem;

// The weapon itself is the player character: it moves, jumps and attacks with its own moveset.
// Hierarchy: WeaponAvatar > Pose (WeaponSwinger, attack poses) > Motion (locomotion offsets) > weapon visual.
[RequireComponent(typeof(CharacterController))]
public class WeaponAvatar : MonoBehaviour
{
    [SerializeField] InputActionAsset actions;
    [SerializeField] ThirdPersonCamera cameraRig;
    [SerializeField] WeaponSwinger swinger;
    [SerializeField] Transform motion;
    [Tooltip("Number keys 1..n pick these weapons.")]
    [SerializeField] WeaponData[] slots = new WeaponData[5];
    [SerializeField] float gravity = -25f;

    CharacterController cc;
    InputAction move, jump, attack, altAttack, skill;
    AvatarMoveset moveset;
    WeaponData weapon;
    GameObject visual;

    Vector3 lungeVelocity;
    float lungeTimeLeft;

    public WeaponData Weapon => weapon;
    public AvatarMoveset Moveset => moveset;
    public ThirdPersonCamera CameraRig => cameraRig;
    public WeaponSwinger Swinger => swinger;
    public Transform Motion => motion;
    public WeaponData[] Slots => slots;
    public float Gravity => gravity;
    public bool IsGrounded => cc.isGrounded;

    // Set by the moveset every frame; gravity and the CharacterController move are applied here.
    public Vector3 PlanarVelocity { get; set; }
    public float VerticalVelocity { get; set; }

    void Awake()
    {
        cc = GetComponent<CharacterController>();
    }

    void OnEnable()
    {
        var map = actions.FindActionMap("Player", true);
        move = map.FindAction("Move", true);
        jump = map.FindAction("Jump", true);
        attack = map.FindAction("Attack", true);
        altAttack = map.FindAction("AltAttack", true);
        skill = map.FindAction("Skill", true);
        map.Enable();
    }

    void Start()
    {
        if (slots.Length > 0 && slots[0] != null) Equip(slots[0]);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        ReadWeaponKeys();
        if (moveset == null) return;

        if (skill.WasPressedThisFrame() && !moveset.Busy && weapon.upgradeTo != null) Evolve();

        Vector2 stick = move.ReadValue<Vector2>();
        Vector3 wish = Quaternion.Euler(0f, cameraRig.Yaw, 0f) * new Vector3(stick.x, 0f, stick.y);
        var input = new AvatarInput
        {
            wish = Vector3.ClampMagnitude(wish, 1f),
            jump = jump.WasPressedThisFrame(),
            attack = attack.WasPressedThisFrame(),
            attackHeld = attack.IsPressed(),
            alt = altAttack.WasPressedThisFrame(),
            altHeld = altAttack.IsPressed(),
            melee = Keyboard.current != null && Keyboard.current[Key.V].wasPressedThisFrame,
        };

        if (cc.isGrounded && VerticalVelocity < 0f) VerticalVelocity = -2f;
        moveset.Tick(input, dt);
        VerticalVelocity += gravity * dt;

        Vector3 velocity = PlanarVelocity + Vector3.up * VerticalVelocity;
        if (lungeTimeLeft > 0f)
        {
            velocity += lungeVelocity;
            lungeTimeLeft -= dt;
        }
        cc.Move(velocity * dt);
    }

    void ReadWeaponKeys()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;
        for (int i = 0; i < slots.Length && i < 9; i++)
        {
            if (!keyboard[Key.Digit1 + i].wasPressedThisFrame || slots[i] == null) continue;
            Equip(slots[i]);
            return;
        }
    }

    void Evolve()
    {
        HitFeedback.Instance.PlayUpgrade();
        Equip(weapon.upgradeTo);
        HitFeedback.Instance.PlayUpgradeFlash(visual.transform.position + Vector3.up * 0.8f);
    }

    // Moves `distance` metres along `direction` over `duration` seconds.
    public void Lunge(Vector3 direction, float distance, float duration)
    {
        if (duration <= 0f || distance <= 0f) return;
        float speed = distance / duration;
        if (lungeTimeLeft > 0f && lungeVelocity.magnitude > speed) return;
        lungeVelocity = direction.normalized * speed;
        lungeTimeLeft = duration;
    }

    // Jumps up to `height` metres (only from the ground unless `air` is set).
    public void Hop(float height, bool air = false)
    {
        if (air || cc.isGrounded) VerticalVelocity = Mathf.Sqrt(height * -2f * gravity);
    }

    public void Equip(WeaponData data)
    {
        if (moveset != null)
        {
            moveset.Teardown();
            Destroy(moveset);
        }
        if (visual != null) Destroy(visual);
        swinger.Cancel();
        lungeTimeLeft = 0f;
        motion.localPosition = Vector3.zero;
        motion.localRotation = Quaternion.identity;
        motion.localScale = Vector3.one;

        weapon = data;
        visual = Instantiate(data.prefab, motion);
        visual.transform.localPosition = data.gripLocalPosition;
        visual.transform.localRotation = Quaternion.Euler(data.gripLocalEuler);
        visual.transform.localScale = Vector3.one * data.heldScale;
        // The avatar's own CharacterController is its only collider.
        foreach (var c in visual.GetComponentsInChildren<Collider>()) Destroy(c);

        moveset = AddMoveset(data);
        moveset.Configure(this, data, visual.transform);
    }

    AvatarMoveset AddMoveset(WeaponData data)
    {
        if (data.chainsaw) return gameObject.AddComponent<ChainsawMoveset>();
        if (data.shears) return gameObject.AddComponent<ShearsMoveset>();
        if (data.rifle) return gameObject.AddComponent<RifleMoveset>();
        return gameObject.AddComponent<RulerMoveset>();
    }
}
