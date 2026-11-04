using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Unknown.Battle
{
    /// <summary>Plays immutable engine results. Never rolls dice, spends mana or applies combat rules.</summary>
    public sealed class CardAnimationController : MonoBehaviour
    {
        RectTransform layer;
        Font font;
        Func<bool> paused;
        Dictionary<int, RectTransform> views;
        readonly Dictionary<int, RectTransform> ghosts = new Dictionary<int, RectTransform>();
        readonly List<CanvasGroup> hidden = new List<CanvasGroup>();
        readonly Dictionary<int, int> textLanes = new Dictionary<int, int>();
        Func<int, int, Vector2> slotPoint, manaPoint;
        Func<int, Vector2> heroPoint, deckPoint, handPoint;
        Sprite ring;
        Texture2D ringTexture;
        int lastChainSource = -1;
        Vector2 lastChainPoint;
        readonly Color red = new Color(1, 0.25f, 0.25f), green = new Color(0.35f, 1, 0.57f), purple = new Color(0.73f, 0.4f, 1), gold = new Color(1, 0.82f, 0.3f);

        public void Initialize(RectTransform parent, Font uiFont, Func<bool> isPaused)
        {
            font = uiFont; paused = isPaused;
            var go = new GameObject("Battle FX", typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(parent, false); layer = go.GetComponent<RectTransform>();
            layer.anchorMin = Vector2.zero; layer.anchorMax = Vector2.one; layer.offsetMin = layer.offsetMax = Vector2.zero;
            var group = go.GetComponent<CanvasGroup>(); group.blocksRaycasts = false; group.interactable = false;
            ringTexture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            var pixels = new Color[64 * 64];
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
            { float distance = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f)); pixels[y * 64 + x] = new Color(1, 1, 1, Mathf.Clamp01(2 - Mathf.Abs(distance - 27))); }
            ringTexture.SetPixels(pixels); ringTexture.Apply(); ring = Sprite.Create(ringTexture, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f));
        }
        public Vector2 Point(RectTransform rect) => rect == null ? Vector2.zero : (Vector2)layer.InverseTransformPoint(rect.TransformPoint(rect.rect.center));
        public void Bind(Dictionary<int, RectTransform> cardViews, Func<int, int, Vector2> slot,
            Func<int, Vector2> hero, Func<int, Vector2> deck, Func<int, Vector2> hand, Func<int, int, Vector2> mana)
        { views = cardViews; slotPoint = slot; heroPoint = hero; deckPoint = deck; handPoint = hand; manaPoint = mana; }
        RectTransform View(int id) => ghosts.TryGetValue(id, out var ghost) && ghost != null ? ghost
            : views != null && views.TryGetValue(id, out var view) ? view : null;
        Vector2 Position(int id, int owner) => View(id) != null ? Point(View(id)) : handPoint(owner);
        CanvasGroup Group(RectTransform rect)
        { var group = rect.GetComponent<CanvasGroup>(); return group != null ? group : rect.gameObject.AddComponent<CanvasGroup>(); }

        RectTransform Clone(int id, int owner)
        {
            if (ghosts.TryGetValue(id, out var existing) && existing != null) return existing;
            var original = View(id);
            if (original == null) return CardBack(id, handPoint(owner));
            Vector2 position = Point(original), size = original.rect.size;
            var clone = Instantiate(original.gameObject, layer).GetComponent<RectTransform>();
            foreach(var script in clone.GetComponentsInChildren<CardHover>()) script.enabled=false;
            foreach(var script in clone.GetComponentsInChildren<CardDrag>()) script.enabled=false;
            foreach(var script in clone.GetComponentsInChildren<UnitAttackDrag>()) script.enabled=false;
            foreach(var script in clone.GetComponentsInChildren<HandCardMotion>()) script.enabled=false;
            clone.anchorMin = clone.anchorMax = clone.pivot = new Vector2(0.5f, 0.5f); clone.sizeDelta = size;
            clone.anchoredPosition = position; clone.localScale = Vector3.one;
            var cloneGroup = Group(clone); cloneGroup.alpha = 1; cloneGroup.blocksRaycasts = false; cloneGroup.interactable = false;
            var originalGroup = Group(original); originalGroup.alpha = 0; hidden.Add(originalGroup);
            ghosts[id] = clone; return clone;
        }
        RectTransform CardBack(int id, Vector2 point, string title = "?", Color? color = null)
        {
            if (ghosts.TryGetValue(id,out var prior) && prior!=null) Destroy(prior.gameObject);
            var rect = Shape("FX card", point, new Vector2(132, 170), color ?? new Color(0.15f, 0.2f, 0.31f));
            var text = MakeText(rect, title, 23, Color.white); text.rectTransform.sizeDelta = new Vector2(126, 160);
            ghosts[id] = rect; return rect;
        }
        internal int ActiveCardGhostCount
        {
            get { int count=0; if(layer!=null) foreach(Transform t in layer) if(t.name=="FX card") count++; return count; }
        }
        RectTransform Shape(string name, Vector2 point, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(layer, false);
            var rect = go.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size; rect.anchoredPosition = point;
            var image = go.GetComponent<Image>(); image.color = color; image.raycastTarget = false; return rect;
        }
        Text MakeText(Transform parent, string message, int size, Color color)
        {
            var go = new GameObject("FX text", typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>(); text.font = font; text.text = message; text.fontSize = size; text.color = color;
            text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false; text.horizontalOverflow = HorizontalWrapMode.Overflow;
            var rect = text.rectTransform; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f); rect.sizeDelta = new Vector2(300, 70);
            return text;
        }
        IEnumerator Step(float duration, Action<float> action)
        {
            float elapsed = 0;
            while (elapsed < duration)
            {
                if (!paused()) { elapsed += Time.unscaledDeltaTime; action(Mathf.Clamp01(elapsed / duration)); }
                yield return null;
            }
            action(1);
        }
        public IEnumerator Banner(string title, Color color, float duration,float height=110,int fontSize=36)
        {
            var bar = Shape("Turn banner", Vector2.zero, new Vector2(1000, height), new Color(0.025f, 0.04f, 0.07f, 0.94f));
            var text = MakeText(bar, title, fontSize, color); var group = Group(bar);
            yield return Step(duration, t => { group.alpha = Mathf.Sin(t * Mathf.PI); bar.localScale = Vector3.one * (0.94f + 0.06f * t); });
            Destroy(bar.gameObject);
        }
        public Func<BattleEvent, IEnumerator> DrawPresentation;
        public Func<BattleEvent, IEnumerator> MapPresentation;
        public IEnumerator Play(IReadOnlyList<BattleEvent> entries)
        {
            int lastHurt = -1;
            foreach (var entry in entries)
            {
                Vector2 target = Position(entry.TargetID, entry.Owner);
                var view = View(entry.TargetID);
                switch (entry.Kind)
                {
                    case BattleEventKind.MapEvent:
                        if(MapPresentation!=null)yield return MapPresentation(entry); break;
                    case BattleEventKind.CardGranted:
                        if(DrawPresentation!=null)yield return DrawPresentation(entry); break;
                    case BattleEventKind.HandReturn:
                        yield return Step(0.12f,_=>{}); break;
                    case BattleEventKind.HeroChanged:
                        Floating(-1,heroPoint(entry.Owner),entry.Amount.ToString(),red); break;
                    case BattleEventKind.ManaSpent:
                        for (int pip = entry.Secondary - entry.Amount; pip < entry.Secondary; pip++)
                            StartCoroutine(Pulse(manaPoint(entry.Owner, pip), new Color(0.3f, 0.65f, 1), 30, 0.3f));
                        break;
                    case BattleEventKind.Summon:
                        var summoned = Clone(entry.SourceID, entry.Owner); Vector2 from = summoned.anchoredPosition, to = slotPoint(entry.Owner, entry.Slot);
                        yield return Step(0.38f, t => { summoned.anchoredPosition = Vector2.Lerp(from, to, Smooth(t)); summoned.localScale = Vector3.one * (1 + 0.1f * Mathf.Sin(t * Mathf.PI)); });
                        StartCoroutine(Pulse(to, SuitColor(entry.Definition.Suit), 120, 0.3f)); break;
                    case BattleEventKind.Attack:
                        yield return Attack(entry); break;
                    case BattleEventKind.Damage:
                        Vector2 damageFrom = entry.Chain && lastChainSource == entry.SourceID ? lastChainPoint : Position(entry.SourceID, 1 - entry.Owner);
                        if (entry.SourceID != entry.TargetID) yield return Beam(damageFrom, target, SuitColor(entry.Suit), 0.12f);
                        StartCoroutine(Pulse(target, SuitColor(entry.Suit), 65 + entry.Amount * 5, 0.28f));
                        StartCoroutine(Flash(view, entry.Suit == Suit.Spade ? purple : red, 0.18f));
                        Floating(entry.TargetID, target, "-" + entry.Amount, red); lastHurt = entry.TargetID;
                        if (entry.Chain) { lastChainSource = entry.SourceID; lastChainPoint = target; }
                        yield return Step(0.07f, _ => { }); break;
                    case BattleEventKind.Heal:
                        if (entry.Suit == Suit.Heart && lastHurt >= 0 && entry.SourceID == entry.TargetID)
                            yield return Beam(Position(lastHurt, 1 - entry.Owner), target, new Color(1, 0.6f, 0.8f), 0.14f);
                        Floating(entry.TargetID, target, "+" + entry.Amount, green);
                        StartCoroutine(Flash(view, green, 0.2f)); StartCoroutine(Pulse(target, green, 110, 0.35f));
                        yield return Step(0.08f, _ => { }); break;
                    case BattleEventKind.AttackChanged:
                        Color attackColor = entry.Amount < 0 ? purple : gold;
                        Floating(entry.TargetID, target, (entry.Amount < 0 ? "↓ " : "↑ ") + "ATK " + entry.Amount.ToString("+0;-0;0"), attackColor);
                        StartCoroutine(Flash(view, attackColor, 0.22f)); yield return Step(0.08f, _ => { }); break;
                    case BattleEventKind.MaxHPChanged:
                        Floating(entry.TargetID, target, "HP " + entry.Amount.ToString("+0;-0;0"), entry.Amount<0 ? purple : green);
                        StartCoroutine(Pulse(target, green, 145, 0.32f));
                        if (view != null) { Vector3 scale = view.localScale; yield return Step(0.13f, t => view.localScale = scale * (1 + 0.07f * Mathf.Sin(t * Mathf.PI))); }
                        break;
                    case BattleEventKind.Draw:
                        if (DrawPresentation != null) { yield return DrawPresentation(entry); break; }
                        var drawn = CardBack(entry.TargetID, deckPoint(entry.Owner)); Vector2 drawTo = handPoint(entry.Owner);
                        drawn.sizeDelta = new Vector2(46,64);
                        yield return Step(0.18f, t => { drawn.anchoredPosition = Vector2.Lerp(deckPoint(entry.Owner), drawTo, Smooth(t)); drawn.localScale = Vector3.one * (0.6f + 0.4f * t); });
                        Destroy(drawn.gameObject); ghosts.Remove(entry.TargetID);
                        break;
                    case BattleEventKind.Discard:
                        var discarded = Clone(entry.TargetID, entry.Owner); var discardedGroup = Group(discarded);
                        yield return Step(0.2f, t => { discarded.localScale = Vector3.one * (1 - 0.95f * t); discardedGroup.alpha = 1 - t; }); break;
                    case BattleEventKind.Destroy:
                        if (view != null)
                        {
                            var dying = Clone(entry.TargetID, entry.Owner); var dyingGroup = Group(dying); Vector2 dyingPoint = dying.anchoredPosition;
                            StartCoroutine(Flash(dying, red, 0.16f));
                            yield return Step(0.4f, t => { dying.anchoredPosition = dyingPoint + new Vector2(Mathf.Sin(t * 60) * 7 * (1 - t), 0); dying.localScale = Vector3.one * (1 - 0.9f * t); dyingGroup.alpha = 1 - t; });
                        }
                        break;
                    case BattleEventKind.Combine:
                        yield return Combine(entry); break;
                    case BattleEventKind.JokerTravel:
                        var joker = Clone(entry.SourceID, entry.Owner); Vector2 jokerFrom = joker.anchoredPosition;
                        yield return Step(0.25f, t => { joker.anchoredPosition = Vector2.Lerp(jokerFrom, target, Smooth(t)); joker.localScale = Vector3.one * (1 - 0.65f * t); });
                        for (int i = 0; i < 3; i++) StartCoroutine(Orbit(target, purple, i * Mathf.PI * 2 / 3));
                        yield return Step(0.3f, _ => { }); Group(joker).alpha = 0; break;
                    case BattleEventKind.JokerSuccess:
                    case BattleEventKind.JokerFailure:
                        bool success = entry.Kind == BattleEventKind.JokerSuccess;
                        StartCoroutine(Pulse(target, success ? gold : red, 190, 0.4f));
                        Floating(entry.TargetID, target + new Vector2(0, 45), success ? "SUCCESS" : "FAILED", success ? gold : red, 28);
                        yield return Step(0.18f, _ => { }); break;
                    case BattleEventKind.TimeOut:
                        yield return Banner("TIME OUT", red, 0.3f); break;
                    case BattleEventKind.GameOver:
                        if (entry.Secondary == (int)VictoryReason.UnknownComplete) yield return UnknownComplete();
                        else yield return Banner($"PLAYER {entry.Owner + 1} WINS", gold, 0.7f);
                        break;
                }
            }
        }
        IEnumerator Attack(BattleEvent entry)
        {
            var attacker = Clone(entry.SourceID, entry.Owner); Vector2 origin = attacker.anchoredPosition;
            Vector2 target = entry.TargetID < 0 ? heroPoint(entry.TargetOwner) : Position(entry.TargetID, entry.TargetOwner);
            yield return Step(0.1f, t => attacker.localScale = Vector3.one * (1 + 0.1f * t));
            yield return Step(0.13f, t => attacker.anchoredPosition = Vector2.Lerp(origin, target, Smooth(t)));
            StartCoroutine(Pulse(target, red, 105 + entry.Amount * 4, 0.27f));
            Floating(entry.TargetID, target, "-" + entry.Amount, red);
            if (entry.TargetID >= 0) { Floating(entry.SourceID, origin, "-" + entry.Secondary, red); StartCoroutine(Shake(View(entry.TargetID))); }
            yield return Step(0.08f, _ => { });
            yield return Step(0.19f, t => { attacker.anchoredPosition = Vector2.Lerp(target, origin, Smooth(t)); attacker.localScale = Vector3.one * (1.1f - 0.1f * t); });
        }
        IEnumerator Combine(BattleEvent entry)
        {
            if (entry.Definition == null)
            {
                // Hidden combinations have no presentation for the other recipient.
                yield break;
            }
            var ace = Clone(entry.SourceID, entry.Owner); var face = Clone(entry.TargetID, entry.Owner);
            Vector2 a = ace.anchoredPosition, b = face.anchoredPosition;
            yield return Step(0.35f, t => { ace.anchoredPosition = Vector2.Lerp(a, new Vector2(-40, 0), Smooth(t)); face.anchoredPosition = Vector2.Lerp(b, new Vector2(40, 0), Smooth(t)); });
            yield return Step(0.18f, t => { ace.localScale = face.localScale = Vector3.one * (1 - t); });
            StartCoroutine(Pulse(Vector2.zero, purple, 260, 0.42f));
            var result = CardBack(entry.Secondary, Vector2.zero, entry.Definition.CardName + "\n" + entry.Definition.Attack + " / " + entry.Definition.MaxHP, new Color(0.15f, 0.08f, 0.25f));
            yield return Step(0.15f, t => result.localScale = Vector3.one * (0.1f + t * 1.1f));
            yield return Step(0.32f, t => { result.anchoredPosition = Vector2.Lerp(Vector2.zero, handPoint(entry.Owner), Smooth(t)); result.localScale = Vector3.one * (1.2f - t * 0.2f); });
        }
        IEnumerator UnknownComplete()
        {
            var cards = new RectTransform[3];
            for (int i = 0; i < 3; i++) cards[i] = CardBack(-1000-i, new Vector2((i-1)*180, 0), "UNKNOWN", new Color(0.15f, 0.06f, 0.25f));
            StartCoroutine(Pulse(Vector2.zero, purple, 680, 1.2f));
            yield return Step(0.55f, t => { for (int i = 0; i < 3; i++) cards[i].localScale = Vector3.one * (0.7f + 0.3f*t); });
            yield return Banner("UNKNOWN COMPLETE", purple, 1.1f);
        }
        IEnumerator Beam(Vector2 from, Vector2 to, Color color, float duration)
        {
            var light = Shape("Ability light", from, new Vector2(18, 18), color);
            yield return Step(duration, t => light.anchoredPosition = Vector2.Lerp(from, to, t)); Destroy(light.gameObject);
        }
        IEnumerator Pulse(Vector2 center, Color color, float size, float duration)
        {
            var wave = Shape("Impact ring", center, Vector2.one * size, color); wave.GetComponent<Image>().sprite = ring;
            yield return Step(duration, t => { wave.localScale = Vector3.one * (0.2f + t); wave.GetComponent<Image>().color = new Color(color.r, color.g, color.b, 1 - t); });
            Destroy(wave.gameObject);
        }
        IEnumerator Orbit(Vector2 center, Color color, float angle)
        {
            var light = Shape("Orbit light", center, new Vector2(10, 10), color);
            yield return Step(0.35f, t => light.anchoredPosition = center + new Vector2(Mathf.Cos(angle + t * 8), Mathf.Sin(angle + t * 8)) * 90);
            Destroy(light.gameObject);
        }
        IEnumerator Shake(RectTransform rect)
        {
            if (rect == null) yield break; Vector2 origin = rect.anchoredPosition;
            yield return Step(0.15f, t => { if (rect != null) rect.anchoredPosition = origin + new Vector2(Mathf.Sin(t * 70) * 7 * (1 - t), 0); });
            if (rect != null) rect.anchoredPosition = origin;
        }
        IEnumerator Flash(RectTransform rect, Color color, float duration)
        {
            if (rect == null) yield break; var image = rect.GetComponent<Image>(); if (image == null) yield break;
            Color original = image.color;
            yield return Step(duration, t => { if (image != null) image.color = Color.Lerp(original, color, Mathf.Sin(t * Mathf.PI) * 0.7f); });
            if (image != null) image.color = original;
        }
        void Floating(int id, Vector2 point, string message, Color color, int size = 24)
        {
            textLanes.TryGetValue(id, out int lane); textLanes[id] = lane + 1;
            var text = MakeText(layer, message, size, color); Vector2 start = point + new Vector2((lane % 2 == 0 ? -1 : 1) * 24, 20 + lane % 4 * 25);
            StartCoroutine(Float(text, start, color));
        }
        IEnumerator Float(Text text, Vector2 start, Color color)
        {
            yield return Step(0.8f, t => { text.rectTransform.anchoredPosition = start + Vector2.up * t * 65; text.color = new Color(color.r, color.g, color.b, 1 - t); });
            Destroy(text.gameObject);
        }
        static float Smooth(float t) => t * t * (3 - 2 * t);
        Color SuitColor(Suit suit) => suit == Suit.Diamond ? red : suit == Suit.Spade ? purple
            : suit == Suit.Heart ? new Color(1, 0.55f, 0.75f) : suit == Suit.Club ? green : purple;
        public void ClearAction()
        {
            foreach (var group in hidden) if (group != null) group.alpha = 1;
            foreach (var ghost in ghosts.Values) if (ghost != null) Destroy(ghost.gameObject);
            hidden.Clear(); ghosts.Clear();
        }
        public void ResetAll()
        {
            StopAllCoroutines(); ClearAction(); textLanes.Clear(); lastChainSource = -1;
            if (layer != null) foreach (Transform child in layer) Destroy(child.gameObject);
        }
        void OnDestroy() { if (ring != null) Destroy(ring); if (ringTexture != null) Destroy(ringTexture); }
    }
}
