using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Incremental connectivity check: only prune material that originally connected to the roots.</summary>
public static class VoxelTreeSupport
{
    public static IEnumerator 检查(VoxelDestructible body,Action<bool> complete,Func<byte[],float[],IEnumerator> detach=null)
    {
        var data=body.数据;var size=data.尺寸;int length=data.实体.Length;
        var original=data.实体;var current=new byte[length];
        var occupied=data.实体索引;
        if(occupied==null || occupied.Length!=data.实体数量)
        {var list=new List<int>();for(int i=0;i<length;i++)if(original[i]!=0)list.Add(i);occupied=list.ToArray();}
        float lowest=float.PositiveInfinity,highest=float.NegativeInfinity;
        var heights=new float[length];int work=0;
        foreach(int i in occupied)
        {
            int x=i%size.x,y=(i/size.x)%size.y,z=i/(size.x*size.y);
            if(original[i]!=0)
            {
                float h=body.transform.TransformPoint(data.原点+new Vector3(x+.5f,y+.5f,z+.5f)*data.格子边长).y;
                heights[i]=h;lowest=Mathf.Min(lowest,h);highest=Mathf.Max(highest,h);
                current[i]=(byte)(body.取实体(x,y,z)?1:0);
            }
            if(++work%2048==0)yield return null;
        }
        float rootBand=data.格子边长*body.transform.TransformVector(Vector3.up).magnitude*1.6f;
        var baseline=new bool[length];var supported=new bool[length];
        for(int pass=0;pass<2;pass++)
        {
            var cells=pass==0?original:current;var seen=pass==0?baseline:supported;var queue=new Queue<int>();
            foreach(int i in occupied)
            {
                if(cells[i]!=0 && heights[i]<=lowest+rootBand){seen[i]=true;queue.Enqueue(i);}
                if(++work%2048==0)yield return null;
            }
            while(queue.Count>0)
            {
                int i=queue.Dequeue(),x=i%size.x,y=(i/size.x)%size.y,z=i/(size.x*size.y);
                if(x>0)Visit(i-1,cells,seen,queue);if(x+1<size.x)Visit(i+1,cells,seen,queue);
                if(y>0)Visit(i-size.x,cells,seen,queue);if(y+1<size.y)Visit(i+size.x,cells,seen,queue);
                if(z>0)Visit(i-size.x*size.y,cells,seen,queue);if(z+1<size.z)Visit(i+size.x*size.y,cells,seen,queue);
                if(++work%1024==0)yield return null;
            }
        }
        var prune=new List<int>();int upper=0,lost=0;float crown=Mathf.Lerp(lowest,highest,.6f);
        foreach(int i in occupied)
        {
            if(baseline[i])
            {
                if(heights[i]>=crown){upper++;if(!supported[i])lost++;}
                if(current[i]!=0 && !supported[i])prune.Add(i);
            }
            if(++work%2048==0)yield return null;
        }
        bool fallen=upper>0 && lost>upper*.5f;
        if(fallen && detach!=null)
        {
            var loose=new byte[length];foreach(int i in prune)loose[i]=1;
            var field=body.复制距离();
            if(field!=null)for(int i=0;i<length;i++)
            {if(loose[i]==0)field[i]=-Mathf.Max(data.格子边长,Mathf.Abs(field[i]));if(i%4096==4095)yield return null;}
            var build=detach(loose,field);if(build!=null)while(build.MoveNext())yield return null;
        }
        for(int start=0;start<prune.Count;start+=256)
        {body.移除断枝(prune,start,Mathf.Min(256,prune.Count-start));yield return null;}
        complete(fallen);
    }
    static void Visit(int i,byte[] cells,bool[] seen,Queue<int> queue)
    {if(cells[i]!=0 && !seen[i]){seen[i]=true;queue.Enqueue(i);}}
}
