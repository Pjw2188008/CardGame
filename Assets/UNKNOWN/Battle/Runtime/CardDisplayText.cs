using System.Collections.Generic;
namespace Unknown.Battle
{
    // Presentation only. Full descriptions and effect definitions remain unchanged.
    public static class CardDisplayText
    {
        static readonly Dictionary<string,string> compact=new Dictionary<string,string> {
            {"Diamond_2", "출전\n적 1명 피해 2"},
            {"Diamond_3", "출전\n적 1명 피해 3"},
            {"Diamond_4", "출전\n적 1명 피해 3"},
            {"Diamond_5", "출전\n아군 1명 ATK +2"},
            {"Diamond_6", "출전\n적 1명 피해 5"},
            {"Diamond_7", "출전\n적 1명 보호 제거 후 피해 2"},
            {"Diamond_8", "출전\n아군 전체 ATK +1"},
            {"Diamond_9", "출전\n적 1명 피해 6"},
            {"Diamond_10", "출전\n적 전체 피해 3"},
            {"Diamond_J", "출전\n아군 전체 ATK +2"},
            {"Diamond_Q", "출전\n적 전체 피해 4"},
            {"Diamond_K", "출전\n적 1명 피해 8\n아군 1명 ATK +2"},
            {"Diamond_A", "출전\n적 전체 피해 3\n아군 전체 ATK +1"},
            {"Spade_2", "출전\n적 1명 ATK -1 (2턴)"},
            {"Spade_3", "출전\n적 1명 최대 HP -1 (2턴)"},
            {"Spade_4", "출전\n적 1명 ATK -2 (2턴)"},
            {"Spade_5", "출전\n적 1명 최대 HP -2 (2턴)"},
            {"Spade_6", "출전\n적 1명 ATK -1·최대HP -1 (2턴)"},
            {"Spade_7", "출전\n적 1명 ATK -2 (3턴)"},
            {"Spade_8", "출전\n적 전체 ATK -1 (2턴)"},
            {"Spade_9", "출전\n적 1명 최대 HP -3 (2턴)"},
            {"Spade_10", "출전\n적 전체 최대 HP -1 (2턴)"},
            {"Spade_J", "출전\n적 1명 ATK -3 (3턴)"},
            {"Spade_Q", "출전\n적 전체 ATK -2 (3턴)"},
            {"Spade_K", "출전\n적 1명 ATK -3·최대HP -3 (3턴)"},
            {"Spade_A", "출전\n적 전체 ATK -1·최대HP -2 (3턴)"},
            {"Heart_2", "출전\n아군 1명 HP 2 회복"},
            {"Heart_3", "출전\n아군 1명 보호 2"},
            {"Heart_4", "출전\n아군 1명 HP 3 회복"},
            {"Heart_5", "출전\n아군 1명 최대·현재HP +2"},
            {"Heart_6", "출전\n아군 전체 HP 2 회복"},
            {"Heart_7", "출전\n아군 1명 보호 3"},
            {"Heart_8", "출전\n아군 전체 최대·현재HP +1"},
            {"Heart_9", "출전\n아군 전체 HP 3 회복"},
            {"Heart_10", "출전\n아군 전체 보호 2"},
            {"Heart_J", "출전\n아군 전체 최대·현재HP +2"},
            {"Heart_Q", "출전\n아군 전체 HP 4 회복\n보호 2"},
            {"Heart_K", "출전\n아군 전체 보호 3, 최대·현재 HP +2"},
            {"Heart_A", "출전\n아군 전체 최대·현재HP +2\n보호 1"},
            {"Club_2", "출전\n1장 뽑기"},
            {"Club_3", "출전\n마나 +1"},
            {"Club_4", "출전\n손패 1장 버리고 2장 뽑기"},
            {"Club_5", "출전\n마나 +1"},
            {"Club_6", "출전\n2장 뽑기"},
            {"Club_7", "출전\n1장 뽑기, 마나 +1"},
            {"Club_8", "출전\n2장 뽑기"},
            {"Club_9", "출전\n손패 1장 버리고 3장 뽑기"},
            {"Club_10", "출전\n2장 뽑기, 마나 +1"},
            {"Club_J", "출전\n3장 뽑기"},
            {"Club_Q", "출전\n손패 최대 2장 교환, 3장 뽑기"},
            {"Club_K", "출전\n3장 뽑기, 마나 +2"},
            {"Club_A", "출전\n2장 뽑기, 마나 +2"},
            {"Unknown_J","출전 · 돌파\n적 1명 피해 7\n자신 ATK +3"},
            {"Unknown_Q","출전 · 강화\n아군 전체 ATK +3\n최대·현재 HP +4"},
            {"Unknown_K","출전 · 약화\n적 1명 피해 10\n적 전체 ATK -2"}
        };
        public static string For(CardInstance card)
        {
            var d=card.Definition;
            string text=d.CardType==CardType.Joker ? "도박 · 유닛당 1회\n50%: ATK·최대 HP ×1.5\n실패: 대상 파괴"
                : d.Version==CardDatabase.DefaultVersion && compact.TryGetValue(d.CardID,out var shortText) ? shortText : d.Description;
            // Placement timing is implicit on cards; reserve the whole area for the effect.
            if(text.StartsWith("출전",System.StringComparison.Ordinal)){
                int firstLine=text.IndexOf('\n');
                return firstLine>=0 ? text.Substring(firstLine+1) : text.Substring(2).TrimStart(' ',':','·');
            }
            int split=text.IndexOf('\n');
            if(split>=0){
                string heading=text.Substring(0,split),body=text.Substring(split+1);
                // Complex abilities use the trigger inline instead of spending a whole line on a heading.
                if(body.IndexOf('\n')>=0){
                    int separator=heading.IndexOf(" · ");
                    string trigger=separator>=0 ? heading.Substring(0,separator) : heading;
                    if(d.CardType==CardType.Joker)trigger=heading;
                    text="<b>"+trigger+"</b>: "+body;
                }else text="<b>"+heading+"</b>\n"+body;
            }
            // Current ATK/HP remain visible on the stat badges; keep ability text stable.
            return text;
        }
    }
}
