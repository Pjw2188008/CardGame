#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
namespace Unknown.Battle
{
    [Serializable]
    public sealed class BalanceDefinition
    {
        public string cardID,name,suit,rank,type,description,version;
        public int mana,attack,hp;
        public BalanceEffectDefinition[] effects;
        public static BalanceDefinition From(CardDefinition card) => new BalanceDefinition {
            cardID=card.CardID,name=card.CardName,suit=card.Suit.ToString(),rank=card.Rank.ToString(),type=card.CardType.ToString(),description=card.Description,version=card.Version.ToString(),
            mana=card.ManaCost,attack=card.Attack,hp=card.MaxHP,effects=card.EffectList.Select(e=>new BalanceEffectDefinition{kind=e.Kind.ToString(),target=e.Target.ToString(),amount=e.Amount,freshTarget=e.FreshTarget}).ToArray()};
    }
    [Serializable] public sealed class BalanceEffectDefinition {public string kind,target;public int amount;public bool freshTarget;}
    [Serializable]
    public sealed class BalanceCardReport
    {
        public BalanceDefinition definition;
        public BalanceCardStats statistics;
        public double usageWinRate,averageDamage,averageUseTurn,averageSurvival,damagePerMana,healingPerMana,drawsPerMana,survivalPerMana,receivedDamagePerMana,buffDamagePerMana,extraHPAbsorbedPerMana;
        public double jokerSuccessRate,averageJokerSuccessAttackGain,averageJokerSuccessHPGain,averageJokerFailureAttackLoss,averageJokerFailureHPLoss;
        public double jokerExpectedAttackDelta,jokerExpectedHPDelta;
        public string sampleStatus,manaEfficiencyStatus,damageReductionStatus="not_applicable",shieldStatus="not_applicable",survivalEffectStatus;
        public static BalanceCardReport From(BalanceCardStats row)
        {
            var definition=CardDatabase.Get(row.cardID,row.version);
            double mana=row.manaPaid>0 ? row.manaPaid : double.PositiveInfinity;
            return new BalanceCardReport {definition=BalanceDefinition.From(definition),statistics=row,usageWinRate=row.UsageWinRate,averageDamage=row.AverageDamage,
                averageUseTurn=row.uses==0 ? 0 : (double)row.useTurnSum/row.uses,averageSurvival=row.AverageSurvival,damagePerMana=row.DamagePerMana,
                healingPerMana=row.healing/mana,drawsPerMana=row.draws/mana,survivalPerMana=row.survivalTurnSum/mana,receivedDamagePerMana=row.damageReceived/mana,
                buffDamagePerMana=row.buffExtraDamage/mana,extraHPAbsorbedPerMana=row.extraHPAbsorbed/mana,
                jokerSuccessRate=row.jokerSuccesses+row.jokerFailures==0 ? 0 : (double)row.jokerSuccesses/(row.jokerSuccesses+row.jokerFailures),
                averageJokerSuccessAttackGain=row.jokerSuccesses==0 ? 0 : (double)row.jokerAttackGain/row.jokerSuccesses,averageJokerSuccessHPGain=row.jokerSuccesses==0 ? 0 : (double)row.jokerHPGain/row.jokerSuccesses,
                averageJokerFailureAttackLoss=row.jokerFailures==0 ? 0 : (double)row.jokerLostAttack/row.jokerFailures,averageJokerFailureHPLoss=row.jokerFailures==0 ? 0 : (double)row.jokerLostHP/row.jokerFailures,
                jokerExpectedAttackDelta=row.jokerSuccesses+row.jokerFailures==0 ? 0 : (double)(row.jokerAttackGain-row.jokerLostAttack)/(row.jokerSuccesses+row.jokerFailures),
                jokerExpectedHPDelta=row.jokerSuccesses+row.jokerFailures==0 ? 0 : (double)(row.jokerHPGain-row.jokerLostHP)/(row.jokerSuccesses+row.jokerFailures),
                sampleStatus=row.uses<(row.scope=="map" ? 30 : 200) ? "insufficient_sample" : "reviewable_observational_sample",
                manaEfficiencyStatus=row.manaPaid==0 ? "not_applicable_zero_paid_mana" : "available",
                survivalEffectStatus=definition.CardType==CardType.Joker || definition.EffectList.Any(e=>e.Kind==EffectKind.Heal || e.Kind==EffectKind.FirstAttackHeal || e.Kind==EffectKind.IncreaseMaxHP) ? "available" : "not_applicable"};
        }
    }
    [Serializable] public sealed class BalanceCandidate {public string cardID,category,metric,group;public long sample;public double value,reference;public string[] evidence;}
    [Serializable] public sealed class BalanceCandidates {public BalanceCandidate[] candidates;public string rule="Exploratory review only. At least 200 natural uses overall and 30 uses per map. No automatic card changes.";}
    [Serializable]
    public sealed class BalanceSummary
    {
        public string schemaVersion="2.0",state;
        public int attempted,completed,errors,unresolved,cancelled;
        public BalanceCardReport[] cards,maps,ai;
        public BalanceEffectStats[] effects;
        public string[] interpretation;
    }
    [Serializable]
    public sealed class BalanceMetadata
    {
        public string schemaVersion="2.0",createdUtc,unityVersion,cardDataSHA256,ruleDataSHA256,mapDataSHA256,rulesCodeSHA256,testerCodeSHA256,aiCodeSHA256,state;
        public BalanceTestSettings settings;
        public BalanceDefinition[] cards;
        public MapDefinition[] maps;
        public JokerEventDefinition[] jokerEvents;
        public bool pokerEnabled;
        public int startingHP,startingHand,handLimit,fieldSlots,startingMana,manaCap,unknownLimit;
        public bool shuffleDecks;
        public string scheduling="map=i%6, pair=(i/6)%16, first=pair/4, second=pair%4; swap first/second on odd i; seed=base+i*104729 (Int32 wrap)";
        public string[] metricDefinitions;
        public BalanceMatchResult[] matches;
    }
    public static class BalanceReport
    {
        static string Format(object value)
        {
            if(value==null)return "";
            if(value is string[] array)return string.Join("|",array);
            if(value is IFormattable number)return number.ToString(null,CultureInfo.InvariantCulture);
            return value.ToString();
        }
        static string Quote(object value) => "\""+Format(value).Replace("\"","\"\"")+"\"";
        static void Json(string path,object data) => File.WriteAllText(path,JsonUtility.ToJson(data,true),new UTF8Encoding(false));
        static FieldInfo[] Fields(Type type) => type.GetFields(BindingFlags.Public|BindingFlags.Instance).OrderBy(f=>f.Name,StringComparer.Ordinal).ToArray();
        static void Csv(string path,IEnumerable<string> headers,IEnumerable<object[]> rows)
        {
            using(var writer=new StreamWriter(path,false,new UTF8Encoding(true))) {
                writer.WriteLine(string.Join(",",headers.Select(h=>Quote(h))));
                foreach(var row in rows)writer.WriteLine(string.Join(",",row.Select(Quote)));
            }
        }
        static object[] CardValues(BalanceCardStats row)
        {
            var report=BalanceCardReport.From(row);var definition=report.definition;
            var values=new List<object>{definition.cardID,definition.name,definition.suit,definition.rank,definition.type,definition.mana,definition.attack,definition.hp,definition.description,string.Join("|",definition.effects.Select(e=>e.kind+":"+e.target+":"+e.amount))};
            values.AddRange(Fields(typeof(BalanceCardStats)).Select(f=>f.GetValue(row)));
            foreach(var field in Fields(typeof(BalanceCardReport)).Where(f=>f.Name!="definition" && f.Name!="statistics"))
                values.Add(field.Name.EndsWith("PerMana",StringComparison.Ordinal) && row.manaPaid==0 ? null : field.GetValue(report));
            return values.ToArray();
        }
        static void CardCsv(BalanceTestRun run,string name,string scope)
        {
            var header=new List<string>{"id","name","suit","rank","type","mana","baseAttack","baseHP","description","effects"};
            header.AddRange(Fields(typeof(BalanceCardStats)).Select(f=>f.Name));
            header.AddRange(Fields(typeof(BalanceCardReport)).Where(f=>f.Name!="definition" && f.Name!="statistics").Select(f=>f.Name));
            Csv(Path.Combine(run.DirectoryPath,name),header,run.Cards.Values.Where(c=>c.scope==scope).OrderBy(c=>c.cardID,StringComparer.Ordinal).ThenBy(c=>c.group,StringComparer.Ordinal).Select(CardValues));
        }
        public static string Hash(string text)
        {
            using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-","").ToLowerInvariant();
        }
        static double Metric(BalanceCardStats row,string metric)
        {
            double uses=Math.Max(1,row.uses);
            switch(metric) {
                case "damage_per_use":return row.AverageDamage;
                case "healing_per_use":return row.healing/uses;
                case "draws_per_use":return row.draws/uses;
                case "weakening_per_use":return row.attackDecrease/uses;
                case "extra_hp_per_use":return row.extraHP/uses;
                case "absorbed_hp_per_use":return row.extraHPAbsorbed/uses;
                default:return row.buffExtraDamage/uses;
            }
        }
        internal static bool SupportsMetric(CardDefinition card,string metric)
        {
            switch(metric) {
                case "damage_per_use":return card.Suit==Suit.Diamond || card.EffectList.Any(e=>e.Kind==EffectKind.Damage || e.Kind==EffectKind.ChainDamage || e.Kind==EffectKind.RandomDamage);
                case "healing_per_use":return card.EffectList.Any(e=>e.Kind==EffectKind.Heal || e.Kind==EffectKind.FirstAttackHeal);
                case "draws_per_use":return card.EffectList.Any(e=>e.Kind==EffectKind.DrawCard || e.Kind==EffectKind.Exchange);
                case "weakening_per_use":return card.EffectList.Any(e=>e.Kind==EffectKind.ChainWeaken || e.Kind==EffectKind.ModifyAttack && e.Amount<0);
                case "extra_hp_per_use":case "absorbed_hp_per_use":return card.EffectList.Any(e=>e.Kind==EffectKind.IncreaseMaxHP);
                default:return card.CardType==CardType.Joker || card.EffectList.Any(e=>e.Kind==EffectKind.ModifyAttack && e.Amount>0);
            }
        }
        public static BalanceCandidate[] Candidates(BalanceTestRun run)
        {
            var result=new List<BalanceCandidate>();var all=run.Cards.Values.Where(r=>r.scope=="all").ToArray();
            string[] metrics={"damage_per_use","healing_per_use","draws_per_use","weakening_per_use","buff_damage_per_use","extra_hp_per_use","absorbed_hp_per_use"};
            foreach(var row in all.Where(r=>r.uses>=200 && r.matchesUsed>=10)) {
                var definition=CardDatabase.Get(row.cardID,row.version);
                var peers=all.Where(r=>r.cardID!=row.cardID && r.uses>=200 && CardDatabase.Get(r.cardID,r.version).Suit==definition.Suit && CardDatabase.Get(r.cardID,r.version).CardType==definition.CardType && Math.Abs(CardDatabase.Get(r.cardID,r.version).ManaCost-definition.ManaCost)<=1).ToArray();
                foreach(string metric in metrics.Where(m=>SupportsMetric(definition,m))) {
                    var comparable=peers.Where(p=>SupportsMetric(CardDatabase.Get(p.cardID,p.version),metric)).ToArray();
                    if(comparable.Length<2)continue;
                    double baseline=comparable.Average(p=>Metric(p,metric)),value=Metric(row,metric);
                    if(baseline<.5)continue;
                    bool high=value>=baseline*1.75,low=value<=baseline*.5;
                    if(!high && !low)continue;
                    int agreement=run.Cards.Values.Count(r=>r.cardID==row.cardID && r.scope=="ai" && r.uses>=20 && (high ? Metric(r,metric)>=baseline*1.5 : Metric(r,metric)<=baseline*.65));
                    if(agreement<3)continue;
                    result.Add(new BalanceCandidate{cardID=row.cardID,category=high ? "possible_overperformance" : "possible_underperformance",metric=metric,group="same suit/type, supported effect role, mana +/-1",sample=row.uses,value=value,reference=baseline,evidence=new[]{"At least three AI profiles agree with the direction.","Compare role-specific units separately. Usage win rate is correlational.","Review attack, HP, mana, effect amount and trigger conditions manually."}});
                }
                foreach(var effect in run.Effects.Values.Where(e=>e.cardID==row.cardID && e.activations>=20 && e.kind!="Joker")) {
                    if(effect.conditionsMet<effect.activations*.2 || effect.zeroResults>=effect.applications*.8)
                        result.Add(new BalanceCandidate{cardID=row.cardID,category="effect_activation_review",metric=effect.kind,group=effect.target,sample=effect.activations,value=effect.actualAmount,reference=effect.applications,evidence=new[]{"Few fulfilled target conditions or at least 80% zero-result applications.","Skipped targets, source destruction and capped effects are logged separately.","This does not establish a description/rule mismatch."}});
                }
                var maps=run.Cards.Values.Where(r=>r.cardID==row.cardID && r.scope=="map" && r.uses>=30).ToArray();
                if(maps.Length>=3)foreach(var map in maps)foreach(string metric in metrics.Where(m=>SupportsMetric(definition,m))) {
                    double other=maps.Where(r=>r!=map).Average(r=>Metric(r,metric)),value=Metric(map,metric);
                    if(other>=.5 && (value>=other*2 || value<=other*.35))
                        result.Add(new BalanceCandidate{cardID=row.cardID,category="map_dependency_review",metric=metric,group=map.group,sample=map.uses,value=value,reference=other,evidence=new[]{"Observed map performance differs from other adequately sampled maps.","Inspect map stat grants and map-attributed additional damage alongside intrinsic effects.","AI choices and game length may confound the comparison."}});
                }
            }
            return result.OrderBy(r=>r.cardID,StringComparer.Ordinal).ThenBy(r=>r.category,StringComparer.Ordinal).ThenBy(r=>r.metric,StringComparer.Ordinal).ToArray();
        }
        public static void Save(BalanceTestRun run)
        {
            CardCsv(run,"card_statistics.csv","all");CardCsv(run,"card_map_statistics.csv","map");CardCsv(run,"card_ai_statistics.csv","ai");
            var effectFields=Fields(typeof(BalanceEffectStats));
            Csv(Path.Combine(run.DirectoryPath,"card_effect_statistics.csv"),effectFields.Select(f=>f.Name).Concat(new[]{"averageEffectAmount"}),run.Effects.Values.OrderBy(e=>e.cardID,StringComparer.Ordinal).ThenBy(e=>e.effectIndex).Select(e=>effectFields.Select(f=>f.GetValue(e)).Concat(new object[]{e.applications==0 ? 0 : e.actualAmount/e.applications}).ToArray()));
            var matchFields=Fields(typeof(BalanceMatchResult));Csv(Path.Combine(run.DirectoryPath,"match_results.csv"),matchFields.Select(f=>f.Name),run.Matches.Select(m=>matchFields.Select(f=>f.GetValue(m)).ToArray()));
            var definitions=CardDatabase.AllFor(run.Rules.CardVersion).OrderBy(c=>c.CardID,StringComparer.Ordinal).Select(BalanceDefinition.From).ToArray();
            var metadata=new BalanceMetadata{createdUtc=DateTime.UtcNow.ToString("O"),unityVersion=Application.unityVersion,state=run.State,settings=run.Settings,cards=definitions,maps=run.Rules.MapRules,jokerEvents=run.Rules.MapEvents,pokerEnabled=false,
                startingHP=run.Rules.StartingHP,startingHand=run.Rules.StartingHand,handLimit=run.Rules.HandLimit,fieldSlots=run.Rules.FieldSlots,startingMana=run.Rules.StartingMana,manaCap=run.Rules.ManaCap,unknownLimit=run.Rules.UnknownLimit,shuffleDecks=run.Rules.ShuffleDecks,matches=run.Matches.ToArray(),
                metricDefinitions=new[]{"All aggregate rows exclude error, unresolved and cancelled matches. Detailed logs retain their match IDs.","appearances counts admitted draws, grants and UNKNOWN creation, including redraws; overflow goes directly to discardedUnused.","opportunities counts decision points with a legal summon/gamble, once per instance at that decision; it is not a count of distinct turns.","matchesUsed is distinct games; playerMatchesUsed counts player-game exposures. usageWinRate=winsUsed/playerMatchesUsed.","Survival uses global alternating turns, from summon until death or match end. Censored survivors are marked separately.","Damage is capped to actual remaining HP; counterattacks are attributed to their real source. Self damage is separate.","attackDamage+effectDamage is damage directly dealt. buffExtraDamage is a separate attributed contribution and must not be added to game-wide dealt damage.","Attack grants use a no-grant counterfactual capped by victim HP, divided proportionally among positive grants; weakening uses the corresponding no-debuff counterfactual. These are attribution estimates, not independent causal effects.","Additional HP absorption uses a base-HP-first pool with proportional grant consumption. Healing does not replenish consumed grant pools. Map grants remain separate.","Effect applications count resolved targets, including skipped/cancelled attempts; activations deduplicate source instance and effect index. Zero results include capped healing, empty decks and absent targets.","No shield or direct damage-reduction mechanic exists in the current card database. These columns are not_applicable.","Joker success/failure records and stat vectors are separate units; average gain/loss derives from totals divided by each outcome count. Forced map gambles are included with zero paid mana.","Zero paid mana efficiency is not applicable, encoded by status and blank CSV cells. JSON numeric placeholder zero must be interpreted with the status.","AI policies see only existing recipient-filtered observations. They do not inspect hidden opponent hands or deck order, or score poker synergies.","Safety limits mark unresolved matches without awarding wins. No fatigue or sudden-death rule is introduced.","Candidate thresholds are screening heuristics, not validated balance scores; low-sample cards remain explicitly insufficient_sample.","Descriptions are exported verbatim. Semantic agreement between prose and mechanics requires the deterministic fixtures and manual review."}};
            metadata.cardDataSHA256=Hash(string.Join("\n",definitions.Select(d=>JsonUtility.ToJson(d))));
            metadata.mapDataSHA256=Hash(string.Join("\n",run.Rules.MapRules.Select(d=>JsonUtility.ToJson(d)))+string.Join("\n",run.Rules.MapEvents.Select(d=>JsonUtility.ToJson(d))));
            string runtime=Path.Combine(Application.dataPath,"UNKNOWN/Battle/Runtime");
            metadata.rulesCodeSHA256=Hash(string.Join("\n",new[]{"BattleEngine.cs","BattleEngine.NewEffects.cs","BattleProtocol.cs","CardData.cs","BattleCommands.cs","PokerSynergy.cs","SuitMapRules.cs","JokerMapRules.cs"}.Select(name=>File.ReadAllText(Path.Combine(runtime,name)))));
            metadata.aiCodeSHA256=Hash(string.Join("\n",new[]{"AiAgent.cs","AiObservation.cs","BalancePolicy.cs"}.Select(name=>File.ReadAllText(Path.Combine(runtime,name)))));
            metadata.testerCodeSHA256=Hash(string.Join("\n",new[]{"BalanceTelemetry.cs","BalancePolicy.cs","BalanceTestRun.cs","BalanceReport.cs"}.Select(name=>File.ReadAllText(Path.Combine(runtime,name)))));
            metadata.ruleDataSHA256=Hash(string.Join("|",new object[]{run.Rules.StartingHP,run.Rules.StartingHand,run.Rules.HandLimit,run.Rules.FieldSlots,run.Rules.StartingMana,run.Rules.ManaCap,run.Rules.UnknownLimit,run.Rules.ShuffleDecks,false})
                +string.Join("\n",run.Rules.MapRules.Select(d=>JsonUtility.ToJson(d)))+string.Join("\n",run.Rules.MapEvents.Select(d=>JsonUtility.ToJson(d))));
            Json(Path.Combine(run.DirectoryPath,"test_metadata.json"),metadata);
            Json(Path.Combine(run.DirectoryPath,"balance_summary.json"),new BalanceSummary{state=run.State,attempted=run.Matches.Count,completed=run.ValidMatches,errors=run.Matches.Count(m=>m.status=="error"),unresolved=run.Matches.Count(m=>m.status=="unresolved"),cancelled=run.Matches.Count(m=>m.status=="cancelled"),
                cards=run.Cards.Values.Where(r=>r.scope=="all").OrderBy(r=>r.cardID,StringComparer.Ordinal).Select(BalanceCardReport.From).ToArray(),maps=run.Cards.Values.Where(r=>r.scope=="map").OrderBy(r=>r.cardID,StringComparer.Ordinal).ThenBy(r=>r.group,StringComparer.Ordinal).Select(BalanceCardReport.From).ToArray(),ai=run.Cards.Values.Where(r=>r.scope=="ai").OrderBy(r=>r.cardID,StringComparer.Ordinal).ThenBy(r=>r.group,StringComparer.Ordinal).Select(BalanceCardReport.From).ToArray(),effects=run.Effects.Values.OrderBy(e=>e.cardID,StringComparer.Ordinal).ThenBy(e=>e.effectIndex).ToArray(),interpretation=metadata.metricDefinitions});
            Json(Path.Combine(run.DirectoryPath,"balance_candidates.json"),new BalanceCandidates{candidates=Candidates(run)});
        }
    }
}
#endif
