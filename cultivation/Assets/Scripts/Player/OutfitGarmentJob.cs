using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

/// <summary>原有顺序约束的 Burst 内核；不降低更新频率、迭代次数或网格精度。</summary>
[BurstCompile(CompileSynchronously=true,FloatMode=FloatMode.Strict)]
internal struct OutfitGarmentJob : IJob, IDisposable
{
    public struct Link { public int a,b;public float length,aShare,bShare;public bool bend; }
    public NativeArray<int> canonical;
    public NativeArray<Vector3> rest;
    public NativeArray<Vector3> positions;
    public NativeArray<Vector3> previous;
    public NativeArray<Vector3> targets;
    public NativeArray<Vector3> outward;
    public NativeArray<float> freedom;
    public NativeArray<bool> contact;
    public NativeArray<Link> links;
    public NativeArray<float> posedLengths;
    public NativeArray<float> faceAreas;
    public NativeArray<Vector3> faceCrosses;
    public NativeArray<Vector3> bootAxes;
    public NativeArray<float> bootDenominators;
    public NativeArray<float> bootBaseSquared;
    public NativeArray<float> bootMaximum;
    public NativeArray<Vector3> bootNormals;
    public NativeArray<bool> bootNormalReady;
    public NativeArray<int> faces;
    public NativeArray<Vector3> offsets;
    public NativeArray<Vector3> neighborOffsets;
    public NativeArray<int> neighborCounts;
    public NativeArray<float> contactSpacing;
    public NativeArray<float> inner;
    public NativeArray<Vector3> points;
    public NativeArray<Vector3> starts;
    public NativeArray<Vector3> ends;
    public NativeArray<float> radii;
    public NativeArray<Vector3> legStarts;
    public NativeArray<Vector3> legEnds;
    public NativeArray<float> bootRadii;
    public NativeArray<Vector3> cuffStarts;
    public NativeArray<Vector3> cuffEnds;
    public NativeArray<float> cuffRadii;
    public NativeArray<float> stats;
    public bool fullShell,initialized,enclosed,shoesOnly,solidLegs;
    public Matrix4x4 mesh,inverse,waist,waistSkin,carried;
    public Vector3 velocity;
    public float scale,dt,legRadius;
    int 细腿接触次数,鞋靴接触次数;
    float 最大鞋靴推出距离;
    bool Thin(int i)=>inner.Length>0&&inner[i]>.5f&&legStarts.Length>0&&legStarts.Length>=4;

    void OriginalBoot(int i){
            if(!shoesOnly||inner.Length==0||inner[i]<=.5f||freedom[i]<=0||cuffStarts.Length==0||cuffRadii.Length==0||cuffRadii.Length!=cuffStarts.Length)return;
            for(int side=0;side<cuffRadii.Length;side++){
                if(cuffRadii[side]<=0)continue;
                var a=cuffStarts[side];var axis=bootAxes[side];var nearest=a+axis*Mathf.Clamp01(Vector3.Dot(positions[i]-a,axis)/bootDenominators[side]);
                // 低模长三角的顶点在靴外，面内仍可能穿靴。按相邻面分辨率留弦高净空。
                float radius=Mathf.Sqrt(bootBaseSquared[side]+contactSpacing[i]/3f);var delta=positions[i]-nearest;
                int key=i*4+side;
                if(!bootNormalReady[key]){bootNormals[key]=Vector3.ProjectOnPlane(waistSkin.MultiplyVector(outward[i]),axis).normalized;bootNormalReady[key]=true;}
                var normal=bootNormals[key];if(normal.sqrMagnitude<.1f)continue;
                float dot=Vector3.Dot(delta,normal),discriminant=dot*dot+radius*radius-delta.sqrMagnitude;
                if(discriminant<=0)continue;
                float push=-dot+Mathf.Sqrt(discriminant);if(push<=0)continue;
                // 也处理整片已落在靴筒内侧的情况；仅检测顶点是否位于胶囊内会漏掉这种穿透。
                // 只允许原靴附近的局部弯曲，不改变腰口或整圈裙子的尺寸。
                var candidate=positions[i]+normal*push;
                // 宽靴的局部净空至少容纳自身直径，避免固定上限把末端裙角重新钳回靴内。
                positions[i]=targets[i]+Vector3.ClampMagnitude(candidate-targets[i],bootMaximum[side]);
                鞋靴接触次数++;最大鞋靴推出距离=Mathf.Max(最大鞋靴推出距离,push);
            }
        }

