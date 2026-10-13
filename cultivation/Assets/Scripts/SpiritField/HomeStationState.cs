using System;
using System.Collections.Generic;

[Serializable]
public sealed class HomeStorageSlot
{
    public string 物品id = "";
    public int 数量;
}

/// <summary>仓库、任务与携带的种子均属于基站存档；四格按物品类型堆叠。</summary>
[Serializable]
public sealed class HomeStationState
{
    public bool 种田任务;
    public List<HomeStorageSlot> 仓库 = new List<HomeStorageSlot>();
    public string 携带种子 = "";
    public string 携带灵植 = "";
    public int 目标田 = -1;
    public float 离场作业秒;
    public void 确保()
    {
        if (仓库 == null) 仓库 = new List<HomeStorageSlot>();
        while (仓库.Count < 4) 仓库.Add(new HomeStorageSlot());
        for(int i=0;i<4;i++) if(仓库[i]==null)仓库[i]=new HomeStorageSlot();
        foreach (var slot in 仓库) HomeAnimalCatalog.清理退役物品(slot);
    }
    public bool 能存(string id, int count=1)
    {
        确保();
        if (string.IsNullOrEmpty(id) || count<=0) return false;
        foreach(var slot in 仓库) if(slot.物品id==id) return slot.数量<=int.MaxValue-count;
        foreach(var slot in 仓库) if(string.IsNullOrEmpty(slot.物品id)) return true;
        return false;
    }
    public bool 存入(string id,int count)
    {
        if (!能存(id,count))return false;
        HomeStorageSlot empty=null;
        foreach(var s in 仓库) { if(s.物品id==id){s.数量+=count;return true;}if(string.IsNullOrEmpty(s.物品id))empty=s; }
        empty.物品id=id;empty.数量=count;return true;
    }
    public bool 取出(string id,int count)
    {
        确保();if(count<=0)return false;
        foreach(var s in 仓库) if(s.物品id==id&&s.数量>=count)
        {
            s.数量-=count;
            if(s.数量==0 && 携带种子!=id) s.物品id="";
            return true;
        }
        return false;
    }
    public int 数量(string id) { 确保();foreach(var s in 仓库)if(s.物品id==id)return s.数量;return 0; }
    public bool 带种子(string plant,int field)
    {
        var d=灵植库.取(plant);
        if(d==null||!string.IsNullOrEmpty(携带种子)||数量(d.种子id)<1)return false;
        携带种子=d.种子id; 携带灵植=plant;目标田=field;
        return 取出(d.种子id,1);
    }
    public bool 消耗携带种子(string id)
    {
        if(携带种子!=id)return false;
        携带种子="";携带灵植="";目标田=-1;
        foreach(var s in 仓库)if(s.数量==0)s.物品id="";
        return true;
    }
    public void 归还种子()
    {
        if(string.IsNullOrEmpty(携带种子))return;
        string id=携带种子;
        if(存入(id,1)){携带种子="";携带灵植="";目标田=-1;}
    }
}
