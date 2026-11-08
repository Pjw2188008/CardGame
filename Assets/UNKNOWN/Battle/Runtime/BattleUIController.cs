using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem.UI;

namespace Unknown.Battle
{
    /// <summary>Canvas hotseat UI. All game mutations are validated commands on BattleEngine.</summary>
    public sealed partial class BattleUIController : MonoBehaviour
    {
        [SerializeField] int fieldSlots = 5;
        [SerializeField] int seed;
        [SerializeField] bool revealHands;
        [SerializeField, Min(1)] float turnDuration = 40;
        [SerializeField] BattleMap localMap;
        readonly TurnClock clock = new TurnClock();
        CardAnimationController animations;
        IReadOnlyList<BattleEvent> queuedTurnEvents;
        readonly Dictionary<int, RectTransform> cardViews = new Dictionary<int, RectTransform>();
        readonly Image[][] manaPips = { new Image[10], new Image[10] };
        readonly Text[] manaLabels = new Text[2];
        Text timerText, winnerText;
        Image timerFill;
        Button pauseButton, newGameButton;
        GameObject resultOverlay;
        Button returnLobby;
        readonly Image[][] unknownPips = { new Image[3], new Image[3] };
        readonly Text[] unknownLabels = new Text[2];
        bool actionBusy, transitioning, paused, applicationPaused;
        bool IsPaused => online == null && (paused || applicationPaused || Time.timeScale <= 0);
        BattleEngine engine;
        Font font;
        Canvas canvas;
        RectTransform battlefield, ownBoard, enemyBoard, handContent, opponentHand;
        Text opponentInfo, ownInfo, turnText, log, detail, selectionInfo, notice, passText;
        Button endTurn, combineButton, directAttack, revealButton;
        GameObject passOverlay;
        int handID = -1, attackerID = -1, jokerTarget = -1, aceID = -1, faceID = -1;
        string feedback = "";
        int lastLogCount;
        float pulse;
        Image divider;
        readonly Color navy = new Color(0.035f, 0.055f, 0.085f);
        readonly Color panel = new Color(0.075f, 0.105f, 0.14f);
        readonly Color paper = new Color(0.91f, 0.89f, 0.83f);
        readonly Color gold = new Color(0.9f, 0.72f, 0.35f);
        readonly Color ready = new Color(0.22f, 0.8f, 0.65f);

