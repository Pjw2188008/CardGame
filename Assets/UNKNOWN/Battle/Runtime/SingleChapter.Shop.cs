using System;
using System.Collections.Generic;
using System.Linq;

namespace Unknown.Battle
{
    [Serializable] public sealed class SingleShopSettings
    {
        public int minNodes=1,maxNodes=2;
        public int lowPrice=2,midPrice=3,facePrice=5,acePrice=7,jokerPrice=8;
        public int Price(CardDefinition card)
        {
            if(card.CardType==CardType.Joker)return jokerPrice;
            if(card.Rank==Rank.Ace)return acePrice;
            if(card.Rank>=Rank.Jack && card.Rank<=Rank.King)return facePrice;
            return (int)card.Rank<=6 ? lowPrice : midPrice;
        }
        public void Validate(int templates)
        {
            if(minNodes<1 || maxNodes<minNodes || maxNodes>2 || templates<maxNodes || new[]{lowPrice,midPrice,facePrice,acePrice,jokerPrice}.Any(p=>p<0))
                throw new InvalidOperationException("상점 설정이 올바르지 않습니다.");
        }
    }
    [Serializable] public sealed class SingleShopStock
    {
        public int node;
        public string[] cards;
        public int[] prices;
        public bool[] purchased;
    }
    public sealed partial class SingleCampaign
    {
        bool buyingShopCard;
        public SingleShopStock CurrentShop
        {
            get {
                if(Run==null || Run.phase!="shop" || Stage.kind!=SingleNodeKind.Shop)throw new InvalidOperationException("현재 상점에 있지 않습니다.");
                return ShopStock();
            }
        }
        SingleShopStock ShopStock()
        {
            if(Run.shops==null)Run.shops=new List<SingleShopStock>();
            var existing=Run.shops.FirstOrDefault(s=>s.node==Run.current);
            if(existing!=null)return existing;
            var random=new System.Random(unchecked(Run.seed ^ Run.current*104729 ^ 0x53484f50));
            var cards=CardPool().Where(id=>CardDatabase.Get(id).CardType!=CardType.Unknown).OrderBy(_=>random.Next()).ThenBy(id=>id,StringComparer.Ordinal).Take(4).ToArray();
            if(cards.Length!=4)throw new InvalidOperationException("판매 가능한 해금 카드가 4장보다 적습니다.");
            var stock=new SingleShopStock{node=Run.current,cards=cards,prices=cards.Select(id=>Data.shop.Price(CardDatabase.Get(id))).ToArray(),purchased=new bool[4]};
            Run.shops.Add(stock);return stock;
        }
        void OpenShop()
        {
            if(Stage.kind!=SingleNodeKind.Shop)throw new InvalidOperationException("상점 노드가 아닙니다.");
            ShopStock();Run.phase="shop";Save();
        }
        public string ShopPurchaseError(int index)
        {
            if(Run==null || Run.phase!="shop" || Stage.kind!=SingleNodeKind.Shop)return "현재 상점에 있지 않습니다.";
            if(buyingShopCard)return "구매 처리 중입니다.";
            var stock=CurrentShop;
            if(index<0 || index>=stock.cards.Length)return "상품을 선택하세요.";
            if(stock.purchased[index])return "구매한 상품입니다.";
            if(!CardPool().Contains(stock.cards[index]))return "해금되지 않은 카드입니다.";
            if(Run.tokens<stock.prices[index])return "토큰 부족";
            return "";
        }
        public bool BuyShopCard(int index)
        {
            if(ShopPurchaseError(index).Length>0)return false;
            var stock=CurrentShop;var run=Run;int tokens=run.tokens,deckCount=run.deck.Count;
            bool committed=false;buyingShopCard=true;
            try {
                run.tokens-=stock.prices[index];run.deck.Add(stock.cards[index]);stock.purchased[index]=true;
                // Keep all three changes in the existing single save transaction.
                SaveCommitted(()=>committed=true);return true;
            }catch {
                if(!committed){run.tokens=tokens;run.deck.RemoveAt(deckCount);stock.purchased[index]=false;}
                throw;
            }finally {buyingShopCard=false;}
        }
        public void LeaveShop()
        {
            if(Run==null || Run.phase!="shop" || Stage.kind!=SingleNodeKind.Shop)return;
            CompleteNode();
        }
    }
}
