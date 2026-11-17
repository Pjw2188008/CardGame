using System.Collections.Generic;
namespace Unknown.Battle
{
    // Presentation only. Full descriptions and effect definitions remain unchanged.
    public static class CardDisplayText
    {
        static readonly Dictionary<string,string> compact=new Dictionary<string,string> {
            {"Diamond_2","출전 · 피해\n적 1명 피해 1"},
            {"Diamond_3","공격 · 돌파\n이번 공격 피해 +1"},
            {"Diamond_4","출전 · 피해\n다친 적 1명 피해 2"},
            {"Diamond_5","처치 · 추격\n적 플레이어 피해 2"},
            {"Diamond_6","출전 · 성장\n적 1명 피해 2\n처치 시 자신 ATK +1"},
            {"Diamond_7","공격 · 돌파\n대상의 보호 무시"},
            {"Diamond_8","출전 · 강화\n아군 1명 ATK +2\n이번 턴만"},
            {"Diamond_9","처치 · 성장\n자신 HP 2 회복\n자신 ATK +1"},
            {"Diamond_10","출전 · 추격\n적 1명 피해 3\n처치 시 적 본체 피해 3"},
            {"Diamond_J","출전 · 강화\n아군 전체 ATK +2\n이번 턴만"},
            {"Diamond_Q","출전 · 성장\n적 1명 피해 4\n처치 시 아군 ATK +1"},
            {"Diamond_K","출전 · 피해\n적 1명 피해 5\n자신 보호 3"},
            {"Diamond_A","출전 · 돌파\n적 1명 피해 4\n다른 다이아 2장: +2"},
            {"Spade_2","출전 · 약화\n적 1명 ATK -1 · 2턴"},
            {"Spade_3","출전 · 약화\n적 1명 최대HP -1 (2턴)"},
            {"Spade_4","출전 · 약화\n적 1명 ATK -2 · 2턴"},
            {"Spade_5","출전 · 약화\n적 1명 최대HP -2 (2턴)"},
            {"Spade_6","공격 · 약화\n대상 ATK -1 · 2턴\n약화 후 전투"},
            {"Spade_7","출전 · 약화\n적 1명 ATK·최대HP -1\n3턴 지속"},
            {"Spade_8","출전 · 약화\n적 전체 ATK -1 (2턴)"},
            {"Spade_9","공격 · 돌파\n약화된 적에게 피해 +3"},
            {"Spade_10","출전 · 약화\n적 전체 최대HP -2 (2턴)"},
            {"Spade_J","출전 · 약화\n적 1명 ATK -3 · 3턴"},
            {"Spade_Q","출전 · 약화\n적 전체 ATK -2 (2턴)"},
            {"Spade_K","출전 · 약화\n적 1명 ATK -2\n최대HP -3 · 모두 3턴"},
            {"Spade_A","출전 · 약화\n적 전체 ATK·최대HP -1 (2턴)\n이미 약화된 적 1명 피해 3"},
            {"Heart_2","출전 · 회복\n아군 1명 HP 2 회복"},
            {"Heart_3","출전 · 보호\n아군 1명 보호 2"},
            {"Heart_4","턴 시작 · 회복\n자신 HP 1 회복"},
            {"Heart_5","출전 · 회복\n아군 1명 HP 3 회복"},
            {"Heart_6","출전 · 강화\n아군 1명 최대·현재HP +2"},
            {"Heart_7","공격 · 돌파\n자신 HP 최대: 피해 +2"},
            {"Heart_8","출전 · 회복\n아군 전체 HP 2 회복"},
            {"Heart_9","턴 시작 · 성장\nHP 최대: ATK +1 (최대2회)"},
            {"Heart_10","출전 · 보호\n아군 전체 보호 2"},
            {"Heart_J","출전 · 강화\n아군 전체 최대·현재HP +2"},
            {"Heart_Q","출전 · 회복\n아군 전체 HP 4 회복\n자신 보호: 회복 아군 수 (최대3)"},
            {"Heart_K","출전 · 보호\n아군 전체 보호 3\n자신 ATK +1"},
            {"Heart_A","출전 · 회복\n아군 전체 HP 3 회복\nHP 최대 아군 ATK +1 (이번 턴)"},
            {"Club_2","출전 · 드로우\n1장 뽑기"},
            {"Club_3","출전 · 마나\n현재 마나 +1"},
            {"Club_4","출전 · 교환\n손패 1장 버리고 2장 뽑기"},
            {"Club_5","출전 · 할인\n다음 배치 -1마나 (이번 턴)"},
            {"Club_6","출전 · 드로우\n2장 뽑기"},
            {"Club_7","출전 · 보급\n1장 뽑기 · 마나 +1"},
            {"Club_8","출전 · 할인\n다음 배치 -2마나 (이번 턴)"},
            {"Club_9","출전 · 교환\n손패 1장 버리고 3장 뽑기"},
            {"Club_10","출전 · 보급\n2장 뽑기\n다음 배치 비용 -1"},
            {"Club_J","출전 · 보급\n2장 뽑기 · 마나 +1"},
            {"Club_Q","출전 · 교환\n손패 0~2장 버리기\n버린 수 +2장 뽑기"},
            {"Club_K","출전 · 보급\n3장 뽑기\n다음 배치 비용 -2"},
            {"Club_A","출전 · 보급\n2장 뽑기 · 마나 +2"},
            {"Unknown_J","출전 · 돌파\n적 1명 피해 7\n자신 ATK +3"},
            {"Unknown_Q","출전 · 강화\n아군 전체 ATK +3\n최대·현재 HP +4"},
            {"Unknown_K","출전 · 약화\n적 1명 피해 10\n적 전체 ATK -2"}
        };
        public static string For(CardInstance card)
        {
            var d=card.Definition;
            string text=d.CardType==CardType.Joker ? "도박 · 유닛당 1회\n50%: ATK·최대 HP ×1.5\n실패: 대상 파괴"
                : d.Version==CardDatabase.DefaultVersion && compact.TryGetValue(d.CardID,out var shortText) ? shortText : d.Description;
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
