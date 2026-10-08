using UnityEngine;

/// <summary>Standalone experiment; deliberately does not connect to combat or save data.</summary>
public sealed class VoxelPrototypeDemo : MonoBehaviour
{
    public VoxelDestructible 目标;
    public Camera 观察相机;
    public Vector3 观察中心;
    public float 观察距离=14;
    public float 破坏半径=.6f;
    float yaw=25,pitch=25;
    GUIStyle text;
    Font font;
    void Start() { 定位(); }
    void Update()
    {
        if(Input.GetMouseButton(1)) { yaw+=Input.GetAxis("Mouse X")*4; pitch=Mathf.Clamp(pitch-Input.GetAxis("Mouse Y")*3,5,80); }
        观察距离=Mathf.Clamp(观察距离-Input.mouseScrollDelta.y,3,60); 定位();
        if(Input.GetKeyDown(KeyCode.R)) 目标.初始化();
        if(Input.GetKeyDown(KeyCode.LeftBracket)) 破坏半径=Mathf.Max(.1f,破坏半径-.15f);
        if(Input.GetKeyDown(KeyCode.RightBracket)) 破坏半径=Mathf.Min(5,破坏半径+.15f);
        if(Input.GetMouseButtonDown(0) && Input.mousePosition.y < Screen.height-230)
        {
            var ray=观察相机.ScreenPointToRay(Input.mousePosition);
            if(Physics.Raycast(ray,out var hit,200) && hit.collider.GetComponentInParent<VoxelDestructible>()==目标)
                目标.破坏球(hit.point,破坏半径*(Input.GetKey(KeyCode.LeftShift)?2:1));
        }
    }
    void 定位()
    {
        if(!观察相机) return;
        观察相机.transform.position=观察中心+Quaternion.Euler(pitch,yaw,0)*new Vector3(0,0,-观察距离);
        观察相机.transform.LookAt(观察中心);
    }
    void OnGUI()
    {
        if(!目标 || !目标.数据) return;
        if(text==null) { font=Font.CreateDynamicFontFromOSFont("SimHei",18);text=new GUIStyle(GUI.skin.label) { font=font,fontSize=18,normal={textColor=new Color(.9f,.93f,.86f)} }; }
        GUI.Box(new Rect(12,12,700,210),GUIContent.none);
        GUI.Label(new Rect(26,22,675,200),
            "体素局部破坏原型  ·  左侧原模型 / 右侧可破坏模型\n"+
            "左键挖洞 · Shift 大范围 · 右键旋转 · 滚轮缩放 · [ / ] 调半径 · R 复原\n"+
            $"{(目标.数据.平滑表面?"平滑曲面":"方块表面")} / 格子 {目标.数据.格子边长:F3}m / 每块 {目标.数据.分块边长}³ / 半径 {破坏半径:F2}m\n"+
            $"实体 {目标.剩余体素:N0} / 本次移除 {目标.本次移除} / 影响 {目标.本次影响块} 块 / 待更新 {目标.等待块}\n"+
            $"修改 {目标.最近修改毫秒:F2}ms / 最近网格+碰撞 {目标.最近批次毫秒:F2}ms / 最高批次 {目标.最长批次毫秒:F2}ms\n"+
            $"累计重建 {目标.累计重建块} 块（初始化只加载预烘焙网格）",text);
    }
    void OnDestroy() { if(font) Destroy(font); }
}
