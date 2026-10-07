using UnityEngine;

/// <summary>丹房对白和炼丹期间保留师兄的站位，避免待机 AI 或根运动把他带进建筑。</summary>
[DisallowMultipleComponent]
public class StoryCompanionAnchor : MonoBehaviour
{
    NpcAiBase ai;
    bool wasEnabled;
    Vector3 position;
    void OnEnable()
    {
        position=transform.position;ai=GetComponent<NpcAiBase>();wasEnabled=ai!=null && ai.enabled;
        if(ai!=null)ai.enabled=false;
    }
    void LateUpdate(){transform.position=position;}
    void OnDisable(){if(ai!=null)ai.enabled=wasEnabled;}
}
