using UnityEditor;
using UnityEngine;

public static partial class WeaponAvatarSceneBuilder
{
    const string MachineRiflePath = "Assets/Weapons/LowPoly_MMG/LowPoly_MMG.obj";
    const float MachineRifleLengthScale = 1.15f;

    static WeaponData Rifle()
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(RiflePath);
        if (model == null) throw new System.Exception("Missing " + RiflePath);
        Bounds b = ModelBounds(model);

        var d = GameSceneBuilder.LoadOrCreate<WeaponData>(AvatarDir + "/Avatar_Rifle.asset");
        d.displayName = "M4 소총";
        d.description = "40발 소총 · R 재장전 · E 폭발탄 · Q 기관총 60초";
        d.prefab = model;
        d.heldScale = 1.6f;
        d.gripLocalPosition = Vector3.zero;
        d.gripLocalEuler = Vector3.zero;
        d.restPosition = new Vector3(0f, -b.min.y * d.heldScale, 0f);
        d.restEuler = Vector3.zero;
        d.bladeBase = new Vector3(0f, b.center.y, b.min.z * 0.4f);
        d.bladeTip = new Vector3(0f, b.center.y, b.min.z);
        d.hitRadius = 0.3f;
        d.rifle = true;
        d.rifleMuzzle = new Vector3(0f, b.center.y + b.extents.y * 0.3f, b.max.z);
        Bounds aperture = PartBounds(RiflePath, "Rear_Aperture", Quaternion.identity);
        Bounds post = PartBounds(RiflePath, "Front_Sight_Post", Quaternion.identity);
        d.rifleSightRear = aperture.center;
        d.rifleSightFront = new Vector3(post.center.x, post.max.y, post.center.z);
        ConfigureMachineRifle(d, b);
        d.fireInterval = 0.09f;
        d.fireRange = 30f;
        d.rifleJumpShots = 4;
        d.rifleJumpHeight = 2.4f;
        d.rifleStandPivot = Vector3.zero;
        d.rifleStandPitch = -78f;
        d.rifleAimAssist = true;
        d.rifleHeavyHit = false;
        d.damage = 7;
        d.knockback = 3f;
        d.maxHits = 0;
        d.comboResetTime = 0.9f;
        d.swingMoveScale = 0.4f;
        d.hitStop = 0.05f;
        d.shake = 0.15f;
        d.fovPunch = 1.2f;
        d.trailColor = new Color(1f, 0.9f, 0.6f, 0.7f);
        d.upgradeTo = null;
        AddStockCombo(d);
        return d;
    }

    static void ConfigureMachineRifle(WeaponData d, Bounds rifleBounds)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(MachineRiflePath);
        if (model == null) throw new System.Exception("Missing " + MachineRiflePath);
        Bounds bounds = ModelBounds(model);
        Bounds muzzle = PartBounds(MachineRiflePath, "12_Muzzle_Tip", Quaternion.identity);
        Bounds grip = PartBounds(MachineRiflePath, "21_Pistol_Grip", Quaternion.identity);
        Bounds rear = PartBounds(MachineRiflePath, "16_Rear_Sight_Base", Quaternion.identity);
        Bounds front = PartBounds(MachineRiflePath, "17_Front_Sight_Post", Quaternion.identity);
        Bounds stock = PartBounds(MachineRiflePath, "20_Stock_Buttpad", Quaternion.identity);
        d.rifleMachinePrefab = model;
        d.rifleMachineScale = rifleBounds.size.z * d.heldScale * MachineRifleLengthScale / bounds.size.z;
        // Keep the trigger hand at the M4's origin; scale belongs to the model, not Motion.
        d.rifleMachineGrip = -grip.center * d.rifleMachineScale;
        d.rifleMachineMuzzle = new Vector3(muzzle.center.x, muzzle.center.y, muzzle.max.z);
        d.rifleMachineSightRear = new Vector3(rear.center.x, rear.max.y, rear.center.z);
        d.rifleMachineSightFront = new Vector3(front.center.x, front.max.y, front.center.z);
        d.rifleMachineStandPivot = new Vector3(stock.center.x, stock.center.y, stock.min.z);
    }

    static void AddStockCombo(WeaponData d)
    {
        d.combo.Clear();
        d.altCombo.Clear();
        d.altCombo.Add(GameSceneBuilder.Swing(0.08f, 0.10f, 0.20f,
            new Vector3(0f, 0.9f, 0f), new Vector3(0f, 90f, 0f),
            new Vector3(0f, 0.9f, 0f), new Vector3(0f, 270f, 0f), lunge: 0.8f, knockback: 1.6f));
        d.altCombo.Add(GameSceneBuilder.Swing(0.12f, 0.09f, 0.26f,
            new Vector3(0f, 0.9f, 0f), new Vector3(60f, 180f, 0f),
            new Vector3(0f, 0.6f, 0f), new Vector3(-40f, 180f, 0f),
            lunge: 1f, stagger: 0.55f, damage: 1.6f, knockback: 2f, hitStop: 1.4f, shake: 1.5f, heavy: true, chainAt: 0.7f));
    }
}
