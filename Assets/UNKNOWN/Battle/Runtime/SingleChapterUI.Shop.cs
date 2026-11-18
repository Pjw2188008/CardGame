using System;
using UnityEngine;

namespace Unknown.Battle
{
    public sealed partial class SingleChapterUI
    {
        void RunShopScreen(string message="")
        {
            var stock=Campaign.CurrentShop;Clear("카드 상점");
            Text("◆ 런 토큰 "+Campaign.Run.tokens+"    덱 "+Campaign.Run.deck.Count+"장",.15f,.80f,.70f,.055f,25);
            for(int i=0;i<stock.cards.Length;i++){
                int index=i;var id=stock.cards[i];bool purchased=stock.purchased[i];
                var card=CardPreview(root,id,.065f+i*.235f,.33f,.165f,.405f,()=>{},false,true);
                Hint(card.gameObject,CardDatabase.Get(id).CardName+"\n"+CardDatabase.Get(id).Description);
                Text("◆ "+stock.prices[i]+" 토큰",.05f+i*.235f,.265f,.195f,.045f,22);
                string reason=Campaign.ShopPurchaseError(i);
                Button(purchased?"구매 완료":reason.Length>0?reason:"구매",.05f+i*.235f,.195f,.195f,.055f,()=>{
                    string feedback;
                    try{feedback=Campaign.BuyShopCard(index)?"구매 완료 · "+CardDatabase.Get(id).CardName:Campaign.ShopPurchaseError(index);}
                    catch(Exception ex){feedback="구매 저장 실패: "+ex.Message;}
                    RunShopScreen(feedback);
                },reason.Length==0);
            }
            Text(message,.08f,.125f,.84f,.04f,18);
            Button("상점 나가기",.35f,.045f,.30f,.06f,()=>{Campaign.LeaveShop();Continue();});
        }
    }
}
