using UnityEngine;

/// <summary>落剑沿发射点前方移动，按父级缩放换算；触地特效归属剑阵，不依赖演示场景。</summary>
public class AbilityFallingSword : MonoBehaviour
{
    public float 速度 = 40f;
    public GameObject 触地特效;
    float 地面高度, 出生时间;
    void Start()
    {
        var 阵 = GetComponentInParent<DirectedAbilityRunner>();
        地面高度 = 阵 != null ? 阵.transform.position.y : transform.root.position.y;
        出生时间 = Time.time;
    }
    void Update()
    {
        Vector3 前 = transform.position;
        transform.localPosition += transform.localRotation * Vector3.forward * (速度 * Time.deltaTime);
        Vector3 后 = transform.position;
        if (后.y <= 地面高度 && 前.y > 地面高度)
        {
            float t = Mathf.InverseLerp(后.y, 前.y, 地面高度);
            Vector3 命中 = Vector3.Lerp(后, 前, t);
            if (触地特效 != null)
            {
                var 特效 = Instantiate(触地特效, transform.parent);
                特效.transform.position = 命中;
                特效.transform.rotation = Quaternion.identity;
                foreach (var 节点 in 特效.GetComponentsInChildren<Transform>(true)) 节点.gameObject.layer = gameObject.layer;
                Destroy(特效, 1f);
            }
            Destroy(gameObject);
        }
        else if (Time.time - 出生时间 > 2f) Destroy(gameObject);
    }
}
