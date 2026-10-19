using System;
using UnityEngine;

/// <summary>外围方块围绕各自原位置小幅漂浮；中央方块不运动，预览暂停时仍可展示。</summary>
public sealed class FloatingCubeCluster : MonoBehaviour
{
    [Serializable] public struct Piece{
        public Transform transform;
        public Vector3 position;
        public Quaternion rotation;
        public float amplitude,phase;
    }
    public Piece[] pieces=new Piece[0];
    float origin;
    void OnEnable(){origin=Time.unscaledTime;}
    void Update(){Evaluate(Time.unscaledTime-origin);}
    public void Evaluate(float time)
    {
        foreach(var p in pieces)if(p.transform){
            float wave=Mathf.Sin(time*.9f+p.phase)-Mathf.Sin(p.phase);
            float drift=Mathf.Sin(time*.53f+p.phase*1.7f)-Mathf.Sin(p.phase*1.7f);
            p.transform.localPosition=p.position+new Vector3(drift*.22f,wave*.18f,wave)*p.amplitude;
            // 仅摆动几度，避免外围方块互相扫过或穿入中心。
            p.transform.localRotation=p.rotation*Quaternion.Euler(wave*2.3f,drift*3.2f,wave*1.7f);
        }
    }
}