        void Start()
        {
            font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Arial" }, 18);
            BuildCanvas(); if(tutorialRequested)StartTutorial(); else if (online == null) NewMatch(); else StartOnline();
        }
        void NewMatch()
        {
            if(aiMode)ResetAiMatch();
            if(pokerList!=null)pokerList.gameObject.SetActive(false);
            lastDropID=-1;toastUntil=0;
            StopAllCoroutines(); if (animations != null) animations.ResetAll();
            actionBusy = transitioning = paused = false; clock.Stop();
            pauseButton.GetComponentInChildren<Text>().text = "일시정지";
            engine = new BattleEngine(seed == 0 ? Environment.TickCount : seed, new BattleConfig { FieldSlots = Math.Max(1, fieldSlots), Map=localMap,MapRules=BattleMapSettings.LoadRules(), MapEvents=localMap==BattleMap.JokerGame ? JokerMapSettings.LoadRules() : JokerEventDefinition.Defaults() });
            presentedMapEvents.Clear();
            queuedTurnEvents=null; var initialDraws = HumanAiEvents(engine.DrainEvents());
            ResetHandViews(); PrepareHandPlayback(initialDraws);
            ClearSelection(); feedback = "패의 카드 → 빈 아군 슬롯: 소환 / 아군 유닛 → 적 유닛: 공격";
            passOverlay.SetActive(false); resultOverlay.SetActive(false); Refresh();
            StartCoroutine(InitialHandPresentation(initialDraws));
        }
        void BuildCanvas()
        {
            var canvasGO = new GameObject("UNKNOWN Battle Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);
            canvas = canvasGO.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 1100;
            var scaler = canvasGO.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight; scaler.matchWidthOrHeight = 0.5f;
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var events = new GameObject("Battle EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform, false);
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            var root = Box(canvas.transform, "Background", Vector2.zero, Vector2.one, navy);
            battlefield = Box(root, "Battlefield", Vector2.zero, Vector2.one, navy);
            interactionRoot = root;
            BuildAttackTarget();
            opponentInfo = Label(battlefield, "", new Vector2(0.22f, 0.957f), new Vector2(0.78f, 0.995f), 21, new Color(1, 0.66f, 0.61f));
            ownInfo = Label(battlefield, "", new Vector2(0.22f, 0.26f), new Vector2(0.78f, 0.298f), 21, new Color(0.57f, 0.86f, 0.9f));
            BuildMana(1, 0.932f, 0.951f); BuildMana(0, 0.235f, 0.255f);
            BuildUnknownProgress(0, 0.70f, 0.263f);
            opponentHand = Box(battlefield, "Opponent Hand", new Vector2(0.5f, 0.881f), new Vector2(0.5f, 0.881f), Color.clear);
            opponentHand.sizeDelta = new Vector2(680, 72);
            enemyBoard = Box(battlefield, "Enemy Five Slots", new Vector2(0.5f, 0.724f), new Vector2(0.5f, 0.724f), Color.clear);
            ownBoard = Box(battlefield, "Friendly Five Slots", new Vector2(0.5f, 0.395f), new Vector2(0.5f, 0.395f), Color.clear);
            enemyBoard.sizeDelta = ownBoard.sizeDelta = new Vector2(1060, 220);
            divider = Box(battlefield, "Field Divider", new Vector2(0.08f, 0.62f), new Vector2(0.92f, 0.622f), new Color(0.3f, 0.4f, 0.48f)).GetComponent<Image>();
            turnText = Label(battlefield, "", new Vector2(0.215f, 0.59f), new Vector2(0.335f, 0.62f), 21, gold);
            timerText = Label(battlefield, "40", new Vector2(0.225f, 0.543f), new Vector2(0.325f, 0.59f), 30, Color.white);
            var track = Box(battlefield, "Time gauge", new Vector2(0.225f, 0.531f), new Vector2(0.325f, 0.536f), panel);
            timerFill = Box(track, "Time remaining", Vector2.zero, Vector2.one, ready).GetComponent<Image>();
            endTurn = ActionButton(battlefield, "턴 종료", new Vector2(0.75f, 0.545f), new Vector2(0.87f, 0.595f), EndTurn);
            directAttack = ActionButton(battlefield, "상대 직접 공격", new Vector2(0.82f, 0.932f), new Vector2(0.98f, 0.957f), AttackPlayer);
            BuildCombinationSlots();
            combineButton = ActionButton(battlefield, "UNKNOWN 조합", new Vector2(0.42f, 0.502f), new Vector2(0.58f, 0.533f), Combine);
            combineButton.gameObject.AddComponent<CardHover>().Configure(ShowCombinationPreview,HideTooltip,false,false);
            selectionInfo = Label(battlefield, "", new Vector2(0.34f, 0.534f), new Vector2(0.66f, 0.56f), 16, gold);
            notice = Label(battlefield, "", new Vector2(0.29f, 0.30f), new Vector2(0.71f, 0.35f), 17, gold);
            log = Label(battlefield, "", new Vector2(0.015f, 0.025f), new Vector2(0.165f, 0.185f), 13, new Color(0.65f, 0.72f, 0.8f)); log.alignment = TextAnchor.LowerLeft;
            var handArea = Box(battlefield, "Hand Area", new Vector2(0.12f, 0.012f), new Vector2(0.88f, 0.225f), Color.clear);
            handContent = Box(handArea, "Hand Cards", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Color.clear);
            handContent.sizeDelta = new Vector2(1200, 210);
            var menu = Box(battlefield, "Battle Menu", new Vector2(0.01f, 0.65f), new Vector2(0.21f, 0.95f), panel);battleMenu=menu;
            ActionButton(battlefield, "메뉴", new Vector2(0.015f, 0.96f), new Vector2(0.09f, 0.993f), ToggleBattleMenu);
            pauseButton = ActionButton(menu, "일시정지", new Vector2(0.05f, 0.72f), new Vector2(0.95f, 0.95f), () => { paused=!paused; pauseButton.GetComponentInChildren<Text>().text=paused ? "계속하기" : "일시정지"; });
            revealButton = ActionButton(menu, "패 공개 디버그", new Vector2(0.05f, 0.48f), new Vector2(0.95f, 0.70f), () => { if(!Blocked()) { revealHands=!revealHands; Refresh(); } });
            newGameButton = ActionButton(menu, "새 게임", new Vector2(0.05f, 0.25f), new Vector2(0.95f, 0.47f), () => { if(!actionBusy && !transitioning) NewMatch(); });
            pauseButton.gameObject.SetActive(false);revealButton.gameObject.SetActive(false);newGameButton.gameObject.SetActive(false);
            ActionButton(menu,"게임으로 돌아가기",new Vector2(0.05f,0.69f),new Vector2(0.95f,0.94f),ToggleBattleMenu);
            ActionButton(menu,"설정",new Vector2(0.05f,0.38f),new Vector2(0.95f,0.63f),()=>GamePreferences.Instance.Open());
            ActionButton(menu, "타이틀로 돌아가기", new Vector2(0.05f, 0.07f), new Vector2(0.95f, 0.32f), ReturnToMenu);
            revealButton.gameObject.SetActive(false);newGameButton.gameObject.SetActive(false);pauseButton.gameObject.SetActive(false);
            menu.gameObject.SetActive(false);
            BuildTooltip();
            var overlay = Box(root, "Hotseat Privacy Screen", Vector2.zero, Vector2.one, new Color(0.025f, 0.04f, 0.065f, 1));
            passOverlay = overlay.gameObject;
            passText = Label(overlay, "", new Vector2(0.1f, 0.5f), new Vector2(0.9f, 0.7f), 32, Color.white);
            ActionButton(overlay, "준비 완료 · 턴 시작", new Vector2(0.33f, 0.36f), new Vector2(0.67f, 0.44f), ConfirmHandoff);
            var result = Box(root, "Winner screen", Vector2.zero, Vector2.one, new Color(0.025f, 0.04f, 0.065f, 0.98f));
            resultOverlay = result.gameObject;
            winnerText = Label(result, "", new Vector2(0.1f, 0.5f), new Vector2(0.9f, 0.7f), 38, gold);
            ActionButton(result, "메인 메뉴", new Vector2(0.33f, 0.36f), new Vector2(0.67f, 0.44f), ReturnToMenu);
            returnLobby = ActionButton(result, "대기실로 돌아가기", new Vector2(0.33f, 0.26f), new Vector2(0.67f, 0.33f), ReturnToLobby);
            returnLobby.gameObject.SetActive(online != null);
            if(aiMode)BuildAiResult(result);
            animations = gameObject.AddComponent<CardAnimationController>(); animations.Initialize(root, font, () => IsPaused);
            animations.DrawPresentation = PresentDraw;
            animations.DiscardPresentation=PresentDiscard;
            animations.MapPresentation = PresentMapEvent;
            animations.PokerPresentation = PresentPoker;
            BuildDeckMarkers(); BuildDragLayer();BuildHandTools();
        }

        void BuildMana(int row, float bottom, float top)
        {
            for (int i = 0; i < 10; i++)
                manaPips[row][i] = Box(battlefield, "Mana " + (i + 1), new Vector2(0.24f + i * 0.042f, bottom), new Vector2(0.275f + i * 0.042f, top), panel).GetComponent<Image>();
            manaLabels[row] = Label(battlefield, "", new Vector2(0.67f, bottom - 0.004f), new Vector2(0.89f, top + 0.004f), 17, new Color(0.4f, 0.7f, 1));
        }
        void RefreshMana()
        {
            for (int row = 0; row < 2; row++)
            {
                var player = engine.State.Players[row == 0 ? ViewPlayer : 1 - ViewPlayer];
                manaLabels[row].text = $"{player.CurrentMana} / {player.MaxMana} MP"+(player.CurrentMana>player.MaxMana ? " (임시 +"+(player.CurrentMana-player.MaxMana)+")" : "");
                for (int i = 0; i < 10; i++) manaPips[row][i].color = i < player.CurrentMana ? new Color(0.2f, 0.55f, 1)
                    : i < player.MaxMana ? new Color(0.07f, 0.18f, 0.32f) : new Color(0.24f, 0.27f, 0.3f);
            }
        }
        void BuildUnknownProgress(int row, float left, float bottom)
        {
            unknownLabels[row] = Label(battlefield, "", new Vector2(left, bottom), new Vector2(left + 0.19f, bottom + 0.037f), 19, new Color(0.84f, 0.68f, 1));
            for (int i = 0; i < 3; i++)
                unknownPips[row][i] = Box(battlefield, "UNKNOWN Progress " + row + " " + i,
                    new Vector2(left + 0.20f + i * 0.028f, bottom + 0.005f),
                    new Vector2(left + 0.222f + i * 0.028f, bottom + 0.032f), panel).GetComponent<Image>();
        }
        void RefreshUnknownProgress()
        {
            for (int row = 0; row < 1; row++)
            {
                int count = engine.Config.Map==BattleMap.JokerGame ? engine.State.Players[ViewPlayer].JokerSuccesses : engine.State.Players[ViewPlayer].UnknownProgress;
                unknownLabels[row].text = (engine.Config.Map==BattleMap.JokerGame ? "JOKER JACKPOT " : "내 UNKNOWN: ") + count + "/3";
                for (int i = 0; i < 3; i++)
                {
                    unknownPips[row][i].color = i < count ? new Color(0.65f, 0.27f, 0.94f) : new Color(0.13f, 0.1f, 0.2f);
                    var glow = unknownPips[row][i].GetComponent<Outline>();
                    if (count >= 3 && glow == null) { glow = unknownPips[row][i].gameObject.AddComponent<Outline>(); glow.effectColor = new Color(0.9f, 0.65f, 1); glow.effectDistance = new Vector2(3, -3); }
                    if (glow != null) glow.enabled = count >= 3;
                }
            }
        }
        void BindAnimations()
        {
            int active = ViewPlayer;
            animations.Bind(cardViews,
                (owner, slot) => SlotPoint(owner == active ? ownBoard : enemyBoard, slot),
                owner => animations.Point((owner == active ? ownInfo : opponentInfo).rectTransform),
                owner => animations.Point(deckMarkers[owner == active ? 0 : 1]),
                owner => animations.Point(owner == active ? handContent.parent as RectTransform : opponentHand),
                (owner, pip) => animations.Point(manaPips[owner == active ? 0 : 1][Mathf.Clamp(pip, 0, 9)].rectTransform));
        }
        Vector2 SlotPoint(RectTransform board, int slot)
        {
            if (slot < 0 || slot >= board.childCount) return animations.Point(board);
            return animations.Point(board.GetChild(slot) as RectTransform);
        }

        RectTransform Box(Transform parent, string name, Vector2 min, Vector2 max, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>(); rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            var image = go.GetComponent<Image>(); image.color = color; image.raycastTarget = color.a > 0;
            return rect;
        }
        Text Label(Transform parent, string value, Vector2 min, Vector2 max, int size, Color color)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>(); rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = new Vector2(4, 2); rect.offsetMax = new Vector2(-4, -2);
            var text = go.GetComponent<Text>(); text.font = font; text.text = value; text.fontSize = size; text.color = color; text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false; text.supportRichText = true; text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }
        Button ActionButton(Transform parent, string text, Vector2 min, Vector2 max, Action action)
        {
            var rect = Box(parent, text, min, max, new Color(0.16f, 0.23f, 0.29f));
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = rect.GetComponent<Image>(); button.onClick.AddListener(() => action());
            Label(rect, text, Vector2.zero, Vector2.one, 16, Color.white); return button;
        }
        void ClearChildren(Transform parent)
        {
            foreach (Transform child in parent) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        }
        void ClearSelection() { EndAttackDrag(); EndCardDrag(); HideTooltip(); combinationMode=false;discardMode=false;discardID=-1; handID = attackerID = jokerTarget = aceID = faceID = -1; }
        CardInstance SelectedHand => engine.State.Players[ViewPlayer].Hand.FirstOrDefault(c => c.InstanceID == handID);
        void Result(bool success, string message = "")
        {
            feedback = success ? message : engine.State.LastError;
            if (!success) {Toast(feedback);ReturnRejectedCard();}
            if (success) { handID = attackerID = jokerTarget = -1; }
            Refresh();
        }
        void RunCommand(Func<bool> command, string message)
        {
            if (Blocked()) return;
            actionBusy = true;
            bool success;
            try { success = command(); }
            catch { actionBusy = false; throw; }
            if (!success) { actionBusy = false; Result(false); return; }
            feedback = message; ClearSelection(); RefreshMana();RefreshPokerUI();
            StartCoroutine(ActionPresentation(engine.DrainEvents()));
        }
        IEnumerator ActionPresentation(IReadOnlyList<BattleEvent> entries)
        {
            entries=HumanAiEvents(entries);
            tutorial?.Observe(entries);
            if(tutorial!=null && entries.Any(e=>e.Kind==BattleEventKind.Summon || e.Kind==BattleEventKind.Attack || e.Kind==BattleEventKind.Discard || e.Kind==BattleEventKind.PokerChanged || e.Kind==BattleEventKind.JokerSuccess || e.Kind==BattleEventKind.JokerFailure || e.Kind==BattleEventKind.Combine))TutorialCue(!entries.Any(e=>e.Kind==BattleEventKind.JokerFailure));
            ObserveAttackResult(entries);
            actionBusy = true;
            try
            {
                var pending = entries;
                while (pending.Count > 0)
                {
                    PrepareHandPlayback(pending);
                    yield return animations.Play(pending);
                    // Expiry can resolve the remaining target selections while the first visual is playing.
                    pending = HumanAiEvents(engine.DrainEvents());
                }
            }
            finally { animations.ClearAction(); actionBusy = false; Refresh();if(aiMode)aiNextAction=Time.unscaledTime+.4f; }
            if (engine.State.IsOver) ShowWinner();
            else if (engine.State.TurnExpired) StartCoroutine(TransitionTurn(true));
        }
        void ConfirmHandoff()
        {
            if (actionBusy || transitioning) return;
            passOverlay.SetActive(false); StartCoroutine(BeginTurnPresentation());
        }
        IEnumerator BeginTurnPresentation()
        {
            transitioning = true;
            clock.Begin(Math.Max(1, turnDuration), Time.realtimeSinceStartupAsDouble);
            try
            {
                yield return animations.Banner(engine.State.ActivePlayer == 0 ? "YOUR TURN" : "OPPONENT TURN", gold, 0.5f);
                var draws = queuedTurnEvents ?? engine.DrainEvents(); queuedTurnEvents=null; PrepareHandPlayback(draws);
                ShowRefillFeedback(draws);
                yield return animations.Play(draws);
            }
            finally { animations.ClearAction(); transitioning = false; }
            clock.Begin(Math.Max(1, turnDuration), Time.realtimeSinceStartupAsDouble);
            if(aiMode)aiNextAction=Time.unscaledTime+.4f;
            Refresh();
        }
        IEnumerator TransitionTurn(bool timedOut)
        {
            if (transitioning || actionBusy || engine.State.IsOver) yield break;
            transitioning = true; clock.Stop();
            int actor = engine.State.ActivePlayer;
            bool success = timedOut ? engine.FinishExpiredTurn(actor) : engine.EndTurn(actor);
            if (!success) { transitioning = false; Result(false); yield break; }
            queuedTurnEvents=HumanAiEvents(engine.DrainEvents()); PrepareHandPlayback(queuedTurnEvents);
            yield return animations.Banner("TURN END", gold, 0.5f);
            ClearSelection(); feedback = "패에서 카드를 선택하거나 아군 유닛으로 공격하세요.";
            Refresh(); transitioning = false;
            if (!revealHands && !aiMode)
            {
                passText.text = $"플레이어 {engine.State.ActivePlayer + 1}에게 화면을 넘겨 주세요.\n패는 준비 완료 후 공개됩니다.";
                passOverlay.SetActive(true);
            }
            else yield return BeginTurnPresentation();
        }
        void ShowWinner()
        {
            if(tutorial!=null)return;
            EndAttackDrag(); EndCardDrag();
            clock.Stop();
            bool won = engine.State.Winner == ViewPlayer;
            winnerText.text = engine.State.VictoryReason==VictoryReason.JokerJackpot ? won ? "VICTORY — JOKER JACKPOT" : "DEFEAT — OPPONENT HIT JACKPOT" : engine.State.VictoryReason == VictoryReason.UnknownComplete
                ? won ? "VICTORY — UNKNOWN COMPLETE" : "DEFEAT — OPPONENT COMPLETED UNKNOWN"
                : won ? "VICTORY — OPPONENT HP ZERO" : "DEFEAT — YOUR HP ZERO";
            resultOverlay.SetActive(true);
            if(aiMode)ShowAiResult();
        }
        void SelectHand(CardInstance card)
        {
            if (Blocked() || card==null) return;
            if(discardMode) {DiscardSelected(card);return;}
            if(combinationMode) { SelectCombinationMaterial(card); return; }
            if (engine.NeedsTarget) { feedback = engine.TargetPrompt; Refresh(); return; }
            attackerID = jokerTarget = -1;
            handID = handID == card.InstanceID ? -1 : card.InstanceID;
            feedback = card.Definition.CardType == CardType.Joker ? "조커를 아군 카드에 드래그하세요" : "빈 아군 슬롯을 선택하세요";
            Toast(feedback); Refresh();
        }
        bool Blocked()
        {
            if(tutorial!=null && (tutorialSwitching || tutorialFinished || tutorial.Step==1))return true;
            if(MenuOpen || GamePreferences.IsOpen)return true;
            if (engine == null || engine.State.ActivePlayer != ViewPlayer) return true;
            if (online != null) return !online.CanAct || actionBusy || transitioning;
            // InputSystem may process a click before this component's Update; sample the clock here too.
            clock.Tick(Time.realtimeSinceStartupAsDouble, IsPaused || transitioning || passOverlay.activeSelf);
            return engine.State.IsOver || engine.State.TurnExpired || (clock.Duration > 0 && clock.Expired)
                || passOverlay.activeSelf || actionBusy || transitioning || IsPaused;
        }
        void ClickSlot(int owner, int slot)
        {
            if (Blocked() || combinationMode || discardMode) return;
            int active = ViewPlayer;
            if (engine.NeedsTarget) { Dispatch(BattleAction.Target, owner, slot, () => engine.SelectEffectTarget(active, owner, slot), "소환 효과 처리"); return; }
            var unit = engine.State.Players[owner].Board[slot];
            if (owner != active)
            {
                if (attackerID >= 0 && unit!=null) SendAttack(attackerID,unit.InstanceID);
                else { feedback = "먼저 공격 가능한 아군 유닛을 선택하세요."; Refresh(); }
                return;
            }
            var selected = SelectedHand;
            if (selected != null && selected.Definition.CardType == CardType.Joker) { Toast("조커를 아군 카드에 드래그하세요"); return; }
            if (unit == null && selected != null) { Dispatch(BattleAction.Summon, handID, slot, () => engine.Summon(active, handID, slot), "소환 완료"); return; }
            if (unit != null)
            {
                handID = -1;
                if (engine.CanAttack(active, unit)) { attackerID = attackerID == unit.InstanceID ? -1 : unit.InstanceID; feedback = "적 유닛 또는 직접 공격을 선택하세요."; }
                else { attackerID = -1; feedback = unit.HasAttacked ? "이미 공격했습니다." : "소환한 턴에는 공격할 수 없습니다."; }
                Refresh();
            }
        }
        void EndTurn()
        {
            if(tutorial!=null)return;
            if (Blocked()) return;
            if (engine.NeedsTarget) { feedback = engine.TargetPrompt; Refresh(); return; }
            if (online != null) online.Submit(BattleAction.EndTurn); else StartCoroutine(TransitionTurn(false));
        }
        void Combine()
        {
            if (!combinationMode || Blocked() || engine.NeedsTarget || engine.CombinationPreview(ViewPlayer, aceID, faceID)==null) return;
            int ace=aceID, face=faceID, actor=ViewPlayer;
            Dispatch(BattleAction.Combine, ace, face, () => engine.Combine(actor, ace, face), "UNKNOWN 생성");
        }
        void Cancel()
        {
            if (Blocked()) return;
            if (engine.NeedsTarget) feedback = "소환은 확정되었습니다. 강조된 대상을 선택해 효과를 완료하세요.";
            else { ClearSelection(); feedback = "선택을 취소했습니다."; }
            Refresh();
        }
        void AttackPlayer()
        {
            if (Blocked()) return;
            SendAttack(attackerID,-1);
        }

        void Refresh()
        {
            EndAttackDrag(); EndCardDrag(); HideTooltip(); cardViews.Clear();
            var state = engine.State; int active = ViewPlayer, other = 1 - active;
            var me = state.Players[active]; var enemy = state.Players[other];
            opponentInfo.text = $"상대 · 플레이어 {other + 1}     HP {enemy.HP} / {engine.Config.StartingHP}     덱 {enemy.DeckCount}     패 {enemy.Hand.Count}";
            if(aiMode)opponentInfo.text=$"{aiDifficulty.ToString().ToUpperInvariant()} AI     HP {enemy.HP} / {engine.Config.StartingHP}     덱 {enemy.DeckCount}     패 {enemy.Hand.Count}";
            ownInfo.text = $"나 · 플레이어 {active + 1}     HP {me.HP} / {engine.Config.StartingHP}     덱 {me.DeckCount}     패 {me.Hand.Count}/{engine.Config.HandLimit}";
            turnText.text = state.IsOver ? $"플레이어 {state.Winner + 1} 승리" : $"턴 {state.TurnNumber} · 플레이어 {state.ActivePlayer + 1}";
            if(aiMode && !state.IsOver)turnText.text=$"턴 {state.TurnNumber} · "+(state.ActivePlayer==0 ? "나" : "AI 생각 중");
            notice.text = engine.NeedsTarget && state.ActivePlayer==ViewPlayer ? engine.TargetPrompt : Time.unscaledTime < toastUntil ? toastText : "";
            RefreshCombinationSlots();
            RefreshMapUI();
            log.text = string.Join("\n", engine.VisibleLog(active).Reverse().Take(3).Reverse());
            if (lastLogCount != state.LogVersion) { pulse = 0.5f; lastLogCount = state.LogVersion; }
            RefreshBoard(ownBoard, active); RefreshBoard(enemyBoard, other);
            RefreshHands(me, enemy);
            RefreshMana(); RefreshUnknownProgress(); RefreshPokerUI(); BindAnimations(); RefreshControls();
        }
        void RefreshBoard(RectTransform board, int owner)
        {
            ClearChildren(board); int slots = engine.Config.FieldSlots;
            for (int i = 0; i < slots; i++)
            {
                int slot = i; var card = engine.State.Players[owner].Board[i];
                bool target = engine.State.ActivePlayer==ViewPlayer && engine.IsValidEffectTarget(owner, i);
                var rect = Box(board, "Slot " + (i + 1), new Vector2((float)i / slots + 0.004f, 0), new Vector2((float)(i + 1) / slots - 0.004f, 1), card == null ? panel : paper);
                rect.gameObject.AddComponent<CardDropZone>().Configure(this, CardDropKind.Unit, owner, slot);
                if (card == null)
                {
                    var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = rect.GetComponent<Image>(); button.onClick.AddListener(() => ClickSlot(owner, slot));
                    Label(rect, $"{i + 1}\n빈 슬롯", Vector2.zero, Vector2.one, 17, new Color(0.4f, 0.49f, 0.58f));
                    if (owner == ViewPlayer && SelectedHand != null && SelectedHand.Definition.CardType != CardType.Joker
                        && SelectedHand.Definition.ManaCost <= engine.State.Players[owner].CurrentMana)
                    { var outline = rect.gameObject.AddComponent<Outline>(); outline.effectColor = gold; outline.effectDistance = new Vector2(2, -2); }
                }
                else
                {
                    DrawCard(rect, card, target || card.InstanceID == attackerID || card.InstanceID == jokerTarget,
                        owner==ViewPlayer && !Blocked() && engine.CanAttack(card.Owner, card), () => ClickSlot(owner, slot), true);
                    cardViews[card.InstanceID] = rect;
                }
            }
        }
        void DrawCard(RectTransform rect, CardInstance card, bool selected, bool canAttack, Action click, bool onBoard)
        {
            var def = card.Definition;
            bool unknown = def.CardType == CardType.Unknown;
            if (unknown) rect.GetComponent<Image>().color = new Color(0.13f, 0.07f, 0.21f);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = rect.GetComponent<Image>(); button.onClick.AddListener(() => click());
            var outline = rect.gameObject.AddComponent<Outline>(); outline.effectDistance = new Vector2(3, -3);
            outline.effectColor = selected ? gold : canAttack ? ready : unknown ? new Color(0.68f, 0.34f, 0.95f) : new Color(0.2f, 0.26f, 0.32f); outline.useGraphicAlpha = false;
            Color ink = def.Suit == Suit.Diamond ? new Color(0.72f, 0.12f, 0.15f) : def.Suit == Suit.Heart ? new Color(0.8f, 0.22f, 0.46f) : def.Suit == Suit.Club ? new Color(0.12f, 0.44f, 0.3f) : new Color(0.29f, 0.17f, 0.4f);
            if (unknown) ink = new Color(0.85f, 0.73f, 1);
            if (!selected && !canAttack) outline.effectColor = unknown ? new Color(0.68f, 0.34f, 0.95f) : ink;
            var cost = Box(rect, "Mana cost", new Vector2(0.02f, 0.82f), new Vector2(0.2f, 0.99f), new Color(0.08f, 0.3f, 0.6f));
            Label(cost, def.ManaCost.ToString(), Vector2.zero, Vector2.one, 22, Color.white);
            Label(rect, def.CardType == CardType.Normal ? CardDatabase.RankText(def.Rank) : def.CardName, new Vector2(0.22f, 0.82f), new Vector2(0.98f, 0.99f), unknown ? 14 : 22, ink);
            Label(rect, unknown ? "UNKNOWN" : def.CardType == CardType.Joker ? "★" : CardDatabase.Symbol(def.Suit), new Vector2(0.05f, 0.48f), new Vector2(0.95f, 0.83f), unknown ? 22 : 46, ink);
            var description = Label(rect, def.Description, new Vector2(0.04f, 0.23f), new Vector2(0.96f, 0.49f), 14, unknown ? ink : new Color(0.15f, 0.18f, 0.2f));
            description.resizeTextForBestFit = true; description.resizeTextMinSize = 11; description.resizeTextMaxSize = 14;
            Label(rect, "", new Vector2(0.01f, 0.055f), new Vector2(0.34f, 0.24f), 26, ink).name="Stat ATK";
            Label(rect, "", new Vector2(0.55f, 0.055f), new Vector2(0.99f, 0.24f), 26, ink).name="Stat HP";
            UpdateCardStats(rect,card,false);
            var hover = rect.gameObject.AddComponent<CardHover>();
            hover.Configure(data => ShowTooltip(card, rect, data), HideTooltip, true, !onBoard);
            hover.CanZoom=()=>!AttackDragging && dragged==null;
            if(onBoard && card.Owner==ViewPlayer)
            {
                rect.gameObject.AddComponent<UnitAttackDrag>().Configure(this,card.InstanceID);
                var status=Label(rect,"",new Vector2(0.32f,0.01f),new Vector2(0.70f,0.12f),16,ready); status.name="Attack Status";
                var glowGO=new GameObject("Attack Ready Glow",typeof(RectTransform),typeof(CanvasRenderer),typeof(AttackGlowGraphic)); glowGO.transform.SetParent(rect,false);
                var glow=(RectTransform)glowGO.transform; glow.anchorMin=Vector2.zero; glow.anchorMax=Vector2.one; glow.offsetMin=glow.offsetMax=Vector2.zero;
                glowGO.GetComponent<AttackGlowGraphic>().raycastTarget=false;
            }
            if (!onBoard) rect.gameObject.AddComponent<CardDrag>().Configure(this, card.InstanceID);
            string state = !onBoard ? "" : card.JokerUsed ? "조커 사용됨" : "";
            Label(rect, state, new Vector2(0.01f, 0), new Vector2(0.99f, 0.11f), 10, new Color(0.23f, 0.31f, 0.32f));
        }
        void RefreshControls()
        {
            RefreshHandTools();
            bool allowed = !Blocked();
            endTurn.interactable = allowed && !engine.NeedsTarget;
            RefreshCombinationSlots();
            directAttack.interactable = allowed && !engine.NeedsTarget && attackerID >= 0 && engine.State.Players[1 - ViewPlayer].Board.All(c => c == null);
            revealButton.interactable = allowed;
            newGameButton.interactable = !actionBusy && !transitioning;
            RefreshAttackIndicators();
        }
        void Update()
        {
            if (divider == null || engine == null) return;
            UpdateInteractionUI();
            if (online != null) { UpdateOnline(); return; }
            if(tutorial!=null){TickTutorial();return;}
            bool held = IsPaused || transitioning || passOverlay.activeSelf;
            if (engine.State.IsOver) clock.Stop();
            else clock.Tick(Time.realtimeSinceStartupAsDouble, held);
            if (clock.Expired && !engine.State.IsOver && !engine.State.TurnExpired && clock.Duration > 0 && !transitioning && !passOverlay.activeSelf)
            {
                engine.ExpireTurn(engine.State.ActivePlayer);
                if (!actionBusy) StartCoroutine(ActionPresentation(engine.DrainEvents()));
            }
            timerText.text = IsPaused ? "PAUSED" : engine.State.TurnExpired || clock.Expired ? "TIME OUT" : Math.Ceiling(clock.Remaining).ToString("00") + "s";
            Color timeColor = TimerColor(clock.Remaining);
            if (clock.Remaining <= 5 && clock.Remaining > 0) timeColor.a = 0.4f + 0.6f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 7));
            timerText.color = timeColor; timerFill.color = timeColor;
            timerFill.rectTransform.anchorMax = new Vector2(clock.Duration > 0 ? (float)(clock.Remaining / clock.Duration) : 1, 1);
            RefreshControls();
            pulse = Mathf.Max(0, pulse - Time.unscaledDeltaTime);
            divider.color = Color.Lerp(new Color(0.3f, 0.4f, 0.48f), gold, pulse * 2);
            TickAi();
        }
        void OnApplicationPause(bool value) { applicationPaused = value; if(value)EndAttackDrag(); }
        static Color TimerColor(double seconds) => seconds<=5 ? new Color(1,0.24f,0.24f) : seconds<=10 ? new Color(1,0.62f,0.18f) : Color.white;
        void OnApplicationFocus(bool focused) { if(!focused)EndAttackDrag(); }
        void OnDestroy() { EndAttackDrag(); EndCardDrag(); StopOnline(); if(tutorialTone!=null)Destroy(tutorialTone); if (font != null) Destroy(font); }
    }
}
