using UnityEngine;

/// <summary>复用 SwordDance 原始发射配置，避免演示场景的全局倍率与不受控子物体。</summary>
public class AbilityVfxEmitter : MonoBehaviour
{
    public GameObject[] 预制体;
    public float 开始延迟, 生成间隔;
    public int 生成次数;
    public Vector3 随机位置, 随机角度, 随机缩放;
    float 开始, 下次;
    int 已生成;
    void OnEnable() { 开始 = Time.time; 下次 = 开始 + Mathf.Max(0f, 开始延迟); 已生成 = 0; }
    void Update()
    {
        if (Time.time < 下次 || 已生成 >= 生成次数) return;
        已生成++; 下次 = Time.time + Mathf.Max(.01f, 生成间隔);
        if (预制体 == null) return;
        foreach (var 预制 in 预制体)
        {
            if (预制 == null) continue;
            var 子 = Instantiate(预制, transform);
            foreach (var 节点 in 子.GetComponentsInChildren<Transform>(true)) 节点.gameObject.layer = gameObject.layer;
            子.transform.localPosition = 随机(随机位置);
            子.transform.localRotation = Quaternion.Euler(随机(随机角度));
            子.transform.localScale = 预制.transform.localScale + new Vector3(Random.Range(0f, 随机缩放.x), Random.Range(0f, 随机缩放.y), Random.Range(0f, 随机缩放.z));
        }
    }
    static Vector3 随机(Vector3 范围) => new Vector3(Random.Range(-范围.x, 范围.x), Random.Range(-范围.y, 范围.y), Random.Range(-范围.z, 范围.z));
}
