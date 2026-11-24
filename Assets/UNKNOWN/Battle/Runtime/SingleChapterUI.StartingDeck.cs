using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Unknown.Battle
{
    public sealed partial class SingleChapterUI
    {
        readonly List<string> startingCards=new List<string>();
        bool automaticDeck;
        float deckScroll;
        string deckMessage="";
        static readonly string[] DeckConceptNames={"공격형","생존형","약화형","자원형","포커형","UNKNOWN형"};

        void ChooseStartingDeck()
        {
            startingCards.Clear();automaticDeck=false;deckScroll=1;deckMessage="";StartingDeckScreen();
        }
        void StartingDeckScreen()
        {
            Clear("시작 카드 선택");int count=Campaign.Data.startingDeckSize;
            Button("직접 선택",.06f,.81f,.20f,.055f,()=>{automaticDeck=false;StartingDeckScreen();});
            Button("콘셉트 자동 선택",.28f,.81f,.24f,.055f,()=>{automaticDeck=true;StartingDeckScreen();});
            Text("선택 "+startingCards.Count+" / "+count+"장 · 카드별 1장",.55f,.81f,.40f,.055f,22);
            if(automaticDeck)for(int i=0;i<DeckConceptNames.Length;i++){
                int concept=i;
                Button(DeckConceptNames[i],.045f+i*.154f,.735f,.145f,.055f,()=>{
                    var cards=SingleStartingDeck.Build(Campaign.CardPool(),count,(SingleDeckConcept)concept);
                    startingCards.Clear();startingCards.AddRange(cards);deckMessage=DeckConceptNames[concept]+" 선택 완료 · 카드를 눌러 수정할 수 있습니다.";
                    if(concept==5 && !cards.Any(id=>CardDatabase.Get(id).Rank==Rank.Ace))deckMessage="A 카드 미해금: 현재는 조합 준비용 자원 카드 위주로 구성합니다.";
                    StartingDeckScreen();
                });
            }
            else Text("카드를 눌러 선택하거나 취소하세요.",.06f,.735f,.60f,.045f,19);

            var list=Scroll();var viewport=(RectTransform)list.parent;
            viewport.anchorMin=new Vector2(.045f,.19f);viewport.anchorMax=new Vector2(.955f,.70f);viewport.offsetMin=viewport.offsetMax=Vector2.zero;
            var scroll=viewport.GetComponent<ScrollRect>();
            var pool=Campaign.CardPool().Select(id=>CardDatabase.Get(id)).OrderBy(c=>c.Suit).ThenBy(c=>c.ManaCost).ThenBy(c=>c.CardID,StringComparer.Ordinal).ToArray();
            const int columns=7;const float cardHeight=292,rowHeight=326;
            int rows=(pool.Length+columns-1)/columns;list.sizeDelta=new Vector2(0,rows*rowHeight+12);
            for(int i=0;i<pool.Length;i++){
                var card=pool[i];string id=card.CardID;bool picked=startingCards.Contains(id);
                var rect=CardPreview(list,id,(i%columns)/(float)columns+.008f,1,1f/columns-.016f,0,()=>{
                    if(startingCards.Contains(id)){startingCards.Remove(id);deckMessage="";}
                    else if(startingCards.Count<count){startingCards.Add(id);deckMessage="";}
                    else deckMessage=count+"장을 선택했습니다. 먼저 카드를 취소하세요.";
                    StartingDeckScreen();
                },true,true);
                rect.name=(picked?"✓ ":"")+card.CardName+"\n"+card.ManaCost+" MP";
                rect.pivot=new Vector2(.5f,1);rect.sizeDelta=new Vector2(0,cardHeight);rect.anchoredPosition=new Vector2(0,-(i/columns)*rowHeight-8);
                if(picked){var mark=Text("✓ 선택됨",0,-.09f,1,.08f,18,rect);mark.color=new Color(.45f,1f,.78f);}
                rect.GetComponent<CardArtLayout>()?.RefreshLayout();
            }
            Text(deckMessage,.05f,.125f,.90f,.045f,18);
            Button("돌아가기",.12f,.045f,.25f,.06f,Home);
            Button("선택 완료 · 시작",.58f,.045f,.30f,.06f,()=>{
                Campaign.StartSelected(difficulty,startingCards,Environment.TickCount);
                selected=-1;mapOffset=-1;Map();
            },startingCards.Count==count);
            Canvas.ForceUpdateCanvases();scroll.verticalNormalizedPosition=deckScroll;
            scroll.onValueChanged.AddListener(_=>deckScroll=scroll.verticalNormalizedPosition);
        }
    }
}
