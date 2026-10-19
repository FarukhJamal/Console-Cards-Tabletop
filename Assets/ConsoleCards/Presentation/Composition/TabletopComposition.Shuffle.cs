using System;
using System.Collections.Generic;
using ConsoleCards.Application.Results;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Presentation.Interaction;
using ConsoleCards.Presentation.Settings;
using ConsoleCards.Presentation.UI;
using ConsoleCards.Presentation.Views;
using UnityEngine;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>How a deck or stack is shuffled (SHUF-1). Only the animation and sound differ; Cut cuts once.</summary>
    public enum ShuffleStyle
    {
        Riffle = 0,
        Overhand = 1,
        Hindu = 2,
        Pile = 3,
        Wash = 4,
        Cut = 5
    }

    /// <summary>A show-off move with a deck or stack (SHUF-1); the order never changes.</summary>
    public enum CardFlourish
    {
        Fan = 0,
        Spring = 1,
        RibbonSpread = 2,
        Cascade = 3,
        SwivelCut = 4
    }

    /// <summary>
    /// SHUF-1 (doc 30 Part H): shuffle styles and flourishes. The deck menu has "Shuffle (your style)", "Shuffle as..."
    /// (pick a style once, or set your default) and "Flourish...". Your default style is kept per profile (doc 28).
    /// The new order always comes from the table's one random source (ShuffleDeckCommand); each style is only an
    /// animation of the cards going from the old order to the new one, with its own sound
    /// (Resources/TableSounds/Shuffle&lt;Style&gt; and Flourish&lt;Name&gt;; replace them with the same names).
    /// Quick Shuffle (R) stays the short riffle.
    /// </summary>
    public sealed partial class TabletopComposition
    {
        private const string ShuffleStylePrefsKey = "ConsoleCards.ShuffleStyle.";
        private const string ShuffleSoundFolder = "TableSounds/";
        private const int ShuffleStyleCount = 6;
        private const int FlourishCount = 5;

        private readonly List<KeyValuePair<Transform, TabletopTransformSnapshot>> shuffleCards =
            new List<KeyValuePair<Transform, TabletopTransformSnapshot>>();
        private readonly List<Transform> shuffleEnds = new List<Transform>();
        private readonly Dictionary<string, AudioClip> shuffleClips = new Dictionary<string, AudioClip>(StringComparer.Ordinal);

        private static readonly Comparison<KeyValuePair<Transform, TabletopTransformSnapshot>> LowestShuffleStartFirst =
            (a, b) => a.Value.Position.y.CompareTo(b.Value.Position.y);
        private static readonly Comparison<Transform> LowestShuffleEndFirst =
            (a, b) => a.position.y.CompareTo(b.position.y);

        internal static string ShuffleStyleName(ShuffleStyle style)
        {
            switch (style)
            {
                case ShuffleStyle.Overhand: return "Overhand";
                case ShuffleStyle.Hindu: return "Hindu";
                case ShuffleStyle.Pile: return "Pile";
                case ShuffleStyle.Wash: return "Wash";
                case ShuffleStyle.Cut: return "Cut";
                default: return "Riffle";
            }
        }

        internal static string FlourishName(CardFlourish flourish)
        {
            switch (flourish)
            {
                case CardFlourish.Spring: return "Spring";
                case CardFlourish.RibbonSpread: return "Ribbon spread";
                case CardFlourish.Cascade: return "Cascade";
                case CardFlourish.SwivelCut: return "Swivel cut";
                default: return "Fan";
            }
        }

        // ---------- your default style (per profile) ----------

        private string ShuffleStyleKey()
        {
            string profileId = string.Empty;
            try
            {
                profileId = EnsureProfile().EnsureActive().id ?? string.Empty;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Shuffle] No profile for the default shuffle style: {exception.Message}");
            }

            return ShuffleStylePrefsKey + profileId;
        }

        internal ShuffleStyle DefaultShuffleStyle
        {
            get
            {
                int value = PlayerPrefs.GetInt(ShuffleStyleKey(), (int)ShuffleStyle.Riffle);
                return value >= 0 && value < ShuffleStyleCount ? (ShuffleStyle)value : ShuffleStyle.Riffle;
            }
            set
            {
                PlayerPrefs.SetInt(ShuffleStyleKey(), (int)value);
                PlayerPrefs.Save();
            }
        }

        // ---------- menus ----------

        // The shuffle rows of a deck's or stack's menu: shuffle in your style, pick another style, a flourish.
        internal void AddShuffleActions(List<PopupActionOption> actions, ContainerId pileId)
        {
            ShuffleStyle mine = DefaultShuffleStyle;
            actions.Add(new PopupActionOption(
                mine == ShuffleStyle.Riffle ? "Shuffle" : $"Shuffle ({ShuffleStyleName(mine)})",
                true,
                () =>
                {
                    ShuffleDeckResult result = ShuffleDeck(pileId, mine);
                    if (result.Succeeded) ContextMenus.CloseContextMenu();
                }));
            if (!CanShuffleInStyles(pileId)) return;
            actions.Add(new PopupActionOption(
                "Shuffle as...",
                true,
                () => ContextMenus.SetContextMenuMode(ContextMenuMode.ShuffleStyles)));
            actions.Add(new PopupActionOption(
                "Flourish...",
                true,
                () => ContextMenus.SetContextMenuMode(ContextMenuMode.Flourishes)));
        }

        internal bool CanShuffleInStyles(ContainerId pileId)
        {
            return !pileId.IsEmpty
                && matchState != null
                && matchState.Containers.TryGetValue(pileId, out ContainerState pile)
                && (pile.Kind == ContainerKind.Deck || pile.Kind == ContainerKind.Stack)
                && pile.Count >= 2
                && TryGetPileTransform(pileId, out _);
        }

        internal void ShowShuffleStylesMenu()
        {
            ContainerId pileId = contextMenuContainerId;
            ShuffleStyle mine = DefaultShuffleStyle;
            List<PopupActionOption> actions = new List<PopupActionOption>(ShuffleStyleCount + 1);
            for (int i = 0; i < ShuffleStyleCount; i++)
            {
                ShuffleStyle style = (ShuffleStyle)i;
                actions.Add(new PopupActionOption(
                    style == mine ? $"{ShuffleStyleName(style)} (your default)" : ShuffleStyleName(style),
                    true,
                    () =>
                    {
                        ShuffleDeckResult result = ShuffleDeck(pileId, style);
                        if (result.Succeeded) ContextMenus.CloseContextMenu();
                    }));
            }

            actions.Add(new PopupActionOption(
                "Set my default...",
                true,
                () => ContextMenus.SetContextMenuMode(ContextMenuMode.ShuffleDefault)));
            runtimeUi.ShowContextMenu(
                contextMenuAnchorScreenPosition,
                "SHUFFLE AS",
                "Cut cuts the pile once; the others shuffle it.",
                actions,
                ContextMenus.CloseContextMenu,
                ContextMenus.DismissPopupFromSecondary);
        }

        internal void ShowShuffleDefaultMenu()
        {
            ShuffleStyle mine = DefaultShuffleStyle;
            List<PopupActionOption> actions = new List<PopupActionOption>(ShuffleStyleCount);
            for (int i = 0; i < ShuffleStyleCount; i++)
            {
                ShuffleStyle style = (ShuffleStyle)i;
                actions.Add(new PopupActionOption(
                    style == mine ? $"{ShuffleStyleName(style)} (now)" : ShuffleStyleName(style),
                    true,
                    () =>
                    {
                        DefaultShuffleStyle = style;
                        ShowMessage($"Your shuffle is now {ShuffleStyleName(style)}.");
                        ContextMenus.SetContextMenuMode(ContextMenuMode.ShuffleStyles);
                    }));
            }

            runtimeUi.ShowContextMenu(
                contextMenuAnchorScreenPosition,
                "MY SHUFFLE",
                "The deck menu's Shuffle uses this style. Kept for your profile.",
                actions,
                ContextMenus.CloseContextMenu,
                ContextMenus.DismissPopupFromSecondary);
        }

        internal void ShowFlourishMenu()
        {
            ContainerId pileId = contextMenuContainerId;
            List<PopupActionOption> actions = new List<PopupActionOption>(FlourishCount);
            for (int i = 0; i < FlourishCount; i++)
            {
                CardFlourish flourish = (CardFlourish)i;
                actions.Add(new PopupActionOption(
                    FlourishName(flourish),
                    true,
                    () =>
                    {
                        ContextMenus.CloseContextMenu();
                        PlayFlourish(pileId, flourish);
                    }));
            }

            runtimeUi.ShowContextMenu(
                contextMenuAnchorScreenPosition,
                "FLOURISH",
                "Just for show: the order stays the same.",
                actions,
                ContextMenus.CloseContextMenu,
                ContextMenus.DismissPopupFromSecondary);
        }

        private bool TryGetPileTransform(ContainerId pileId, out Transform pile)
        {
            pile = null;
            if (Registry.TryGetDeckPresentation(pileId, out _, out FixedContainerVisual deckVisual) && deckVisual != null)
            {
                pile = deckVisual.transform;
            }
            else if (Registry.stackViewsByContainerId.TryGetValue(pileId, out StackRuntimeView stack) && stack != null && stack.Root != null)
            {
                pile = stack.Root.transform;
            }

            return pile != null;
        }

        // ---------- the shuffle styles ----------

        // The cards from the old order (starts) to the new one (already laid out), in the style.
        private void AnimateShuffleStyle(
            ShuffleStyle style,
            IReadOnlyDictionary<Transform, TabletopTransformSnapshot> starts,
            Transform pile)
        {
            int count = PrepareShuffleCards(starts);
            if (count == 0) return;
            float speed = Mathf.Max(0.05f, Feel.ShuffleStyleSpeed);
            float card = WorldPerCardWidth();
            PileAxes(pile, out Vector3 side, out Vector3 toward);
            Vector3 baseline = shuffleCards[0].Value.Position;
            switch (style)
            {
                case ShuffleStyle.Overhand:
                    AnimatePackets(count, card, speed, side * (0.95f * card), Quaternion.AngleAxis(-55f, toward), 6, 0.14f);
                    break;
                case ShuffleStyle.Hindu:
                    AnimatePackets(count, card, speed, -toward * (0.7f * card), Quaternion.AngleAxis(18f, side), 8, 0.11f);
                    break;
                case ShuffleStyle.Pile:
                    AnimatePileShuffle(count, card, speed, side, toward, baseline);
                    break;
                case ShuffleStyle.Wash:
                    AnimateWash(count, card, speed, baseline);
                    break;
                case ShuffleStyle.Cut:
                    AnimateCut(count, card, speed, side);
                    break;
            }

            shuffleCards.Clear();
            shuffleEnds.Clear();
        }

        // Fills shuffleCards (lowest start first) and shuffleEnds (lowest place first). Returns the card count.
        private int PrepareShuffleCards(IReadOnlyDictionary<Transform, TabletopTransformSnapshot> starts)
        {
            shuffleCards.Clear();
            shuffleEnds.Clear();
            if (starts == null) return 0;
            foreach (KeyValuePair<Transform, TabletopTransformSnapshot> pair in starts)
            {
                if (pair.Key == null) continue;
                shuffleCards.Add(pair);
                shuffleEnds.Add(pair.Key);
            }

            shuffleCards.Sort(LowestShuffleStartFirst);
            shuffleEnds.Sort(LowestShuffleEndFirst);
            return shuffleCards.Count;
        }

        private static void PileAxes(Transform pile, out Vector3 side, out Vector3 toward)
        {
            side = pile != null ? pile.right : Vector3.right;
            side.y = 0f;
            if (side.sqrMagnitude < 0.0001f) side = Vector3.right;
            side.Normalize();
            // Along the pile's length, toward its far end (the card's top).
            toward = Vector3.Cross(side, Vector3.up).normalized;
            if (Vector3.Dot(toward, pile != null ? pile.forward : Vector3.forward) < 0f) toward = -toward;
        }

        // Overhand and Hindu: the pile is lifted beside or in front of its place and tipped, then dropped back in
        // small packets, the bottom of the new order first.
        private void AnimatePackets(int count, float card, float speed, Vector3 offset, Quaternion tip, int packets, float packetGap)
        {
            float lift = 0.9f * card;
            float move = 0.24f / speed;
            int perPacket = Mathf.Max(1, Mathf.CeilToInt(count / (float)packets));
            for (int i = 0; i < count; i++)
            {
                Transform target = shuffleCards[i].Key;
                TabletopTransformSnapshot start = shuffleCards[i].Value;
                int rank = shuffleEnds.IndexOf(target);
                int packet = Mathf.Max(0, rank) / perPacket;
                presentationTransitions.AnimateThrough(
                    target,
                    start,
                    start.Position + offset + (Vector3.up * lift),
                    tip * start.Rotation,
                    0f,
                    move,
                    0.1f * card,
                    move + (0.1f / speed) + (packet * packetGap / speed),
                    0.18f / speed,
                    0.12f * card);
            }
        }

        // Pile: dealt from the top into up to 4 piles in front of the pile, then the piles are stacked back.
        private void AnimatePileShuffle(int count, float card, float speed, Vector3 side, Vector3 toward, Vector3 baseline)
        {
            int piles = Mathf.Clamp(count / 3, 2, 4);
            float deal = Mathf.Min(0.07f, 1.2f / count) / speed;
            float move = 0.18f / speed;
            float dealt = ((count - 1) * deal) + move;
            const float thickness = 0.004f;
            for (int i = 0; i < count; i++)
            {
                Transform target = shuffleCards[i].Key;
                TabletopTransformSnapshot start = shuffleCards[i].Value;
                int order = count - 1 - i; // the top card is dealt first
                int pileIndex = order % piles;
                int layer = order / piles;
                Vector3 spot = baseline
                    - (toward * (1.5f * card))
                    + (side * ((pileIndex - ((piles - 1) * 0.5f)) * 1.25f * card))
                    + (Vector3.up * (layer * thickness));
                presentationTransitions.AnimateThrough(
                    target,
                    start,
                    spot,
                    start.Rotation,
                    order * deal,
                    move,
                    0.35f * card,
                    dealt + (0.15f / speed) + (pileIndex * 0.16f / speed),
                    0.24f / speed,
                    0.3f * card);
            }
        }

        // Wash: the cards spread out around the pile and swirl on the table, then are gathered into the new order.
        private void AnimateWash(int count, float card, float speed, Vector3 baseline)
        {
            float move = 0.32f / speed;
            float hold = 0.9f / speed;
            for (int i = 0; i < count; i++)
            {
                Transform target = shuffleCards[i].Key;
                TabletopTransformSnapshot start = shuffleCards[i].Value;
                float seed = Hash01(i * 7919 + 13);
                float angle = (i * 2.39996f) + (seed * 0.8f);
                float radius = card * (0.7f + (1.5f * Mathf.Sqrt((i + 1f) / count)));
                Vector3 spot = baseline + new Vector3(Mathf.Cos(angle) * radius, i * 0.002f, Mathf.Sin(angle) * radius);
                Quaternion turn = Quaternion.AngleAxis((Hash01(i * 104729 + 7) * 300f) - 150f, Vector3.up);
                int rank = Mathf.Max(0, shuffleEnds.IndexOf(target));
                presentationTransitions.AnimateThrough(
                    target,
                    start,
                    spot,
                    turn * start.Rotation,
                    i * 0.01f / speed,
                    move,
                    0.15f * card,
                    move + hold + (count * 0.01f / speed) + (rank * 0.012f / speed),
                    0.28f / speed,
                    0.2f * card,
                    baseline,
                    (40f + (Hash01(i * 15485863 + 3) * 70f)) * speed);
            }
        }

        // Cut: the top part lifts off and sets down beside the pile; the rest lifts and goes on top of it.
        private void AnimateCut(int count, float card, float speed, Vector3 side)
        {
            float move = 0.24f / speed;
            for (int i = 0; i < count; i++)
            {
                Transform target = shuffleCards[i].Key;
                TabletopTransformSnapshot start = shuffleCards[i].Value;
                bool topPart = target.position.y < start.Position.y - 0.0001f; // it ends lower: it went under
                if (topPart)
                {
                    presentationTransitions.AnimateThrough(
                        target,
                        start,
                        start.Position + (side * (1.15f * card)) - (Vector3.up * (start.Position.y - target.position.y)),
                        start.Rotation,
                        0f,
                        move,
                        0.3f * card,
                        move + (0.35f / speed),
                        0.24f / speed,
                        0.2f * card);
                }
                else
                {
                    presentationTransitions.AnimateThrough(
                        target,
                        start,
                        start.Position + (Vector3.up * (0.6f * card)),
                        start.Rotation,
                        move * 0.6f,
                        move,
                        0f,
                        move + (0.4f / speed),
                        0.24f / speed,
                        0.05f * card);
                }
            }
        }

        // ---------- flourishes (the order stays the same) ----------

        internal void PlayFlourish(ContainerId pileId, CardFlourish flourish)
        {
            EnsureInitialized();
            if (!CanShuffleInStyles(pileId) || !TryGetPileTransform(pileId, out Transform pile))
            {
                ShowMessage("A flourish needs a deck or stack of at least 2 cards.");
                return;
            }

            IReadOnlyDictionary<Transform, TabletopTransformSnapshot> starts = CaptureContainerCardTransforms(pileId);
            int count = PrepareShuffleCards(starts);
            if (count == 0) return;
            float speed = Mathf.Max(0.05f, Feel.ShuffleStyleSpeed);
            float card = WorldPerCardWidth();
            PileAxes(pile, out Vector3 side, out Vector3 toward);
            Vector3 baseline = shuffleCards[0].Value.Position;
            for (int i = 0; i < count; i++)
            {
                Transform target = shuffleCards[i].Key;
                TabletopTransformSnapshot start = shuffleCards[i].Value;
                float along = count > 1 ? i / (float)(count - 1) : 0f; // 0 bottom, 1 top
                switch (flourish)
                {
                    case CardFlourish.Fan:
                    {
                        // A thumb fan: the cards turn about the pile's near end, the top card furthest.
                        Vector3 pivot = baseline - (toward * (0.7f * card));
                        Quaternion turn = Quaternion.AngleAxis(Mathf.Lerp(-55f, 55f, along), Vector3.up);
                        presentationTransitions.AnimateThrough(target, start,
                            pivot + (turn * (start.Position - pivot)) + (Vector3.up * (0.25f * card)),
                            turn * start.Rotation,
                            i * 0.008f / speed, 0.3f / speed, 0f,
                            (0.3f + 0.7f + (count * 0.008f)) / speed + ((count - 1 - i) * 0.006f / speed),
                            0.26f / speed, 0f);
                        break;
                    }

                    case CardFlourish.Spring:
                    {
                        // The cards spring one by one from the pile to the other hand beside it, then come back.
                        int order = count - 1 - i; // the top card springs first and ends at the bottom there
                        Vector3 spot = baseline + (side * (1.6f * card)) + (Vector3.up * ((0.5f * card) + (order * 0.004f)));
                        presentationTransitions.AnimateThrough(target, start,
                            spot,
                            Quaternion.AngleAxis(-20f, toward) * start.Rotation,
                            order * Mathf.Min(0.03f, 0.8f / count) / speed, 0.22f / speed, 0.7f * card,
                            ((count * Mathf.Min(0.03f, 0.8f / count)) + 0.22f + 0.25f) / speed,
                            0.3f / speed, 0.35f * card);
                        break;
                    }

                    case CardFlourish.RibbonSpread:
                    {
                        // The cards slide out in an even ribbon across the table, then are swept back up.
                        Vector3 spot = baseline + (side * ((0.6f + (i * 0.16f)) * card)) + (Vector3.up * (i * 0.002f));
                        presentationTransitions.AnimateThrough(target, start,
                            spot,
                            start.Rotation,
                            i * 0.012f / speed, 0.24f / speed, 0f,
                            ((count * 0.012f) + 0.24f + 0.7f) / speed + ((count - 1 - i) * 0.01f / speed),
                            0.26f / speed, 0.03f * card);
                        break;
                    }

                    case CardFlourish.Cascade:
                    {
                        // The pile rises in the hand, tipped, and the cards fall back down one after another.
                        Vector3 spot = start.Position + (Vector3.up * ((1.3f * card) + (i * 0.02f * card))) - (toward * (0.3f * card));
                        presentationTransitions.AnimateThrough(target, start,
                            spot,
                            Quaternion.AngleAxis(25f, side) * start.Rotation,
                            0f, 0.28f / speed, 0f,
                            (0.28f + 0.2f) / speed + (i * Mathf.Min(0.035f, 1f / count) / speed),
                            0.2f / speed, 0f);
                        break;
                    }

                    default:
                    {
                        // Swivel cut: the top half swings out about the pile's corner and back on top.
                        if (i < count / 2)
                        {
                            break;
                        }

                        Vector3 corner = baseline - (side * (0.5f * card)) - (toward * (0.7f * card));
                        Quaternion turn = Quaternion.AngleAxis(-140f, Vector3.up);
                        presentationTransitions.AnimateThrough(target, start,
                            corner + (turn * (start.Position - corner)) + (Vector3.up * (0.15f * card)),
                            turn * start.Rotation,
                            0f, 0.32f / speed, 0f,
                            (0.32f + 0.18f) / speed,
                            0.32f / speed, 0.05f * card);
                        break;
                    }
                }
            }

            shuffleCards.Clear();
            shuffleEnds.Clear();
            PlayShuffleClip("Flourish" + flourish, false);
        }

        // ---------- sounds ----------

        private void PlayShuffleStyleSound(ShuffleStyle style)
        {
            PlayShuffleClip("Shuffle" + style, true);
        }

        // A style's or flourish's own sound by name; the riffle sound when it is missing.
        private void PlayShuffleClip(string name, bool fallBackToRiffle)
        {
            EnsureTableSounds();
            if (!shuffleClips.TryGetValue(name, out AudioClip clip))
            {
                clip = Resources.Load<AudioClip>(ShuffleSoundFolder + name);
                shuffleClips[name] = clip;
            }

            if (clip != null) tableSounds.Play(clip, Feel.CardSoundVolume * 1.4f, 1f);
            else if (fallBackToRiffle) PlayShuffleSound();
            else PlayCardSound(true);
        }

        // A steady 0..1 value for a number (scatter in the wash; the order never depends on it).
        private static float Hash01(int value)
        {
            unchecked
            {
                uint x = (uint)value;
                x ^= x >> 16;
                x *= 0x7feb352d;
                x ^= x >> 15;
                x *= 0x846ca68b;
                x ^= x >> 16;
                return (x & 0xFFFFFF) / (float)0x1000000;
            }
        }
    }
}