    void Boots(int i){
            if(enclosed)return;
            if(!Thin(i)||legStarts.Length<6||bootRadii.Length==0||bootRadii.Length!=2)return;
            for(int side=0;side<2;side++){
                var a=legStarts[4+side];var axis=legEnds[4+side]-a;var nearest=a+axis*Mathf.Clamp01(Vector3.Dot(positions[i]-a,axis)/Mathf.Max(1e-8f,axis.sqrMagnitude));
                var delta=positions[i]-nearest;float radius=(bootRadii[side]+.004f)*scale;if(delta.sqrMagnitude>=radius*radius)continue;
                var outwardNormal=waistSkin.MultiplyVector(outward[i]).normalized;
                var normal=delta.sqrMagnitude>1e-10f?delta.normalized:outwardNormal;
                // 前襟沿自身外侧向外、向上绕过鞋靴，不把薄面从腿间推向背后。
                if(Vector3.Dot(normal,outwardNormal)<.15f)normal=(normal+outwardNormal*1.5f).normalized;
                float projection=Vector3.Dot(delta,normal);float push=-projection+Mathf.Sqrt(Mathf.Max(0,projection*projection+radius*radius-delta.sqrMagnitude));
                positions[i]+=normal*push;鞋靴接触次数++;最大鞋靴推出距离=Mathf.Max(最大鞋靴推出距离,push);
            }
        }

    void Capsule(int i,int c,out Vector3 a,out Vector3 b,out float radius){
            if(c>0&&Thin(i)){a=legStarts[c-1];b=legEnds[c-1];radius=(legRadius+.004f)*scale;}
            else{a=starts[c];b=ends[c];radius=(radii[c]+.010f)*scale;}
        }

