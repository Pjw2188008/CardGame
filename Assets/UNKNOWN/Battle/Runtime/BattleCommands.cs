using System;
using System.Linq;
namespace Unknown.Battle
{
    /// <summary>Shared rule entry point. Attack targets are instance IDs, never client-provided damage.</summary>
    public static class BattleCommands
    {
        public static bool Execute(BattleEngine engine,int actor,BattleAction action,int first=0,int second=0)
        {
            if(engine==null || actor<0 || actor>1)return false;
            switch(action) {
                case BattleAction.Summon:return engine.Summon(actor,first,second);
                case BattleAction.Attack:
                    int slot=second==-1 ? -1 : Array.FindIndex(engine.State.Players[1-actor].Board.ToArray(),c=>c!=null && c.InstanceID==second);
                    if(second!=-1 && slot<0){engine.State.LastError="유효한 적 유닛을 선택하세요.";return false;}
                    return engine.Attack(actor,first,slot);
                case BattleAction.Target:return engine.SelectEffectTarget(actor,first,second);
                case BattleAction.Combine:return engine.Combine(actor,first,second);
                case BattleAction.Joker:return engine.Gamble(actor,first,second);
                case BattleAction.Discard:return engine.DiscardHand(actor,first);
                case BattleAction.EndTurn:return engine.EndTurn(actor);
                default:return false;
            }
        }
    }
}
