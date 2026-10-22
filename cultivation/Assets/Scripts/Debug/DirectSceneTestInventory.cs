#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>仅编辑器直接启动洞府时使用临时全物品背包；从主菜单进入的正式流程不执行。</summary>
public sealed class DirectSceneTestInventory : MonoBehaviour
{
    public static bool Active {get;private set;}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset(){Active=false;SceneManager.sceneLoaded-=SceneLoaded;}
    public static bool Eligible(string scene,bool hasSession,bool isolated)=>scene=="3C_Testbed"&&!hasSession&&!isolated;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Begin()
    {
        bool isolated=!string.IsNullOrEmpty(SessionState.GetString(SaveSystem.验证目录键,""));
        if(!Eligible(SceneManager.GetActiveScene().name,SaveSystem.当前存档!=null,isolated))return;
        Active=true;SceneManager.sceneLoaded+=SceneLoaded;
        var host=new GameObject("DirectSceneTestInventory");DontDestroyOnLoad(host);host.AddComponent<DirectSceneTestInventory>();
    }
    static void SceneLoaded(Scene scene,LoadSceneMode mode){if(scene.name=="StartScene"){Active=false;SceneManager.sceneLoaded-=SceneLoaded;}}
    IEnumerator Start()
    {
        yield return null;var data=FindObjectOfType<UIPanelData>();if(!data){Destroy(gameObject);yield break;}
        var catalog=AssetDatabase.FindAssets("t:ItemDefinition",new[]{"Assets"}).Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<ItemDefinition>).Where(i=>i&&!string.IsNullOrEmpty(i.物品id)).GroupBy(i=>i.物品id).Select(g=>g.First()).OrderBy(i=>i.物品id,System.StringComparer.Ordinal).ToList();
        data.EnsureLists();data.已获得武器=WeaponCatalog.全部.Where(w=>w).ToList();data.物品=catalog;data.法则碎片.Clear();data.炉中法则.Clear();for(int i=0;i<data.法则装备.Count;i++)data.法则装备[i]="";data.RaiseChanged();
        Debug.Log("[直接场景测试] 全目录 "+catalog.Count+" 种物品各一件（含法则宝箱，不含法则碎片）；存档位于 Library/DirectSceneTest/saves。");Destroy(gameObject);
    }
}
#endif
