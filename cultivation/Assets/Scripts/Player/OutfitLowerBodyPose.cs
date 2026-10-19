using UnityEngine;

/// <summary>闭襟专用下肢收束，保留动作资源、骨长、上身旋转与角色位移。</summary>
public sealed class OutfitLowerBodyPose
{
    static readonly int[] LegBones={3,4,5,12,13,15};
    readonly AppearanceClothProfile profile;
    readonly Transform[] bones;
    readonly Matrix4x4 hipBind;
    readonly Vector3 hipRest;
    readonly Vector3[] footRest=new Vector3[2];
    readonly Quaternion[] footRestRotation=new Quaternion[2];
    readonly Quaternion[] original=new Quaternion[6],applied=new Quaternion[6];
    readonly Vector3[] lastRaw=new Vector3[2],plant=new Vector3[2];
    readonly Vector3[] goals=new Vector3[2];
    readonly bool[] planted=new bool[2];
    readonly float[] plantBlend=new float[2];
    bool changed,hadRaw;
    float blend,hipLift;
    Vector3 hipOriginal,hipApplied;
    public float 原腿距 { get; private set; }
    public float 当前腿距 { get; private set; }
    public float 骨长误差 { get; private set; }

    public OutfitLowerBodyPose(AppearanceClothProfile profile,Transform[] bones)
    {
        this.profile=profile;this.bones=bones;hipBind=profile.蒙皮.bindposes[1];hipRest=hipBind.inverse.GetColumn(3);
        footRest[0]=profile.蒙皮.bindposes[5].inverse.GetColumn(3);footRest[1]=profile.蒙皮.bindposes[15].inverse.GetColumn(3);
        footRestRotation[0]=profile.蒙皮.bindposes[5].inverse.rotation;footRestRotation[1]=profile.蒙皮.bindposes[15].inverse.rotation;
    }

    void Restore()
    {
        if(!changed)return;
        if((bones[1].localPosition-hipApplied).sqrMagnitude<1e-12f)bones[1].localPosition=hipOriginal;
        // Animator已写入的新姿势不覆盖；手动预览/暂停未写动画时，避免叠加上一轮修正。
        for(int i=0;i<LegBones.Length;i++)if(bones[LegBones[i]]&&Quaternion.Angle(bones[LegBones[i]].localRotation,applied[i])<.0001f)bones[LegBones[i]].localRotation=original[i];
        changed=false;
    }

    public void 重置(){Restore();blend=hipLift=0;hadRaw=false;planted[0]=planted[1]=false;plantBlend[0]=plantBlend[1]=0;}

