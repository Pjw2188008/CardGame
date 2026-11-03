using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unknown.Multiplayer
{
    /// <summary>Atmospheric room menu. Connection state stays in RoomController.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(RoomController))]
    public sealed class RoomTestUI : MonoBehaviour
    {
        enum Page { Home, Online, Join, Lobby, Game }
        RoomController room;
        NetworkBattleController networkBattle;
        Unknown.Battle.BattleUIController battleUI;
        bool battleOnline;
        UIDocument document;
        PanelSettings panelSettings;
        Font koreanFont, titleFont;
        VisualElement content, errorBox;
        readonly Dictionary<Page, VisualElement> pages = new Dictionary<Page, VisualElement>();
        readonly List<Button> actions = new List<Button>();
        Label status, error, code, players, player1, player2, gameCode, waiting, busyMark;
        TextField input;
        Button create, start, gameLeave, copy;
        Page currentPage;
        bool pageInitialized;
        int transition;
        float busyTime;
        internal bool DisplaysTwoPlayers => players != null && players.text.Contains("2 / 2")
            && player2.text.Contains("참가자") && pages[Page.Lobby].resolvedStyle.display == DisplayStyle.Flex;

        void Start()
        {
            room = GetComponent<RoomController>();
            networkBattle = GetComponent<NetworkBattleController>(); networkBattle.Changed += Render; networkBattle.SnapshotReceived += OnBattleSnapshot;
            var uiObject = new GameObject("UNKNOWN Room UI");
            uiObject.transform.SetParent(transform, false);
            uiObject.layer = gameObject.layer;
            var template = Resources.Load<PanelSettings>("UnknownRoomPanel");
            if (template == null) throw new InvalidOperationException("Run UNKNOWN > Multiplayer > Install Room Test UI in Current Scene first.");
            panelSettings = Instantiate(template);
            panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panelSettings.referenceResolution = new Vector2Int(1280, 720);
            panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panelSettings.match = 0.5f;
            panelSettings.sortingOrder = 1000;
            document = uiObject.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            document.sortingOrder = 1000;
            Build(document.rootVisualElement);
            room.Changed += Render;
            Render();
        }

        void Build(VisualElement root)
        {
            koreanFont = Font.CreateDynamicFontFromOSFont(new[] { "Batang", "Malgun Gothic", "Apple SD Gothic Neo", "Noto Serif CJK KR", "Arial" }, 20);
            titleFont = Font.CreateDynamicFontFromOSFont(new[] { "Times New Roman", "Georgia", "Noto Serif", "Arial" }, 64);
            root.style.unityFontDefinition = FontDefinition.FromFont(koreanFont);
            root.AddToClassList("unknown-root");
            var stylesheet = Resources.Load<StyleSheet>("UnknownRoomStyle");
            if (stylesheet != null) root.styleSheets.Add(stylesheet);
            root.Add(new NightBackdrop());
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("room-scroll");
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            root.Add(scroll);
            // Center against the actual viewport, including when the window is resized.
            scroll.contentContainer.style.alignItems = Align.Center;
            scroll.contentContainer.style.justifyContent = Justify.Center;
            scroll.contentContainer.style.flexGrow = 1;
            scroll.contentContainer.style.width = Length.Percent(100);
            scroll.contentViewport.RegisterCallback<GeometryChangedEvent>(evt =>
                scroll.contentContainer.style.minHeight = evt.newRect.height);
            var shell = new VisualElement();
            shell.AddToClassList("menu-shell");
            scroll.Add(shell);
            var title = Text(shell, "UNKNOWN", "title");
            title.style.unityFontDefinition = FontDefinition.FromFont(titleFont);
            shell.Add(new Flourish());
            content = new VisualElement();
            content.AddToClassList("page-content");
            shell.Add(content);
            var home = MakePage(Page.Home);
            MenuButton(home, "온라인 대전", () => { ShowPage(Page.Online, true); return Task.CompletedTask; });
            MenuButton(home, "로컬 테스트", () => { LaunchBattle(false); return Task.CompletedTask; });
            MenuButton(home, "게임 종료", () => { Application.Quit(); return Task.CompletedTask; });
            home = MakePage(Page.Online);
            create = MenuButton(home, "방 만들기", () => room.CreateRoomAsync());
            MenuButton(home, "방 코드로 참가", () => { ShowPage(Page.Join, true); return Task.CompletedTask; });
            MenuButton(home, "돌아가기", () => { ShowPage(Page.Home, true); return Task.CompletedTask; }, true);
            var joinPage = MakePage(Page.Join);
            Text(joinPage, "방 코드", "page-hint");
            input = new TextField { maxLength = 16, name = "room-code-input" };
            input.AddToClassList("code-input");
            input.tooltip = "방 코드 · 영문 및 숫자";
            joinPage.Add(input);
            input.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter) return;
                _ = InvokeAsync(() => room.JoinRoomAsync(input.value));
                evt.StopPropagation();
            });
            MenuButton(joinPage, "참가하기", () => room.JoinRoomAsync(input.value));
            MenuButton(joinPage, "돌아가기", () => { ShowPage(Page.Home, true); return Task.CompletedTask; }, true);
            var lobby = MakePage(Page.Lobby);
            Text(lobby, "방 코드", "page-hint");
            code = Text(lobby, "", "room-code");
            copy = MenuButton(lobby, "방 코드 복사", () => { room.CopyCode(); return Task.CompletedTask; }, true);
            players = Text(lobby, "", "player-count");
            var seats = new VisualElement();
            seats.AddToClassList("seats");
            lobby.Add(seats);
            player1 = Seat(seats, "I");
            player2 = Seat(seats, "II");
            waiting = Text(lobby, "", "waiting-note");
            MenuButton(lobby, "준비 / 준비 취소", () => { networkBattle.ToggleReady(); return Task.CompletedTask; });
            start = MenuButton(lobby, "게임 시작", () => room.StartGameAsync());
            MenuButton(lobby, "방 나가기", () => room.LeaveRoomAsync(), true);
            var game = MakePage(Page.Game);
            Text(game, "게임 시작", "chapter-title");
            gameCode = Text(game, "", "quiet-note");
            gameLeave = MenuButton(game, "방 나가기", () => room.LeaveRoomAsync(), true);
            var feedback = new VisualElement();
            feedback.AddToClassList("feedback");
            shell.Add(feedback);
            busyMark = Text(feedback, "·  ◇  ·", "busy-mark");
            status = Text(feedback, "", "status");
            errorBox = new VisualElement();
            errorBox.AddToClassList("error-box");
            feedback.Add(errorBox);
            error = Text(errorBox, "", "error-message");
            var footer = new VisualElement();
            footer.AddToClassList("footer");
            root.Add(footer);
            Text(footer, "↑ ↓  선택     ENTER  확인     ESC  돌아가기", "key-hints");
            root.RegisterCallback<KeyDownEvent>(OnKeyDown);
            ShowPage(Page.Home, false);
        }

        VisualElement MakePage(Page page)
        {
            var section = new VisualElement();
            section.AddToClassList("menu-page");
            pages.Add(page, section);
            content.Add(section);
            return section;
        }
        static Label Text(VisualElement parent, string text, string className)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList(className);
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.alignSelf = Align.Center;
            parent.Add(label);
            return label;
        }
        Button MenuButton(VisualElement parent, string text, Func<Task> action, bool secondary = false)
        {
            var button = new Button(() => { _ = InvokeAsync(action); });
            button.AddToClassList("menu-action");
            if (secondary) button.AddToClassList("secondary-action");
            Text(button, "◇", "selection-mark");
            Text(button, text, "action-text");
            Text(button, "◇", "selection-mark");
            button.tooltip = text;
            parent.Add(button);
            actions.Add(button);
            button.RegisterCallback<PointerEnterEvent>(_ =>
            {
                if (button.enabledInHierarchy && !room.Busy) button.Focus();
            });
            return button;
        }
        static Label Seat(VisualElement parent, string numeral)
        {
            var seat = new VisualElement();
            seat.AddToClassList("seat");
            parent.Add(seat);
            Text(seat, numeral, "seat-numeral");
            return Text(seat, "", "seat-name");
        }
        void OnKeyDown(KeyDownEvent evt)
        {
            if (room.Busy) return;
            if (evt.keyCode == KeyCode.Escape && currentPage == Page.Join)
            {
                ShowPage(Page.Home, true); evt.StopPropagation(); return;
            }
            if (evt.keyCode != KeyCode.UpArrow && evt.keyCode != KeyCode.DownArrow) return;
            var target = evt.target as VisualElement;
            if (target == input || (target != null && input.Contains(target))) return;
            var available = actions.FindAll(button => button.enabledInHierarchy && pages[currentPage].Contains(button)
                && button.resolvedStyle.display != DisplayStyle.None);
            if (available.Count == 0) return;
            var focused = document.rootVisualElement.focusController.focusedElement;
            int index = available.FindIndex(button => button == focused);
            int next = index < 0 ? 0 : (index + (evt.keyCode == KeyCode.DownArrow ? 1 : -1) + available.Count) % available.Count;
            available[next].Focus();
            evt.StopPropagation();
        }
        void ShowPage(Page page, bool focus)
        {
            if (pageInitialized && currentPage == page) return;
            currentPage = page;
            int version = ++transition;
            if (!pageInitialized) { ActivatePage(page, false); pageInitialized = true; return; }
            content.AddToClassList("page-hidden");
            content.schedule.Execute(() =>
            {
                if (version != transition || document == null) return;
                ActivatePage(page, focus);
                content.RemoveFromClassList("page-hidden");
            }).StartingIn(140);
        }
        void ActivatePage(Page page, bool focus)
        {
            foreach (var entry in pages) entry.Value.style.display = entry.Key == page ? DisplayStyle.Flex : DisplayStyle.None;
            if (!focus) return;
            content.schedule.Execute(() =>
            {
                if (currentPage != page || room.Busy) return;
                if (page == Page.Join) input.Focus();
                else if (page == Page.Home) create.Focus();
                else if (page == Page.Lobby) { if (room.CanStart) start.Focus(); else copy.Focus(); }
                else gameLeave.Focus();
            }).StartingIn(30);
        }
        static async Task InvokeAsync(Func<Task> action)
        {
            try { await action(); } catch (Exception e) { Debug.LogException(e); }
        }
        void Render()
        {
            if (document == null) return;
            if (battleOnline && battleUI != null && room.Session != null && !room.HasStarted)
            { Destroy(battleUI.gameObject); battleUI = null; battleOnline = false; }
            var session = room.Session;
            if (session != null) ShowPage(room.HasStarted ? Page.Game : Page.Lobby, true);
            else if (currentPage == Page.Lobby || currentPage == Page.Game) ShowPage(Page.Home, true);
            foreach (var action in actions) action.SetEnabled(!room.Busy);
            input.SetEnabled(!room.Busy);
            start.SetEnabled(room.CanStart);
            busyMark.style.display = room.Busy ? DisplayStyle.Flex : DisplayStyle.None;
            status.text = room.Busy ? "잠시만 기다려 주세요…" : room.Status.Contains("복사") ? "초대 코드를 복사했습니다."
                : "";
            status.parent.style.display = room.Busy || !string.IsNullOrEmpty(status.text) || !string.IsNullOrEmpty(room.Error)
                ? DisplayStyle.Flex : DisplayStyle.None;
            error.text = room.Error;
            errorBox.style.display = string.IsNullOrEmpty(room.Error) ? DisplayStyle.None : DisplayStyle.Flex;
            document.rootVisualElement.style.display = battleUI != null ? DisplayStyle.None : DisplayStyle.Flex;
            if (session == null) return;
            code.text = session.Code;
            players.text = $"접속 인원   {session.PlayerCount} / 2";
            player1.text = "플레이어 1\n방장" + (session.IsHost ? " · 나" : "");
            player2.text = session.PlayerCount == 2 ? "플레이어 2\n참가자" + (!session.IsHost ? " · 나" : "") : "빈 자리\n기다리는 중";
            player1.text += networkBattle.HostReady ? "\n준비 완료" : "\n준비 전";
            if (session.PlayerCount == 2) player2.text += networkBattle.GuestReady ? "\n준비 완료" : "\n준비 전";
            player2.parent.EnableInClassList("seat-empty", session.PlayerCount < 2);
            start.style.display = session.IsHost ? DisplayStyle.Flex : DisplayStyle.None;
            waiting.text = session.PlayerCount < 2 ? "상대를 기다리는 중"
                : room.CanStart ? "시작 가능" : session.IsHost ? "두 플레이어 모두 준비해 주세요" : "방장이 시작하기를 기다립니다.";
            gameCode.text = $"{session.Code}    ·    {session.PlayerCount} / 2";
        }
        void Update()
        {
            if (busyMark == null || room == null || !room.Busy) return;
            busyTime += Time.unscaledDeltaTime;
            busyMark.style.opacity = 0.4f + 0.6f * (0.5f + 0.5f * Mathf.Sin(busyTime * 3f));
        }
        void OnBattleSnapshot(Unknown.Battle.BattleSnapshot snapshot) { if (battleUI == null) LaunchBattle(true); }
        void LaunchBattle(bool online)
        {
            battleOnline = online;
            var go = new GameObject(online ? "UNKNOWN Online Battle" : "UNKNOWN Local Battle");
            battleUI = go.AddComponent<Unknown.Battle.BattleUIController>();
            battleUI.ConfigureOnline(online ? networkBattle : null, () => { battleUI = null; Render(); });
            document.rootVisualElement.style.display = DisplayStyle.None;
        }
        void OnDestroy()
        {
            transition++;
            if (networkBattle != null) { networkBattle.Changed -= Render; networkBattle.SnapshotReceived -= OnBattleSnapshot; }
            if (battleUI != null) Destroy(battleUI.gameObject);
            if (room != null) room.Changed -= Render;
            if (document != null) Destroy(document.gameObject);
            if (panelSettings != null) Destroy(panelSettings);
            if (koreanFont != null) Destroy(koreanFont);
            if (titleFont != null) Destroy(titleFont);
        }
        sealed class Flourish : VisualElement
        {
            public Flourish()
            {
                AddToClassList("flourish");
                pickingMode = PickingMode.Ignore;
                generateVisualContent += context =>
                {
                    var p = context.painter2D;
                    float cx = contentRect.width / 2f, y = contentRect.height / 2f;
                    p.lineWidth = 1f;
                    p.strokeColor = new Color(0.7f, 0.78f, 0.82f, 0.7f);
                    Diamond(p, cx, y, 4);
                    foreach (int side in new[] { -1, 1 })
                    {
                        p.BeginPath(); p.MoveTo(new Vector2(cx + side * 15, y));
                        p.LineTo(new Vector2(cx + side * 120, y)); p.Stroke();
                        p.BeginPath(); p.MoveTo(new Vector2(cx + side * 30, y));
                        p.BezierCurveTo(new Vector2(cx + side * 68, y - 22), new Vector2(cx + side * 91, y + 15), new Vector2(cx + side * 134, y - 5));
                        p.Stroke(); Diamond(p, cx + side * 146, y - 5, 2);
                    }
                };
            }
        }
        static void Diamond(Painter2D p, float x, float y, float size)
        {
            p.BeginPath(); p.MoveTo(new Vector2(x, y - size));
            p.LineTo(new Vector2(x + size, y)); p.LineTo(new Vector2(x, y + size));
            p.LineTo(new Vector2(x - size, y)); p.ClosePath(); p.Stroke();
        }
        sealed class NightBackdrop : VisualElement
        {
            readonly IVisualElementScheduledItem animation;
            public NightBackdrop()
            {
                AddToClassList("night-backdrop"); pickingMode = PickingMode.Ignore;
                generateVisualContent += Draw;
                animation = schedule.Execute(MarkDirtyRepaint).Every(50);
                RegisterCallback<DetachFromPanelEvent>(_ => animation.Pause());
                RegisterCallback<AttachToPanelEvent>(_ => animation.Resume());
            }
            void Draw(MeshGenerationContext context)
            {
                float w = contentRect.width, h = contentRect.height, t = Time.unscaledTime;
                if (w <= 0 || h <= 0) return;
                var p = context.painter2D;
                for (int i = 0; i < 16; i++)
                {
                    float y = h * (0.58f + i * 0.026f);
                    p.fillColor = new Color(0.22f, 0.36f, 0.45f, 0.012f + i * 0.001f);
                    p.BeginPath(); p.MoveTo(new Vector2(0, h)); p.LineTo(new Vector2(0, y));
                    p.BezierCurveTo(new Vector2(w * 0.3f, y - h * 0.13f), new Vector2(w * 0.72f, y + h * 0.05f), new Vector2(w, y - h * 0.06f));
                    p.LineTo(new Vector2(w, h)); p.ClosePath(); p.Fill();
                }
                p.lineWidth = 1;
                p.strokeColor = new Color(0.38f, 0.48f, 0.57f, 0.1f);
                foreach (float x in new[] { w * 0.12f, w * 0.88f })
                {
                    p.BeginPath(); p.MoveTo(new Vector2(x - w * 0.065f, h));
                    p.LineTo(new Vector2(x - w * 0.065f, h * 0.39f));
                    p.BezierCurveTo(new Vector2(x - w * 0.065f, h * 0.22f), new Vector2(x, h * 0.2f), new Vector2(x, h * 0.11f));
                    p.BezierCurveTo(new Vector2(x, h * 0.2f), new Vector2(x + w * 0.065f, h * 0.22f), new Vector2(x + w * 0.065f, h * 0.39f));
                    p.LineTo(new Vector2(x + w * 0.065f, h)); p.Stroke();
                }
                for (int i = 0; i < 30; i++)
                {
                    float x = w * ((i * 0.618034f) % 1f) + Mathf.Sin(t * 0.13f + i * 2f) * 13f;
                    float y = h * (1f - Mathf.Repeat(i * 0.173f + t * (0.006f + i % 3 * 0.002f), 1f));
                    float a = (0.14f + 0.1f * Mathf.Sin(t * 0.6f + i)) * (Mathf.Abs(x - w / 2f) < w * 0.23f ? 0.25f : 1f);
                    p.strokeColor = new Color(0.73f, 0.85f, 0.91f, a);
                    Diamond(p, x, y, i % 4 == 0 ? 2f : 0.8f);
                }
            }
        }
    }
}