    public void Execute()
    {
        细腿接触次数=0;
        鞋靴接触次数=0;最大鞋靴推出距离=0;
        for(int clear=0;clear<bootNormalReady.Length;clear++)bootNormalReady[clear]=default;
        if(shoesOnly&&cuffStarts.Length>0&&cuffRadii.Length>0&&cuffRadii.Length==cuffStarts.Length){
            for(int side=0;side<cuffRadii.Length;side++){
                bootAxes[side]=cuffEnds[side]-cuffStarts[side];bootDenominators[side]=Mathf.Max(1e-8f,bootAxes[side].sqrMagnitude);
                float radius=(cuffRadii[side]+.010f)*scale;bootBaseSquared[side]=radius*radius;
                bootMaximum[side]=Mathf.Max(.10f,cuffRadii[side]*2)*scale;
            }
        }





        for(int i=0;i<rest.Length;i++)if(canonical[i]==i)targets[i]=mesh.MultiplyPoint3x4(points[i]);
        for(int e=0;e<links.Length;e++)posedLengths[e]=Vector3.Distance(targets[links[e].a],targets[links[e].b]);
        if(fullShell)for(int t=0;t<faces.Length;t+=3){
            int a=canonical[faces[t]],b=canonical[faces[t+1]],c=canonical[faces[t+2]];
            faceCrosses[t/3]=Vector3.Cross(targets[b]-targets[a],targets[c]-targets[a]);faceAreas[t/3]=faceCrosses[t/3].magnitude;
        }
        if(shoesOnly&&inner.Length>0){
            for(int clear=0;clear<contactSpacing.Length;clear++)contactSpacing[clear]=default;
            foreach(var link in links)if(!link.bend&&inner[link.a]>.5f&&inner[link.b]>.5f){
                float squared=(targets[link.a]-targets[link.b]).sqrMagnitude;
                contactSpacing[link.a]=Mathf.Max(contactSpacing[link.a],squared);contactSpacing[link.b]=Mathf.Max(contactSpacing[link.b],squared);
            }
        }
        if(!initialized||dt<=0){
            for(int i=0;i<rest.Length;i++)positions[i]=previous[i]=targets[canonical[i]];
            initialized=true;
        }
        // 密集新袍面采用有界工作量，避免低帧率增加子步、反过来进一步拖低帧率。
        int steps=Mathf.Clamp(Mathf.CeilToInt(dt/(1f/90)),1,fullShell?2:12);float h=dt/steps;
        float damping=Mathf.Exp(-5*h);
        // 腰口和整体姿态携带衣型；下摆保留有限惯性，不把围合袍面当成背后的悬挂物。
        for(int i=0;i<rest.Length;i++)if(canonical[i]==i&&freedom[i]>0){
            var move=carried.MultiplyPoint3x4(positions[i])-positions[i];
            float carry=Mathf.Lerp(1,.92f,freedom[i]);positions[i]+=move*carry;previous[i]+=move*carry;
        }
        for(int step=0;step<steps;step++){
            for(int i=0;i<rest.Length;i++)if(canonical[i]==i){
                float f=freedom[i];if(f<=0){positions[i]=previous[i]=targets[i];continue;}
                var old=positions[i];var speed=(positions[i]-previous[i])*damping;
                // 弱衣型回弹配合强面内抗拉、弱弯曲；每片能弯，不能整袍硬转或自由塌落。
                positions[i]+=speed+(targets[i]-positions[i])*((Thin(i)?35:100)*h*h)-velocity*(.12f*f*h*h);
                previous[i]=old;
            }
            for(int iteration=0;iteration<(fullShell?2:6);iteration++){
                for(int e=0;e<links.Length;e++){
                    var link=links[e];
                    var d=positions[link.b]-positions[link.a];float length=d.magnitude;if(length<1e-7f)continue;
                    float posedLength=posedLengths[e];
                    // 新着装先经过人体比例重定位，静止网格边长不是运行姿态的目标边长。
                    // 只限制相对已蒙皮衣型的拉伸/压缩，不能将整片袍面强行挤回静止长度。
                    float wanted=fullShell?Mathf.Clamp(length,posedLength*.97f,posedLength*1.03f):Mathf.Lerp(link.length*scale,posedLength,.85f);
                    var correction=d*((length-wanted)/length*(link.bend?.035f:.85f));
                    positions[link.a]+=correction*link.aShare;positions[link.b]-=correction*link.bShare;
                }
                for(int i=0;i<rest.Length;i++)if(canonical[i]==i&&freedom[i]>0){
                    float maximum=Mathf.Lerp(.008f,Thin(i)?.10f:fullShell?.05f:.12f,freedom[i])*scale;
                    positions[i]=targets[i]+Vector3.ClampMagnitude(positions[i]-targets[i],maximum);
                    // 避让在网络松弛中传播，最后一次仍保证身体外侧，不能再钳回穿模的位置。
                    if(enclosed&&Thin(i))continue;
                    for(int c=0;c<5;c++){
                        if(shoesOnly&&c>0)continue;
                        Capsule(i,c,out var a,out var b,out float radius);var axis=b-a;var closest=a+axis*Mathf.Clamp01(Vector3.Dot(positions[i]-a,axis)/Mathf.Max(1e-8f,axis.sqrMagnitude));
                        var offset=positions[i]-closest;
                        if(fullShell&&!(c>0&&(Thin(i)||solidLegs&&contact[i]))){
                            var baseClosest=a+axis*Mathf.Clamp01(Vector3.Dot(targets[i]-a,axis)/Mathf.Max(1e-8f,axis.sqrMagnitude));
                            radius=Mathf.Min(radius,Vector3.Distance(targets[i],baseClosest));
                        }
                        if(offset.sqrMagnitude<radius*radius){
                            if(c>0&&Thin(i))细腿接触次数++;
                            // 使用这片袍子的外侧，不能把前襟从双腿缝隙推出到身后。
                            var normal=Vector3.ProjectOnPlane(waistSkin.MultiplyVector(outward[i]),axis).normalized;
                            if(normal.sqrMagnitude<.1f)normal=offset.sqrMagnitude>1e-10f?offset.normalized:mesh.MultiplyVector(Vector3.right).normalized;
                            positions[i]+=normal*Mathf.Max(0,radius-Vector3.Dot(offset,normal));
                        }
                    }
                    Boots(i);
                    OriginalBoot(i);
                }
            }
        }
        // 后片继续使用既有随动，只补转身时原限位不足的身体接触；不让它参与前片的网络重排。
        for(int i=0;i<points.Length;i++)if(canonical[i]==i&&contact[i]){
            if(enclosed&&Thin(i))continue;
            for(int iteration=0;iteration<4;iteration++)for(int c=0;c<5;c++){
                if(shoesOnly&&c>0)continue;
                Capsule(i,c,out var a,out var b,out float radius);var axis=b-a;var nearest=a+axis*Mathf.Clamp01(Vector3.Dot(positions[i]-a,axis)/Mathf.Max(1e-8f,axis.sqrMagnitude));
                var offset=positions[i]-nearest;
                if(fullShell&&!(c>0&&(Thin(i)||solidLegs&&contact[i]))){
                    var baseClosest=a+axis*Mathf.Clamp01(Vector3.Dot(targets[i]-a,axis)/Mathf.Max(1e-8f,axis.sqrMagnitude));
                    radius=Mathf.Min(radius,Vector3.Distance(targets[i],baseClosest));
                }
                if(offset.sqrMagnitude>=radius*radius)continue;
                if(c>0&&Thin(i))细腿接触次数++;
                var normal=Vector3.ProjectOnPlane(waistSkin.MultiplyVector(outward[i]),axis).normalized;
                if(normal.sqrMagnitude<.1f)normal=offset.sqrMagnitude>1e-10f?offset.normalized:mesh.MultiplyVector(Vector3.right).normalized;
                positions[i]+=normal*Mathf.Max(0,radius-Vector3.Dot(offset,normal));
            }
        }
        // 胶囊是保守代理，不是衣型。不能在最后一次接触处理时绕过位移上限，
        // 把已连续蒙皮的前后袍面逐点推出成翻面三角。
        if(fullShell)for(int i=0;i<points.Length;i++)if(canonical[i]==i)
            positions[i]=targets[i]+Vector3.ClampMagnitude(positions[i]-targets[i],Mathf.Lerp(.008f,Thin(i)?.10f:.05f,freedom[i])*scale);
        if(fullShell){
            for(int i=0;i<points.Length;i++)offsets[i]=positions[i]-targets[i];
            // 对连续袍面传递的是附加位移。相邻薄层/细三角不能各自承担独立推出量，
            // 否则边长尚未超限，也会形成密集的微小翻折。
            for(int iteration=0;iteration<6;iteration++){
                for(int clear=0;clear<neighborOffsets.Length;clear++)neighborOffsets[clear]=default;
                foreach(var link in links)if(!link.bend){neighborOffsets[link.a]+=offsets[link.b];neighborOffsets[link.b]+=offsets[link.a];}
                for(int i=0;i<points.Length;i++)if(canonical[i]==i&&neighborCounts[i]>0)
                    offsets[i]=freedom[i]>0?Vector3.Lerp(offsets[i],neighborOffsets[i]/neighborCounts[i],Thin(i)?.45f:.75f):Vector3.zero;
            }
            for(int i=0;i<points.Length;i++)if(canonical[i]==i)positions[i]=targets[i]+offsets[i];
        }
        if(fullShell)for(int iteration=0;iteration<3;iteration++){
            bool repaired=false;
            for(int t=0;t<faces.Length;t+=3){
                int a=canonical[faces[t]],b=canonical[faces[t+1]],c=canonical[faces[t+2]];
                var before=faceCrosses[t/3];
                float area=faceAreas[t/3];if(area<1e-9f)continue;
                var after=Vector3.Cross(positions[b]-positions[a],positions[c]-positions[a]);
                // 面内抗拉还需要保留三角形朝向；只检查边长会允许薄袍面折到反面。
                if(Vector3.Dot(before,after)>=area*after.magnitude*.65f&&after.magnitude>=area*.5f&&after.magnitude<=area*2)continue;
                positions[a]=Vector3.Lerp(positions[a],targets[a],.5f);
                positions[b]=Vector3.Lerp(positions[b],targets[b],.5f);
                positions[c]=Vector3.Lerp(positions[c],targets[c],.5f);repaired=true;
            }
            if(!repaired)break;
        }
        // 网络平滑与翻面修复会把接触点带回腿内；闭襟在输出前以细腿半径做最终接触投影。
        if(!enclosed&&inner.Length>0&&legStarts.Length>0)for(int i=0;i<points.Length;i++)if(canonical[i]==i&&Thin(i)&&freedom[i]>0){
            // 同一袍面沿自己的外侧退出所有接触体，避免两条腿/鞋靴之间来回投影。
            var normal=waistSkin.MultiplyVector(outward[i]).normalized;if(normal.sqrMagnitude<.1f)normal=mesh.MultiplyVector(Vector3.right).normalized;
            for(int iteration=0;iteration<6;iteration++){
                bool hit=false;
                for(int c=0;c<legStarts.Length;c++){
                    if(c>=4&&(bootRadii.Length==0||bootRadii.Length!=2))continue;
                    var a=legStarts[c];var axis=legEnds[c]-a;var nearest=a+axis*Mathf.Clamp01(Vector3.Dot(positions[i]-a,axis)/Mathf.Max(1e-8f,axis.sqrMagnitude));
                    var delta=positions[i]-nearest;float radius=((c<4?legRadius:bootRadii[c-4])+.004f)*scale;
                    if(delta.sqrMagnitude>=radius*radius)continue;
                    float projection=Vector3.Dot(delta,normal);float push=-projection+Mathf.Sqrt(Mathf.Max(0,projection*projection+radius*radius-delta.sqrMagnitude))+.00001f;
                    positions[i]+=normal*push;hit=true;
                    if(c<4)细腿接触次数++;else{鞋靴接触次数++;最大鞋靴推出距离=Mathf.Max(最大鞋靴推出距离,push);}
                }
                if(!hit)break;
            }
        }
        if(shoesOnly)for(int i=0;i<points.Length;i++)if(canonical[i]==i)OriginalBoot(i);
        // 实体裤腿在袍面之外：不能再把接触半径缩到已穿入的蒙皮目标，或在平滑后钳回腿内。
        // 仅约束外摆接触点，袖子、发束与旧闭襟配置沿原路径运行。
        if(solidLegs)for(int pass=0;pass<3;pass++)for(int i=0;i<points.Length;i++)if(canonical[i]==i&&contact[i]&&freedom[i]>0){
            for(int c=1;c<5;c++){
                Capsule(i,c,out var a,out var b,out float radius);var axis=b-a;var nearest=a+axis*Mathf.Clamp01(Vector3.Dot(positions[i]-a,axis)/Mathf.Max(1e-8f,axis.sqrMagnitude));var delta=positions[i]-nearest;
                // 面内净空补偿：稀疏长三角不能只让三个角点刚好贴住腿。
                radius=Mathf.Sqrt(radius*radius+Mathf.Min(contactSpacing[i],.0009f*scale*scale)/12);
                if(delta.sqrMagnitude>=radius*radius)continue;
                var normal=delta.sqrMagnitude>1e-10f?delta.normalized:Vector3.ProjectOnPlane(waistSkin.MultiplyVector(outward[i]),axis).normalized;
                if(normal.sqrMagnitude<.1f)continue;
                var correction=normal*(radius-delta.magnitude+.0001f*scale);positions[i]+=correction;
                previous[i]+=correction;
            }
        }
        for(int i=0;i<points.Length;i++)if(freedom[canonical[i]]>0||contact[canonical[i]])points[i]=inverse.MultiplyPoint3x4(positions[canonical[i]]);
        stats[0]=细腿接触次数;stats[1]=鞋靴接触次数;stats[2]=最大鞋靴推出距离;
    }
    public void Dispose()
    {
        if(canonical.IsCreated)canonical.Dispose();
        if(rest.IsCreated)rest.Dispose();
        if(positions.IsCreated)positions.Dispose();
        if(previous.IsCreated)previous.Dispose();
        if(targets.IsCreated)targets.Dispose();
        if(outward.IsCreated)outward.Dispose();
        if(freedom.IsCreated)freedom.Dispose();
        if(contact.IsCreated)contact.Dispose();
        if(links.IsCreated)links.Dispose();
        if(posedLengths.IsCreated)posedLengths.Dispose();
        if(faceAreas.IsCreated)faceAreas.Dispose();
        if(faceCrosses.IsCreated)faceCrosses.Dispose();
        if(bootAxes.IsCreated)bootAxes.Dispose();
        if(bootDenominators.IsCreated)bootDenominators.Dispose();
        if(bootBaseSquared.IsCreated)bootBaseSquared.Dispose();
        if(bootMaximum.IsCreated)bootMaximum.Dispose();
        if(bootNormals.IsCreated)bootNormals.Dispose();
        if(bootNormalReady.IsCreated)bootNormalReady.Dispose();
        if(faces.IsCreated)faces.Dispose();
        if(offsets.IsCreated)offsets.Dispose();
        if(neighborOffsets.IsCreated)neighborOffsets.Dispose();
        if(neighborCounts.IsCreated)neighborCounts.Dispose();
        if(contactSpacing.IsCreated)contactSpacing.Dispose();
        if(inner.IsCreated)inner.Dispose();
        if(points.IsCreated)points.Dispose();
        if(starts.IsCreated)starts.Dispose();
        if(ends.IsCreated)ends.Dispose();
        if(radii.IsCreated)radii.Dispose();
        if(legStarts.IsCreated)legStarts.Dispose();
        if(legEnds.IsCreated)legEnds.Dispose();
        if(bootRadii.IsCreated)bootRadii.Dispose();
        if(cuffStarts.IsCreated)cuffStarts.Dispose();
        if(cuffEnds.IsCreated)cuffEnds.Dispose();
        if(cuffRadii.IsCreated)cuffRadii.Dispose();
        if(stats.IsCreated)stats.Dispose();
    }
}
