using UnityEngine;
public sealed class HomeSurface : MonoBehaviour
{
    public Mesh 运行网格;
    void OnDestroy() { if (运行网格) Destroy(运行网格); }
}
