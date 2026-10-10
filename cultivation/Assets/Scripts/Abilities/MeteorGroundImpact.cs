using System.Collections.Generic;
using UnityEngine;

/// <summary>焚天每颗火球落地时播放熔岩并局部破坏体素；独立于敌人范围伤害计时。</summary>
[RequireComponent(typeof(ParticleSystem))]
public sealed class MeteorGroundImpact : MonoBehaviour
{
    ParticleSystem particles;
    readonly List<ParticleCollisionEvent> events=new List<ParticleCollisionEvent>();
    public int 落地次数 {get;private set;}
    public float 缩放=1f;
    public float 破坏半径=1f;
    public void 初始化()
    {
        particles=GetComponent<ParticleSystem>();var c=particles.collision;c.sendCollisionMessages=true;
        // 源特效自带不可见的平面碰撞地板；体素地图必须命中真正的坡面和坑底。
        if(VoxelCombatDamage.Active){
            foreach(var collider in transform.root.GetComponentsInChildren<Collider>(true))collider.enabled=false;
            c.type=ParticleSystemCollisionType.World;c.mode=ParticleSystemCollisionMode.Collision3D;
            c.quality=ParticleSystemCollisionQuality.High;c.collidesWith=~0;
            c.enableDynamicColliders=false;c.colliderForce=0;
        }
    }
    void OnParticleCollision(GameObject other)
    {
        if(!particles||!other||other.GetComponentInParent<NpcInstance>()||other.GetComponentInParent<PlayerController>())return;
        int count=particles.GetCollisionEvents(other,events);
        for(int i=0;i<count;i++){
            var e=events[i];if(Vector3.Dot(e.normal,Vector3.up)<.35f)continue;
            var point=e.intersection;
            var hits=Physics.RaycastAll(point+Vector3.up*2,Vector3.down,6,~0,QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
            foreach(var hit in hits){if(hit.collider.GetComponentInParent<NpcInstance>()||hit.collider.GetComponentInParent<PlayerController>())continue;point=hit.point;break;}
            CombatImpactPipeline.接触("meteor_ground",point,破坏半径,缩放,source:this);落地次数++;
        }
    }
}
