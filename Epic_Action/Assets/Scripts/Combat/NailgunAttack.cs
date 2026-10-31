using System;
using UnityEngine;

[DefaultExecutionOrder(100)]
public class NailgunAttack : MonoBehaviour
{
    enum Phase { Idle, Approaching, Attached, Charging, RemoteCharging, RemoteRecoil, Detaching }
    WeaponData data;
    PlayerController player;
    ThirdPersonCamera cameraRig;
    Transform gun, exposedNail;
    Enemy target;
    Phase phase;
    Action consumeWeapon;
    Vector3 direction, muzzleLocal, nailRest, returnPosition;
    Quaternion modelRotation, returnRotation;
    float forwardSign;
    float phaseStarted, lastShot = -10f, nextShot, shotRecoil, shotKick;
    int queuedShots;
    bool spent, hideNail;
    AudioSource chargeAudio;
    LineRenderer chargeRing;
    Light chargeLight;
    Material chargeMaterial;
    public bool Busy => phase != Phase.Idle;
    bool Charging => phase == Phase.Charging || phase == Phase.RemoteCharging;
    float ChargeProgress => Mathf.Clamp01((Time.unscaledTime-phaseStarted)/Mathf.Max(0.01f,data.nailChargeTime));
    public string Status => Charging ? $"관통 못 충전 {ChargeProgress:P0}" + (phase == Phase.RemoteCharging ? " · 발사 후 총 파괴" : "")
        : phase == Phase.Attached ? $"머리 밀착 사격 · {target?.NailHits ?? 0}/3발 · 추가 클릭으로 연사" : "";

    public void Configure(WeaponData weapon, PlayerController controller, ThirdPersonCamera camera, Transform visual, Action consumed)
    {
        data=weapon; player=controller; cameraRig=camera; gun=visual; consumeWeapon=consumed;
        muzzleLocal=NailgunGeometry.MeasureMuzzle(gun,data.nailMuzzle,out float sign);
        forwardSign=sign;
        modelRotation=Quaternion.Euler(0f,sign<0f?180f:0f,0f);
        exposedNail=gun.Find("NailgunBlue_Evolved_Nail");
        if(exposedNail!=null)nailRest=exposedNail.localPosition;
        CreateChargeEffect();
    }

    public void Tick(bool pressed, bool alternate, bool allowed)
    {
        if(data==null||gun==null)return;
        float now=Time.unscaledTime;
        if(alternate&&allowed&&data.nailPiercing&&!spent&&phase!=Phase.RemoteCharging&&phase!=Phase.RemoteRecoil)
        {
            Cancel();
            phase=Phase.RemoteCharging;phaseStarted=now;
            player.SpeedScale=0.4f;
            direction=cameraRig.transform.forward;
            StartCharge();
            return;
        }
        if(phase==Phase.Idle){if(pressed&&allowed)Begin();return;}
        if(phase==Phase.RemoteCharging)
        {
            player.FaceYaw(cameraRig.Yaw);
            direction=RemoteAimDirection();
            UpdateCharge();
            if(ChargeProgress>=1f)
            {
                PoseRemote();
                Vector3 muzzle=gun.TransformPoint(muzzleLocal);
                StopCharge();
                NailProjectile.Fire(data,player.transform,muzzle,direction,null);
                FireFeedback(muzzle,true);
                spent=true;hideNail=true;
                if(exposedNail!=null)exposedNail.gameObject.SetActive(false);
                phase=Phase.RemoteRecoil;phaseStarted=now;
            }
            return;
        }
        if(phase==Phase.RemoteRecoil)
        {
            if(now-phaseStarted>=0.2f)
            {
                Action consumed=consumeWeapon;consumeWeapon=null;
                consumed?.Invoke();
            }
            return;
        }
        if(phase==Phase.Detaching)
        {
            if(now-phaseStarted>=0.22f){RestoreGun();phase=Phase.Idle;}
            return;
        }
        if(target==null||target.IsDead){Detach();return;}
        player.FaceYaw(Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg);
        if(pressed&&!data.nailPiercing)queuedShots=Mathf.Min(queuedShots+1,3);
        if(phase==Phase.Approaching)
        {
            // Keep both the player capsule and the complete gun outside the enemy.
            float standOff=Mathf.Max(1.08f,Mathf.Abs(muzzleLocal.z*gun.lossyScale.z)+target.Radius+0.2f);
            Vector3 destination=target.transform.position-direction*standOff;
            Vector3 delta=destination-player.transform.position;delta.y=0f;
            if(delta.magnitude<=0.09f){phase=Phase.Attached;FireContact();}
            else
            {
                player.MoveCombat(Vector3.ClampMagnitude(delta,18f*Time.unscaledDeltaTime));
                if(now-phaseStarted>0.8f)Detach();
            }
            return;
        }
        if(phase==Phase.Charging)
        {
            UpdateCharge();
            if(ChargeProgress>=1f)
            {
                Vector3 point=ContactPoint(false);
                PoseContact(point);
                StopCharge();
                NailProjectile.Fire(data,player.transform,point,direction,target);
                FireFeedback(point,true);
                hideNail=true;
                if(exposedNail!=null)exposedNail.gameObject.SetActive(false);
                gun.position-=direction*shotRecoil;
                Detach();
            }
            return;
        }
        if(queuedShots>0&&now>=nextShot)FireContact();
        else if(now-lastShot>=data.nailDetachDelay)Detach();
    }

