using UnityEngine;

/// <summary>洞府未加载时只推进数据任务，不生成模型，不计算离线时间。</summary>
public static class HomeFarmBackground
{
    public static void 推进(float seconds)
    {
        if(seconds<=0||时间管理器.取().暂停)return;
        var farm=灵田.取();
        foreach(var furnishing in 摆设.取().所有摆设)
        {
            if(furnishing==null||furnishing.类型id!="puppet_station")continue;
            var state=furnishing.傀儡基站;if(state==null||!state.种田任务)continue;
            state.归还种子();
            bool blocked=false;
            foreach(var b in farm.所有地块)if(b!=null&&b.已成熟&&!state.能存(b.植.产物id))blocked=true;
            if(blocked)continue;
            state.离场作业秒+=seconds;
            if(state.离场作业秒<25)continue;
            state.离场作业秒=0;
            bool done=false;
            for(int priority=0;priority<3&&!done;priority++)for(int i=0;i<farm.总块数&&!done;i++)
            {
                var b=farm.状态(i);if(b==null)continue;
                if(priority==0&&b.已成熟)done=farm.自动收获(i,state.存入)>0;
                if(priority==1&&b.需要浇水)done=farm.浇水(i);
                if(priority==2&&b.待种植)foreach(var d in 灵植库.全部)
                {
                    if(!灵植库.可种(d,b.品阶)||state.数量(d.种子id)<1)continue;
                    done=farm.自动播种(i,d.id,id=>state.取出(id,1));break;
                }
            }
        }
    }
}
