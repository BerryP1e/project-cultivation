using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>家园建筑的场景表现和背包事务；计时和生产只在摆设的数据层推进。</summary>
public sealed class HomeBuilding : MonoBehaviour
{
    public int 编号;
    public 摆设状态 状态 => 摆设.取().状态(编号);
    public string 提示 { get; private set; } = "";
    readonly Dictionary<string, HomeAnimalView> animals = new Dictionary<string, HomeAnimalView>();
    TextMesh warning;
    ParticleSystem work;
    float nextSync;
    void Start()
    {
        var s = 状态; if (s == null) return; s.确保建筑状态();
        var interact = gameObject.AddComponent<StationInteractable>(); interact.类型 = StationInteractable.StationKind.家园建筑;
        interact.显示名 = s.名; interact.交互距离 = s.类型id == "animal_pen" ? 6 : 4; interact.提示最高点 = 2.7f;
        var label = new GameObject("生产警示"); label.transform.SetParent(transform, false);
        label.transform.localPosition = Vector3.up * (s.类型id == "animal_pen" ? 2.2f : 2.8f);
        warning = label.AddComponent<TextMesh>(); warning.text = "!"; warning.fontSize = 60; warning.characterSize = .08f;
        warning.anchor = TextAnchor.MiddleCenter; warning.color = new Color(1,.35f,.2f); label.SetActive(false);
        if (s.类型id != "animal_pen")
        {
            var steam = new GameObject("生产灵烟"); steam.transform.SetParent(transform, false); steam.transform.localPosition = Vector3.up * (s.类型id == "compost_bin" ? 1 : 2);
            steam.transform.localRotation = Quaternion.Euler(-90,0,0);
            work = steam.AddComponent<ParticleSystem>(); var main = work.main; main.startLifetime = 1.4f; main.startSpeed = .2f; main.startSize = .09f;
            main.startColor = s.类型id == "auto_alchemy" ? new Color(.8f,.65f,.3f,.4f) : new Color(.55f,.7f,.5f,.25f);
            main.maxParticles = 24; main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = work.emission; emission.rateOverTime = 7; var shape = work.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.radius = .15f;
            var fade = work.colorOverLifetime; fade.enabled = true;
            var gradient = new Gradient(); gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.15f),new GradientAlphaKey(0,1)}); fade.color = gradient;
            var renderer = steam.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = Resources.Load<Material>("Home/WorkMist");
            work.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
    void Update()
    {
        if (warning && Camera.main) warning.transform.rotation = Camera.main.transform.rotation;
        if (Time.unscaledTime < nextSync) return; nextSync = Time.unscaledTime + .25f;
        var s = 状态; if (s == null) return;
        if (s.类型id == "animal_pen") SyncAnimals(s.兽栏);
        var status = 生产状态();
        if (warning) warning.gameObject.SetActive(status.Contains("饥饿") || status.Contains("已满"));
        bool busy = s.类型id == "auto_alchemy" ? !string.IsNullOrEmpty(s.炼丹.在炼丹方id) && s.炼丹.剩余天数 > 0 : s.堆肥.原料.数量(HomeAnimalCatalog.粪便id) >= HomeCompostState.每份粪便 && s.堆肥.成品.能存(HomeAnimalCatalog.肥料id, 1);
        if (work && busy != work.isEmitting) { if (busy) work.Play(); else work.Stop(true, ParticleSystemStopBehavior.StopEmitting); }
    }
    void SyncAnimals(HomePenState pen)
    {
        var catalog = HomeAnimalCatalog.取(); if (!catalog) return;
        foreach (var id in animals.Keys.ToArray()) if (!pen.动物.Exists(a => a.uid == id)) { Destroy(animals[id].gameObject); animals.Remove(id); }
        foreach (var a in pen.动物)
        {
            if (animals.ContainsKey(a.uid)) continue;
            var d = catalog.找(a.物种id); if (d == null || !d.模型) continue;
            var root = new GameObject(d.名); root.transform.SetParent(transform, false);
            var view = root.AddComponent<HomeAnimalView>(); view.初始化(d, a); animals.Add(a.uid, view);
        }
    }
    public string 生产状态()
    {
        var s = 状态; if (s == null) return ""; s.确保建筑状态();
        if (s.类型id == "animal_pen")
        {
            int hungry = s.兽栏.动物.Count(a => a.饱食天数 <= 0);
            var catalog = HomeAnimalCatalog.取();
            bool full = s.兽栏.动物.Any(a => {
                var d = catalog ? catalog.找(a.物种id) : null;
                return d != null && (!s.兽栏.收获篮.能存(HomeAnimalCatalog.粪便id,d.粪便数量) || (!string.IsNullOrEmpty(d.产物id) && !s.兽栏.收获篮.能存(d.产物id,1)));
            });
            return "容量 " + s.兽栏.占用(HomeAnimalCatalog.取()) + "/10 · " + s.兽栏.动物.Count + " 只" + (hungry > 0 ? " · " + hungry + " 只饥饿" : " · 饱食") + (full ? " · 收获篮已满" : "");
        }
        if (s.类型id == "compost_bin")
        {
            if (!s.堆肥.成品.能存(HomeAnimalCatalog.肥料id, 1)) return "成品格已满 · 暂停堆肥";
            if (s.堆肥.原料.数量(HomeAnimalCatalog.粪便id) < 3) return "等待粪便 · 每 3 份产出 1 份肥料";
            return "堆肥中 " + (s.堆肥.进度天 / HomeCompostState.每份天数).ToString("P0");
        }
        if (!string.IsNullOrEmpty(s.炼丹.在炼丹方id)) return (灵丹库.取(s.炼丹.在炼丹方id)?.名 ?? "炼丹") + (s.炼丹.剩余天数 > 0 ? " · 剩余 " + s.炼丹.剩余天数.ToString("0.00") + " 天" : " · 成品格已满");
        return !s.炼丹.自动炼制 ? "自动炼制已暂停" : string.IsNullOrEmpty(s.炼丹.开炉原因()) ? "准备开炉" : s.炼丹.开炉原因();
    }
    public bool 放入动物(ItemDefinition item, UIPanelData bag)
    {
        var catalog = HomeAnimalCatalog.取(); var d = catalog ? catalog.按幼崽(item ? item.物品id : "") : null;
        var s = 状态; if (s == null || s.类型id != "animal_pen" || !bag || d == null) return Fail("请选择动物幼崽");
        if (!s.兽栏.能放(d, catalog)) return Fail("兽栏容量不足");
        if (!bag.移除物品(item, 1)) return Fail("背包中没有幼崽");
        s.兽栏.放入(d, catalog); 提示 = "放入" + d.名; return true;
    }
    public bool 取出动物(string uid, UIPanelData bag)
    {
        var s = 状态; if (s == null || s.类型id != "animal_pen" || !bag) return false;
        var a = s.兽栏.动物.Find(v => v.uid == uid); var d = a != null ? HomeAnimalCatalog.取().找(a.物种id) : null;
        var item = d != null ? QuestDatabase.取().找物品(d.幼崽id) : null; if (!item) return Fail("幼崽物品不存在");
        s.兽栏.动物.Remove(a); bag.给物品(item, 1); 提示 = "取回" + d.名; return true;
    }
    public bool 存入(HomeBuildingStorage storage, ItemDefinition item, int count, UIPanelData bag, int index = -1)
    {
        if (!bag || !item || storage == null) return false;
        if (!允许存入(storage, item.物品id)) return Fail("这件物品不能放入此格");
        if (!storage.能存(item.物品id, count, index)) return Fail("该格已满或已有其他物品");
        if (!bag.移除物品(item, count)) return Fail("背包数量不足");
        storage.存入(item.物品id, count, index); 提示 = "已放入" + item.物品名; return true;
    }
    public bool 允许存入(HomeBuildingStorage storage, string id)
    {
        var s = 状态; if (s == null) return false;
        if (s.类型id == "animal_pen") return storage == s.兽栏.食槽 && HomeAnimalCatalog.是食物(id);
        if (s.类型id == "compost_bin") return storage == s.堆肥.原料 && id == HomeAnimalCatalog.粪便id;
        if (s.类型id == "auto_alchemy") return (storage == s.炼丹.主材 || storage == s.炼丹.辅材) && 灵丹库.全部.Any(d => d.主材id == id || 炼丹炉.解析材料(d.辅材).Any(k => k.Key == id));
        return false;
    }
    public bool 取回(HomeBuildingStorage storage, int index, UIPanelData bag)
    {
        if (!bag || storage == null || index < 0 || index >= storage.格.Count) return false;
        var slot = storage.格[index]; var item = QuestDatabase.取().找物品(slot.物品id); int n = slot.数量;
        if (!item || n <= 0) return Fail("此格为空");
        storage.取出(slot.物品id, n); bag.给物品(item, n); 提示 = "已取回" + item.物品名; return true;
    }
    bool Fail(string message) { 提示 = message; return false; }
}