    void Begin()
    {
        Enemy best=null;float bestDistance=data.nailAttachRange;
        Vector3 forward=Quaternion.Euler(0f,cameraRig.Yaw,0f)*Vector3.forward;
        foreach(var enemy in FindObjectsByType<Enemy>(FindObjectsSortMode.None))
        {
            if(enemy.IsDead)continue;
            Vector3 delta=enemy.transform.position-player.transform.position;delta.y=0f;
            if(delta.magnitude>bestDistance||Vector3.Angle(forward,delta)>45f||Blocked(enemy))continue;
            best=enemy;bestDistance=delta.magnitude;
        }
        if(best==null||!best.TryPinForNail(transform))return;
        target=best;direction=target.transform.position-player.transform.position;direction.y=0f;
        direction=direction.sqrMagnitude>0.001f?direction.normalized:forward;
        queuedShots=1;hideNail=false;
        phase=Phase.Approaching;phaseStarted=Time.unscaledTime;player.CombatLocked=true;
    }

    bool Blocked(Enemy enemy)
    {
        Vector3 from=player.transform.position+Vector3.up;
        Vector3 delta=enemy.NailAimPoint(false)-from;
        foreach(var hit in Physics.RaycastAll(from,delta.normalized,delta.magnitude,~0,QueryTriggerInteraction.Ignore))
        {
            if(hit.transform.IsChildOf(player.transform)||hit.collider.GetComponentInParent<Enemy>()!=null)continue;
            return true;
        }
        return false;
    }

    Vector3 ContactPoint(bool head)=>target.NailSurfacePoint(head,direction);

    Vector3 RemoteAimDirection()
    {
        Camera camera=cameraRig.GetComponent<Camera>();
        Ray ray=camera.ViewportPointToRay(new Vector3(0.5f,0.5f));
        Vector3 aim=ray.GetPoint(data.nailProjectileRange+4f);
        float closest=float.MaxValue;
        foreach(var hit in Physics.RaycastAll(ray,data.nailProjectileRange+4f,~0,QueryTriggerInteraction.Ignore))
        {
            if(hit.transform.IsChildOf(player.transform)||hit.distance>=closest)continue;
            closest=hit.distance;aim=hit.point;
        }
        Vector3 muzzle=gun.TransformPoint(muzzleLocal);
        Vector3 delta=aim-muzzle;
        return Vector3.Dot(delta,ray.direction)>0.1f?delta.normalized:ray.direction;
    }

    void FireContact()
    {
        queuedShots=Mathf.Max(0,queuedShots-1);
        Vector3 point=ContactPoint(!data.nailPiercing);
        PoseContact(point);
        target.TakeContactNail(point,direction,data.nailPiercing);
        FireFeedback(point,false);
        gun.position-=direction*shotRecoil;
        nextShot=lastShot+0.12f;
        if(data.nailPiercing){phase=Phase.Charging;phaseStarted=lastShot;StartCharge();}
        else if(target.IsDead)Detach();
    }

    void FireFeedback(Vector3 point,bool piercing)
    {
        lastShot=Time.unscaledTime;
        shotRecoil=piercing?0.22f:0.095f;
        shotKick=piercing?13f:6f;
        HitFeedback.Instance?.PlayNailShot(point,direction,piercing);
    }

    void PoseContact(Vector3 point)
    {
        float recoil=Mathf.Exp(-(Time.unscaledTime-lastShot)*23f);
        Quaternion rotation=Quaternion.LookRotation(direction)*Quaternion.Euler(-shotKick*recoil,0f,0f)*modelRotation;
        gun.rotation=rotation;
        gun.position=point-gun.TransformVector(muzzleLocal)-direction*(shotRecoil*recoil);
        if(Charging)gun.position+=gun.right*(Mathf.Sin(Time.unscaledTime*115f)*0.012f*ChargeProgress);
    }

    void PoseRemote()
    {
        float recoil=Mathf.Exp(-(Time.unscaledTime-lastShot)*20f);
        gun.rotation=Quaternion.LookRotation(direction)*Quaternion.Euler(-shotKick*recoil,0f,0f)*modelRotation;
        gun.position=gun.parent.TransformPoint(data.gripLocalPosition)+Vector3.up*0.08f-direction*(shotRecoil*recoil);
        if(Charging)gun.position+=gun.right*(Mathf.Sin(Time.unscaledTime*115f)*0.012f*ChargeProgress);
    }