    public void 推进(float dt,bool flying,bool action)
    {
        if(dt<=0)return;Restore();
        // 闭襟在攻击、施法及旋身闪避中也必须包住下肢，不能在 Action 时放回原始大跨步。
        float target=Mathf.Clamp01(profile.闭襟收束);
        bool freeRoot=flying||action;
        blend=Mathf.MoveTowards(blend,target,dt/ .18f);
        if(blend<.00001f){hadRaw=false;planted[0]=planted[1]=false;plantBlend[0]=plantBlend[1]=0;return;}
        for(int i=0;i<LegBones.Length;i++)original[i]=bones[LegBones[i]].localRotation;
        hipOriginal=bones[1].localPosition;
        var frame=bones[1].localToWorldMatrix*hipBind;var inverse=frame.inverse;
        var left=bones[5].position;var right=bones[15].position;
        原腿距=Mathf.Abs(inverse.MultiplyPoint3x4(left).y-inverse.MultiplyPoint3x4(right).y);
        float half=Mathf.Max(.025f,profile.闭襟腿距*.5f),scale=frame.MultiplyVector(Vector3.forward).magnitude;
        float floor=Mathf.Min(left.y,right.y);
        float lift=0;
        for(int side=0;side<2;side++){
            var raw=side==0?left:right;var local=inverse.MultiplyPoint3x4(raw);float sign=side==0?1:-1;
            float lateral=sign*Mathf.Clamp(sign*(local.y-hipRest.y),.018f,half);
            local.y=Mathf.Lerp(local.y,hipRest.y+lateral,blend);
            // 前后跨步也须留在闭襟衣型中，不能只收左右腿距，仍让整只靴筒跨出前后裙面。
            float stride=action?.040f:flying?.045f:.060f;
            local.x=Mathf.Lerp(local.x,Mathf.Clamp(local.x,footRest[side].x-stride,footRest[side].x+stride),blend);
            var root=bones[side==0?3:12].position;
            if(freeRoot){
                // 闭襟御风减少深屈膝，让原靴筒沿裙内自然下垂；上身仍使用原御风动作。
                var origin=inverse.MultiplyPoint3x4(root);var old=inverse.MultiplyPoint3x4(raw)-origin;var delta=local-origin;
                var knee=bones[side==0?4:13].position;var ankle=bones[side==0?5:15].position;
                float reach=(Vector3.Distance(root,knee)+Vector3.Distance(knee,ankle))/scale;
                float length=Mathf.Lerp(old.magnitude,Mathf.Max(old.magnitude,reach*.985f),blend*.85f);
                float squared=length*length-delta.x*delta.x-delta.y*delta.y;
                // 腾空/翻身的髋根轨迹仍由原动作控制；裙内腿朝自身下方收回，不跟随踢腿翻到腰口。
                float signDown=action?Mathf.Sign(footRest[side].z-hipRest.z):Mathf.Sign(old.z);
                if(squared>0)local.z=origin.z+signDown*Mathf.Sqrt(squared);
                goals[side]=frame.MultiplyPoint3x4(local);
            }else{
                var goal=frame.MultiplyPoint3x4(local);goal.y=Mathf.Lerp(raw.y,floor+(raw.y-floor)*.35f,blend);goals[side]=goal;
                var knee=bones[side==0?4:13].position;
                float length=Vector3.Distance(root,knee)+Vector3.Distance(knee,raw);
                float reach=Mathf.Pow(Mathf.Lerp(Vector3.Distance(raw,root),length*.985f,blend),2);
                var horizontal=Vector3.ProjectOnPlane(goal-root,Vector3.up);
                lift=Mathf.Max(lift,goal.y+Mathf.Sqrt(Mathf.Max(0,reach-horizontal.sqrMagnitude))-root.y);
            }
        }
        // 较窄的站姿自然稍高；只调整视觉髋部，支撑脚和角色碰撞体保持原地。
        hipLift=Mathf.Lerp(hipLift,freeRoot?0:Mathf.Clamp(lift,0,.12f*scale),1-Mathf.Exp(-14*dt));
        if(!flying)bones[1].position+=Vector3.up*hipLift;
        var finalFrame=bones[1].localToWorldMatrix*hipBind;var finalInverse=finalFrame.inverse;
        骨长误差=0;
        for(int side=0;side<2;side++){
            int rootIndex=side==0?3:12,midIndex=side==0?4:13,tipIndex=side==0?5:15;
            var root=bones[rootIndex];var middle=bones[midIndex];var tip=bones[tipIndex];
            var raw=side==0?left:right;var goal=goals[side];
            // 横向收束不抬起支撑脚；地面支撑相保留世界落点，抬脚后释放。
            if(!freeRoot){
                float vertical=hadRaw?(raw.y-lastRaw[side].y)/Mathf.Max(.001f,dt):0;
                if(planted[side]&&(raw.y>floor+.028f*scale||Vector3.ProjectOnPlane(goal-plant[side],Vector3.up).magnitude>.07f*scale))planted[side]=false;
                if(!planted[side]&&plantBlend[side]<.0001f&&blend>.999f&&raw.y<floor+.009f*scale&&Mathf.Abs(vertical)<.15f*scale){planted[side]=true;plant[side]=goal;}
                // 不能释放后同帧直接重锁到新位置，否则支撑脚会瞬移一整段步幅。
                plantBlend[side]=Mathf.MoveTowards(plantBlend[side],planted[side]?1:0,dt/(planted[side]?.06f:.08f));
                goal=Vector3.Lerp(goal,plant[side],plantBlend[side]);
            }else{planted[side]=false;plantBlend[side]=0;}
            // 世界落脚/髋高修正会再次把脚带到袍面外。最终在当前衣型坐标中复核，
            // 不能只在植脚之前裁一次；不依赖动作名，也覆盖持械移动的前后大步。
            var enclosed=finalInverse.MultiplyPoint3x4(goal);float sideSign=side==0?1:-1;
            enclosed.y=hipRest.y+sideSign*Mathf.Clamp(sideSign*(enclosed.y-hipRest.y),.018f,half);
            float strideLimit=action?.040f:flying?.045f:.060f;
            enclosed.x=Mathf.Clamp(enclosed.x,footRest[side].x-strideLimit,footRest[side].x+strideLimit);
            goal=finalFrame.MultiplyPoint3x4(enclosed);
            lastRaw[side]=raw;
            var start=root.position;var knee=middle.position;float upper=Vector3.Distance(start,knee),lower=Vector3.Distance(knee,tip.position);
            var axis=goal-start;float distance=Mathf.Clamp(axis.magnitude,Mathf.Abs(upper-lower)+.00001f,upper+lower-.00001f);axis=axis.normalized;goal=start+axis*distance;
            var pole=Vector3.ProjectOnPlane(knee-start,axis);
            // 膝盖保留前后屈曲，减小原动作向两侧敞开的弯曲平面。
            var localPole=inverse.MultiplyVector(pole);localPole.y*=Mathf.Lerp(1,.20f,blend);
            pole=Vector3.ProjectOnPlane(frame.MultiplyVector(localPole),axis);
            if(pole.sqrMagnitude<1e-10f)pole=Vector3.ProjectOnPlane(-frame.MultiplyVector(Vector3.right),axis);
            pole.Normalize();
            float along=(upper*upper-lower*lower+distance*distance)/(2*distance);
            var desiredKnee=start+axis*along+pole*Mathf.Sqrt(Mathf.Max(0,upper*upper-along*along));
            // 保留翻身根姿态，同时限制裙内脚踝的极端翻折，避免鞋筒从内裙侧面翻出。
            var rotation=action?Quaternion.RotateTowards(finalFrame.rotation*footRestRotation[side],tip.rotation,28):tip.rotation;
            root.rotation=Quaternion.FromToRotation(middle.position-start,desiredKnee-start)*root.rotation;
            middle.rotation=Quaternion.FromToRotation(tip.position-middle.position,goal-middle.position)*middle.rotation;
            tip.rotation=rotation;
            骨长误差=Mathf.Max(骨长误差,Mathf.Abs(Vector3.Distance(start,middle.position)-upper),Mathf.Abs(Vector3.Distance(middle.position,tip.position)-lower));
        }
        hadRaw=true;changed=true;
        hipApplied=bones[1].localPosition;
        for(int i=0;i<LegBones.Length;i++)applied[i]=bones[LegBones[i]].localRotation;
        当前腿距=Mathf.Abs(inverse.MultiplyPoint3x4(bones[5].position).y-inverse.MultiplyPoint3x4(bones[15].position).y);
    }
}
