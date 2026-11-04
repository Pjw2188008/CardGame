using System;
namespace Unknown.Battle
{
    [Serializable] public sealed class MapEventView
    {
        public int id,owner;
        public JokerEventKind kind;
        public string name,detail;
        public static MapEventView For(MapEventResult result,int recipient)
        {
            if(result==null)return null;
            bool hidden=result.Private && recipient!=result.Owner;
            return new MapEventView{id=result.ID,owner=result.Owner,kind=hidden ? JokerEventKind.None : result.Kind,
                name=hidden ? "맵 사건 발생" : result.Name, detail=hidden ? "상대에게 비공개 사건이 적용되었습니다." : result.Detail};
        }
        public MapEventResult ToResult() => new MapEventResult{ID=id,Owner=owner,Kind=kind,Name=name,Detail=detail};
    }
}
