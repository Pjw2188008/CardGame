#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Unknown.Battle
{
    public sealed partial class BattleUIController
    {
        int checkRecipient = -1;
        [Serializable] sealed class PvPReport
        {
            public bool completed, legacyNullMaterialized;
            public List<string> passed = new List<string>(), failed = new List<string>(), errors = new List<string>();
        }
        // Reproduces the former wire shape using Unity's actual serializer, rather than a substitute JSON library.
        [Serializable] sealed class LegacySlot { public string definition = ""; }
        [Serializable] sealed class LegacyBoard { public LegacySlot[] board; }
        public void RunPvPRegressionChecks() { Invoke(nameof(BeginPvPChecks),0.1f); }
        void BeginPvPChecks() { StopAllCoroutines(); StartCoroutine(PvPChecks()); }
        IEnumerator PvPChecks()
        {
            yield return null;
            StopCoroutineIfNeeded();
            var report = new PvPReport();
            Application.LogCallback listener = (m, stack, type) => { if (type == LogType.Error || type == LogType.Exception) report.errors.Add(m); };
            Application.logMessageReceived += listener;
            Action<bool,string> verify = (ok, name) => { if(ok) report.passed.Add(name); else report.failed.Add(name); };
            try
            {
                var legacy = JsonUtility.FromJson<LegacyBoard>(JsonUtility.ToJson(new LegacyBoard { board = new LegacySlot[5] }));
                report.legacyNullMaterialized = legacy.board.Any(c => c != null);
                var host = new BattleEngine(10, new BattleConfig { StartingHand = 0, StartingMana = 10, ManaCap = 10 });
                foreach (var p in host.State.Players) p.HandCards.Clear();
                foreach (int owner in new[]{0,1})
                {
                    var unit = host.NewCard(CardDatabase.Get(owner==0 ? "Diamond_2" : "Club_K"),owner);
                    unit.CurrentHP = owner==0 ? 2 : 5; unit.SummonedTurn=0; host.State.Players[owner].Slots[owner==0 ? 4 : 1]=unit;
                }
                host.DrainEvents();
                for (int recipient=0;recipient<2;recipient++)
                {
                    checkRecipient=recipient;
                    var wire=JsonUtility.FromJson<BattleSnapshot>(JsonUtility.ToJson(BattleSnapshot.For(host,recipient,1,60,false,Array.Empty<BattleEvent>())));
                    engine=BattleEngine.CreateView(wire); Refresh(); yield return null;
                    verify(ownBoard.childCount==5 && enemyBoard.childCount==5,"P"+recipient+" both fields contain 5 slots after Unity JSON roundtrip");
                    int ownSlot=recipient==0 ? 4 : 1, enemySlot=recipient==0 ? 1 : 4;
                    verify(ownBoard.GetChild(ownSlot).GetComponentsInChildren<UnityEngine.UI.Text>().Any(t=>t.text.Contains(recipient==0 ? "♦" : "♣")),"P"+recipient+" own card appears below");
                    verify(enemyBoard.GetChild(enemySlot).GetComponentsInChildren<UnityEngine.UI.Text>().Any(t=>t.text.Contains(recipient==0 ? "♣" : "♦")),"P"+recipient+" opponent card appears above");
                    verify(opponentInfo.text.Contains("플레이어 "+(2-recipient)) && ownInfo.text.Contains("플레이어 "+(recipient+1)),"P"+recipient+" labels map to snapshot recipient");
                    verify(manaLabels[0].text.Contains("10 / 10") && manaLabels[1].text.Contains("10 / 10"),"P"+recipient+" both mana rows visible");
                }
                // Three successful combinations, with prior results removed to prove cumulative counting.
                for(int i=0;i<3;i++)
                {
                    host.State.Players[0].HandCards.Clear();
                    var a=host.NewCard(CardDatabase.Get("Heart_A"),0); var f=host.NewCard(CardDatabase.Get(new[]{"Spade_J","Spade_Q","Spade_K"}[i]),0);
                    host.State.Players[0].HandCards.Add(a); host.State.Players[0].HandCards.Add(f);
                    verify(host.Combine(0,a.InstanceID,f.InstanceID),"Combination "+(i+1)+" succeeds");
                }
                verify(host.State.IsOver && host.State.Winner==0 && host.State.VictoryReason==VictoryReason.UnknownComplete,"Third combine ends before animation");
                var entries=host.DrainEvents();
                for(int recipient=0;recipient<2;recipient++)
                {
                    checkRecipient=recipient; animations.ResetAll(); resultOverlay.SetActive(false);
                    var wire=JsonUtility.FromJson<BattleSnapshot>(JsonUtility.ToJson(BattleSnapshot.For(host,recipient,3,60,false,entries)));
                    engine=BattleEngine.CreateView(wire); Refresh();
                    verify(unknownLabels[0].text.Contains(recipient==0 ? "3/3" : "0/3") && unknownLabels[1]==null,"P"+recipient+" only own progress visible");
                    yield return animations.Play(wire.events.Select(e=>e.ToEvent()).ToArray()); animations.ClearAction(); ShowWinner();
                    verify(winnerText.text==(recipient==0 ? "VICTORY — UNKNOWN COMPLETE" : "DEFEAT — OPPONENT COMPLETED UNKNOWN"),"P"+recipient+" correct result text");
                    verify(unknownPips[0].All(p=>(p.GetComponent<UnityEngine.UI.Outline>()?.enabled==true)==(recipient==0)),"P"+recipient+" progress glow is private");
                    verify(!endTurn.interactable && !combineButton.interactable,"P"+recipient+" terminal controls disabled");
                }
                report.completed=true;
            }
            finally
            {
                Application.logMessageReceived-=listener; checkRecipient=-1;
                File.WriteAllText("Library/UNKNOWN-PvP-Regressions.json",JsonUtility.ToJson(report,true));
                Debug.Log("[UNKNOWN] PvP regression checks: passed="+report.passed.Count+", failed="+report.failed.Count+", errors="+report.errors.Count);
                Destroy(gameObject);
            }
        }
        void StopCoroutineIfNeeded()
        {
            // This dedicated check object has only its initial turn intro running alongside this coroutine.
            animations.ResetAll(); actionBusy=transitioning=paused=false; clock.Stop();
            passOverlay.SetActive(false); resultOverlay.SetActive(false);
        }
    }
}
#endif