/// <summary>原生兽类骨架直接播原生动画；沿栏内漫步，半径包含体长，避免穿过栅栏和食槽。</summary>
public sealed class HomeAnimalView : MonoBehaviour
{
    HomeAnimalDefinition definition;
    HomeAnimalState state;
    PlayableGraph graph;
    AnimationMixerPlayable mixer;
    Vector3 destination;
    float rest, blend;
    public void 初始化(HomeAnimalDefinition d, HomeAnimalState s)
    {
        definition = d; state = s;
        // 半径 3.05 留出栏内食槽；源模型外圈半径约 4 米。
        transform.localPosition = PickPoint(); destination = transform.localPosition; rest = Random.Range(1, 4);
        var model = Instantiate(d.模型, transform); model.name = d.名 + "模型";
        model.transform.localPosition = d.模型.transform.localPosition;
        var animator = model.GetComponentInChildren<Animator>();
        if (!animator || !d.待机 || !d.行走) return;
        animator.runtimeAnimatorController = null; animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        graph = PlayableGraph.Create("兽栏_" + d.id); graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        mixer = AnimationMixerPlayable.Create(graph, 2);
        var idle = AnimationClipPlayable.Create(graph, d.待机); var walk = AnimationClipPlayable.Create(graph, d.行走);
        idle.SetApplyFootIK(false); walk.SetApplyFootIK(false);
        graph.Connect(idle,0,mixer,0); graph.Connect(walk,0,mixer,1); mixer.SetInputWeight(0,1);
        var output = AnimationPlayableOutput.Create(graph,"原生骨架",animator); output.SetSourcePlayable(mixer); graph.Play();
    }
    Vector3 PickPoint()
    {
        for (int tries = 0; tries < 20; tries++)
        {
            var p = Random.insideUnitCircle * Mathf.Max(.5f, 3.05f - definition.半径);
            var v = new Vector3(p.x,.035f,p.y);
            var neighbors = transform.parent ? transform.parent.GetComponentsInChildren<HomeAnimalView>() : new HomeAnimalView[0];
            if (neighbors.All(other => other == this || !other || other.definition == null || (other.transform.localPosition-v).sqrMagnitude > Mathf.Pow((definition.半径+other.definition.半径)*.85f,2))) return v;
        }
        return Vector3.up*.035f;
    }
    void Update()
    {
        var clock = 时间管理器.取(); if (clock && clock.暂停) { if (graph.IsValid()) graph.Stop(); return; }
        if (graph.IsValid() && !graph.IsPlaying()) graph.Play();
        if (rest > 0) { rest -= Time.deltaTime; if (rest <= 0) destination = PickPoint(); }
        var delta = destination - transform.localPosition; delta.y = 0;
        bool moving = rest <= 0 && delta.magnitude > .04f;
        if (moving)
        {
            float speed = state.饱食天数 > 0 ? .45f : .25f;
            transform.localPosition = Vector3.MoveTowards(transform.localPosition,destination,Time.deltaTime*speed);
            transform.localRotation = Quaternion.Slerp(transform.localRotation,Quaternion.LookRotation(delta),1-Mathf.Exp(-Time.deltaTime*5));
        }
        else if (rest <= 0) rest = Random.Range(2,5);
        blend = Mathf.MoveTowards(blend,moving ? 1 : 0,Time.deltaTime*4);
        if (mixer.IsValid()) { mixer.SetInputWeight(0,1-blend); mixer.SetInputWeight(1,blend); }
    }
    void OnDestroy() { if (graph.IsValid()) graph.Destroy(); }
}
