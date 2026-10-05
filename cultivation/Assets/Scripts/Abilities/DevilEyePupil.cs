using UnityEngine;

/// <summary>实体瞳孔偶尔扫视再归中，方向随法环，绝不朝向摄像机。</summary>
public class DevilEyePupil : MonoBehaviour
{
    float 下次转动, 开始;
    bool 转动中;
    Quaternion 朝向;
    void OnEnable() { 下次转动 = Time.time + Random.Range(2f, 5f); }
    void Update()
    {
        if (!转动中 && Time.time >= 下次转动)
        {
            转动中 = true; 开始 = Time.time;
            朝向 = Quaternion.Euler(Random.Range(-15f, 15f), Random.Range(-50f, 50f), Random.Range(-10f, 10f));
        }
        if (!转动中) return;
        float t = (Time.time - 开始) / .65f;
        float 权重 = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
        transform.localRotation = Quaternion.Slerp(Quaternion.identity, 朝向, 权重);
        if (t >= 1f) { 转动中 = false; 下次转动 = Time.time + Random.Range(4f, 8f); }
    }
}
