using UnityEngine;
using UnityEngine.UI;
namespace Unknown.Battle
{
    public sealed partial class BattleUIController
    {
        Button exchangeSkip;
        void BuildExchangeTools()
        {
            exchangeSkip=ActionButton(battlefield,"교환 건너뛰기",new Vector2(.67f,.50f),new Vector2(.82f,.535f),()=>ExchangeCard(-1));
            exchangeSkip.gameObject.SetActive(false);
        }
        void RefreshExchangeTools()
        {
            if(exchangeSkip==null)return;
            bool choosing=engine!=null && engine.NeedsExchange && engine.State.ActivePlayer==ViewPlayer;
            exchangeSkip.gameObject.SetActive(choosing);exchangeSkip.interactable=choosing && !Blocked();
        }
        void ExchangeCard(int id)
        {
            if(Blocked() || !engine.NeedsExchange)return;
            int actor=ViewPlayer;Dispatch(BattleAction.Exchange,id,0,()=>engine.ChooseExchange(actor,id),id<0 ? "교환을 건너뛰었습니다" : "손패 교환");
        }
    }
}