    void LateUpdate()
    {
        if(gun==null)return;
        if(phase==Phase.RemoteCharging||phase==Phase.RemoteRecoil)PoseRemote();
        else if(target!=null&&(phase==Phase.Attached||phase==Phase.Charging))PoseContact(ContactPoint(!data.nailPiercing));
        else if(phase==Phase.Detaching)
        {
            float u=Mathf.SmoothStep(0f,1f,(Time.unscaledTime-phaseStarted)/0.22f);
            gun.localPosition=Vector3.Lerp(returnPosition,data.gripLocalPosition,u);
            gun.localRotation=Quaternion.Slerp(returnRotation,Quaternion.Euler(data.gripLocalEuler),u);
        }
        if(Charging&&exposedNail!=null)exposedNail.localPosition=nailRest+Vector3.back*(forwardSign*0.12f*ChargeProgress);
    }

    void CreateChargeEffect()
    {
        chargeAudio=gun.gameObject.AddComponent<AudioSource>();
        chargeAudio.playOnAwake=false;chargeAudio.loop=true;chargeAudio.spatialBlend=0f;
        chargeAudio.clip=ProceduralSfx.NailCharge;
        var effect=new GameObject("NailChargeRing");effect.transform.SetParent(gun,false);effect.transform.localPosition=muzzleLocal;
        chargeRing=effect.AddComponent<LineRenderer>();
        chargeMaterial=new Material(Shader.Find("Sprites/Default"));chargeRing.sharedMaterial=chargeMaterial;
        chargeRing.useWorldSpace=false;chargeRing.loop=true;chargeRing.positionCount=48;chargeRing.enabled=false;
        chargeLight=effect.AddComponent<Light>();chargeLight.type=LightType.Point;chargeLight.color=new Color(1f,0.55f,0.15f);chargeLight.enabled=false;
    }
    void StartCharge(){if(chargeAudio!=null){chargeAudio.volume=0.08f;chargeAudio.pitch=0.65f;chargeAudio.Play();}}
    void UpdateCharge()
    {
        float u=ChargeProgress;
        chargeAudio.volume=Mathf.Lerp(0.08f,0.5f,u);chargeAudio.pitch=Mathf.Lerp(0.65f,1.8f,u);
        chargeRing.enabled=true;chargeRing.widthMultiplier=Mathf.Lerp(0.008f,0.026f,u);
        Color color=Color.Lerp(new Color(1f,0.4f,0.08f),new Color(1f,0.95f,0.7f),u);
        chargeRing.startColor=chargeRing.endColor=color;
        float radius=Mathf.Lerp(0.2f,0.055f,u)*(1f+Mathf.Sin(Time.unscaledTime*45f)*0.1f);
        for(int i=0;i<48;i++)
        {
            float angle=i*Mathf.PI*2f/48f+Time.unscaledTime*12f;
            chargeRing.SetPosition(i,new Vector3(Mathf.Cos(angle)*radius,Mathf.Sin(angle)*radius,0f));
        }
        chargeLight.enabled=true;chargeLight.range=0.5f+u;chargeLight.intensity=1f+u*5f;
        cameraRig.FovPunch(u*1.4f,0.06f);
        cameraRig.Shake(u*0.035f,0.05f);
    }
    void StopCharge()
    {
        if(chargeAudio!=null)chargeAudio.Stop();
        if(chargeRing!=null)chargeRing.enabled=false;
        if(chargeLight!=null)chargeLight.enabled=false;
    }
    void RestoreGun()
    {
        if(gun!=null){gun.localPosition=data.gripLocalPosition;gun.localRotation=Quaternion.Euler(data.gripLocalEuler);}
        if(exposedNail!=null){exposedNail.localPosition=nailRest;exposedNail.gameObject.SetActive(true);}
        hideNail=false;
    }
    void Detach()
    {
        StopCharge();
        returnPosition=gun.localPosition;returnRotation=gun.localRotation;
        if(target!=null)target.ReleaseNailPin(transform);
        target=null;queuedShots=0;player.CombatLocked=false;player.ReleaseFacing();
        if(hideNail&&exposedNail!=null)exposedNail.gameObject.SetActive(false);
        player.Lunge(-direction,0.7f,0.18f);
        phase=Phase.Detaching;phaseStarted=Time.unscaledTime;
    }
    public void Cancel()
    {
        StopCharge();
        if(target!=null)target.ReleaseNailPin(transform);
        target=null;
        if(player!=null){player.CombatLocked=false;player.SpeedScale=data!=null?data.holdMoveScale:1f;player.ReleaseFacing();}
        if(data!=null)RestoreGun();
        phase=Phase.Idle;queuedShots=0;
    }
    void OnDisable()=>Cancel();
    void OnDestroy(){if(chargeMaterial!=null)Destroy(chargeMaterial);}
}
