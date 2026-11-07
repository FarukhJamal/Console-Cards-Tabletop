using System;
using System.Collections.Generic;
using System.IO;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.GameTemplates.Definitions;

namespace ConsoleCards.GameTemplates.Privacy
{
    // PRIVACY-P6C (s152, doc 14). The private path's Shop and Payment. The purchase itself is the game's own
    // TrapFloorBuyFromShopCommand, run once on the authority; nothing here buys anything. This file holds the two
    // requests a seat sends (Confirm: buy with these payment cards; Cancel: put these staged cards back), the checks
    // that decide who may send them, and the Shop's public list a guest needs to show the existing Shop and payment
    // popups (the game's prices and which Controller Card gives which button: public game data, as the Shop shows it
    // to every player). Choosing the cards stays on the payer's computer until Confirm, as in normal play. Development only.

    /// <summary>One Ability the Shop lists: its public name, text, kind and price (InputCostNotation; empty: not for sale).</summary>
    public sealed class PrivateShopEntry
    {
        public string StableId;
        public string DisplayName;
        public string Description;
        public string Category;
        public List<string> Tags = new List<string>();
        public string Cost;
    }

    /// <summary>A Controller Card definition and the button it gives (public: printed on the card).</summary>
    public sealed class PrivateControllerCard
    {
        public string StableId;
        public string DisplayName;
        public int Input;
    }

    /// <summary>The Shop as every seat may know it.</summary>
    public sealed class PrivateShopCatalog
    {
        public List<PrivateShopEntry> Entries = new List<PrivateShopEntry>();
        public List<PrivateControllerCard> Controllers = new List<PrivateControllerCard>();

        public static PrivateShopCatalog From(IEnumerable<CardDefinitionData> shopCards, IEnumerable<CardDefinitionData> controllerCards)
        {
            PrivateShopCatalog catalog = new PrivateShopCatalog();
            if (shopCards != null)
                foreach (CardDefinitionData card in shopCards)
                {
                    if (card == null) continue;
                    PrivateShopEntry entry = new PrivateShopEntry
                    {
                        StableId = card.StableId,
                        DisplayName = card.DisplayName,
                        Description = card.Description ?? string.Empty,
                        Category = card.Category ?? string.Empty,
                        Cost = card.InputCost != null && !card.InputCost.IsEmpty ? InputCostNotation.Format(card.InputCost) : string.Empty,
                    };
                    for (int i = 0; i < card.Tags.Count; i++) entry.Tags.Add(card.Tags[i]);
                    catalog.Entries.Add(entry);
                }

            if (controllerCards != null)
                foreach (CardDefinitionData card in controllerCards)
                    if (card != null && card.RepresentedControllerInput.HasValue)
                        catalog.Controllers.Add(new PrivateControllerCard { StableId = card.StableId, DisplayName = card.DisplayName, Input = (int)card.RepresentedControllerInput.Value });
            return catalog;
        }

        /// <summary>
        /// The same list as game definition data, so a guest uses the game's own Shop and cost services (its catalog,
        /// for-sale and kind checks, and ControllerInputCostEvaluator) unchanged. Null when an entry cannot be read.
        /// </summary>
        public GameDefinitionData ToDefinition()
        {
            List<CardDefinitionData> cards = new List<CardDefinitionData>();
            foreach (PrivateShopEntry entry in Entries)
            {
                InputCostData cost = new InputCostData(new InputRequirementData[0]); // not for sale until it has a price
                if (!string.IsNullOrEmpty(entry.Cost) && !InputCostNotation.TryParse(entry.Cost, out cost, out _)) return null;
                cards.Add(new CardDefinitionData(entry.StableId, entry.DisplayName, entry.Category, entry.Description, string.Empty, string.Empty, 1,
                    cost, CardOrientation.Portrait, string.Empty, entry.Tags));
            }

            foreach (PrivateControllerCard controller in Controllers)
            {
                if (!Enum.IsDefined(typeof(ControllerInput), controller.Input)) return null;
                cards.Add(new CardDefinitionData(controller.StableId, controller.DisplayName, "Controller", string.Empty, string.Empty, string.Empty, 1,
                    new InputCostData(new InputRequirementData[0]), CardOrientation.Portrait, string.Empty, new string[0], (ControllerInput)controller.Input));
            }

            return new GameDefinitionData("private-shop", "Shop", string.Empty, 1, 1, null, cards, new GameContentSetData[0], new AvatarDefinitionData[0],
                new ModeDefinitionData[0], string.Empty, null, new ControllerInput[0], null, default, string.Empty, string.Empty);
        }

        /// <summary>Every name the list shows (public: the byte check does not count them as hidden-only names).</summary>
        public HashSet<string> Names()
        {
            HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
            foreach (PrivateShopEntry entry in Entries) if (!string.IsNullOrEmpty(entry.DisplayName)) names.Add(entry.DisplayName);
            foreach (PrivateControllerCard controller in Controllers) if (!string.IsNullOrEmpty(controller.DisplayName)) names.Add(controller.DisplayName);
            return names;
        }
    }

    public enum PrivateTransactionKind : byte
    {
        /// <summary>Confirm Payment: buy one Shop Ability with these payment cards (in the buyer's own Console).</summary>
        ShopBuy = 30,
        /// <summary>Cancel Payment: put these staged cards back where they were (the buyer's own Hand or Console).</summary>
        PaymentRestore = 31,
    }

    public sealed class PrivateTransactionItem
    {
        public TabletopObjectId Card;
        /// <summary>PaymentRestore: where the card goes back to (empty: loose on the table at <see cref="Pose"/>).</summary>
        public ContainerId Container;
        /// <summary>PaymentRestore: its place in that container (as when the payment began).</summary>
        public int Index;
        public bool HasPose;
        public TabletopPose Pose;
        public bool FaceUp;
    }

    public sealed class PrivateTransaction
    {
        public PrivateTransactionKind Kind;
        public Guid RequestId;
        /// <summary>The revision the sender's table showed when it confirmed (a purchase against an older table is refused).</summary>
        public long Revision;
        /// <summary>ShopBuy: the Ability's definition (its stable id as a Guid).</summary>
        public Guid Definition;
        public List<PrivateTransactionItem> Items = new List<PrivateTransactionItem>();
    }

    /// <summary>Strict bytes for the two payment requests: version 1, at most <see cref="MaxItems"/> cards, nothing extra.</summary>
    public static class PrivateTransactionCodec
    {
        public const byte Version = 1;
        public const int MaxItems = 8;
        private const int HeaderLength = 1 + 1 + 16 + 8 + 16 + 1;
        private const int ItemLength = 16 + 16 + 4 + 1 + 8 + 8 + 4 + 4 + 4 + 1;

        public static bool IsTransaction(byte[] bytes, int offset) =>
            bytes != null && offset >= 0 && bytes.Length - offset >= 1
            && (bytes[offset] == (byte)PrivateTransactionKind.ShopBuy || bytes[offset] == (byte)PrivateTransactionKind.PaymentRestore);

        public static byte[] Write(PrivateTransaction tx)
        {
            using (MemoryStream stream = new MemoryStream(HeaderLength + ItemLength * tx.Items.Count))
            using (BinaryWriter w = new BinaryWriter(stream))
            {
                w.Write((byte)tx.Kind);
                w.Write(Version);
                w.Write(tx.RequestId.ToByteArray());
                w.Write(tx.Revision);
                w.Write(tx.Definition.ToByteArray());
                w.Write((byte)tx.Items.Count);
                foreach (PrivateTransactionItem item in tx.Items)
                {
                    w.Write(item.Card.Value.ToByteArray());
                    w.Write(item.Container.Value.ToByteArray());
                    w.Write(item.Index);
                    w.Write(item.HasPose);
                    w.Write(item.Pose.Position.X);
                    w.Write(item.Pose.Position.Y);
                    w.Write(item.Pose.RotationDegrees);
                    w.Write(item.Pose.Layer);
                    w.Write(item.Pose.LocalOrder);
                    w.Write(item.FaceUp);
                }

                w.Flush();
                return stream.ToArray();
            }
        }

        /// <summary>False for anything that is not exactly one well-formed payment request.</summary>
        public static bool TryRead(byte[] bytes, int offset, out PrivateTransaction tx)
        {
            tx = null;
            if (!IsTransaction(bytes, offset) || bytes.Length - offset < HeaderLength) return false;
            try
            {
                using (BinaryReader r = new BinaryReader(new MemoryStream(bytes, offset, bytes.Length - offset, false)))
                {
                    PrivateTransaction read = new PrivateTransaction { Kind = (PrivateTransactionKind)r.ReadByte() };
                    if (r.ReadByte() != Version) return false;
                    read.RequestId = new Guid(r.ReadBytes(16));
                    read.Revision = r.ReadInt64();
                    read.Definition = new Guid(r.ReadBytes(16));
                    int count = r.ReadByte();
                    if (count < 1 || count > MaxItems || bytes.Length - offset != HeaderLength + ItemLength * count) return false;
                    HashSet<TabletopObjectId> seen = new HashSet<TabletopObjectId>();
                    for (int i = 0; i < count; i++)
                    {
                        PrivateTransactionItem item = new PrivateTransactionItem
                        {
                            Card = new TabletopObjectId(new Guid(r.ReadBytes(16))),
                            Container = new ContainerId(new Guid(r.ReadBytes(16))),
                            Index = r.ReadInt32(),
                        };
                        byte hasPose = r.ReadByte();
                        if (hasPose > 1) return false;
                        item.HasPose = hasPose == 1;
                        double x = r.ReadDouble(), y = r.ReadDouble();
                        float rotation = r.ReadSingle();
                        int layer = r.ReadInt32(), localOrder = r.ReadInt32();
                        if (double.IsNaN(x) || double.IsInfinity(x) || double.IsNaN(y) || double.IsInfinity(y) || float.IsNaN(rotation) || float.IsInfinity(rotation)) return false;
                        item.Pose = new TabletopPose(new TableCoordinate(x, y), rotation, layer, localOrder);
                        byte face = r.ReadByte();
                        if (face > 1) return false;
                        item.FaceUp = face == 1;
                        if (item.Card.IsEmpty || !seen.Add(item.Card)) return false;
                        read.Items.Add(item);
                    }

                    if (read.RequestId == Guid.Empty || read.Revision < 0 || !Shaped(read)) return false;
                    tx = read;
                    return true;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool Shaped(PrivateTransaction tx)
        {
            switch (tx.Kind)
            {
                case PrivateTransactionKind.ShopBuy:
                    if (tx.Definition == Guid.Empty) return false;
                    foreach (PrivateTransactionItem item in tx.Items)
                        if (!item.Container.IsEmpty || item.HasPose || item.Index != 0) return false; // a payment card only
                    return true;
                case PrivateTransactionKind.PaymentRestore:
                    if (tx.Definition != Guid.Empty) return false;
                    foreach (PrivateTransactionItem item in tx.Items)
                        if (item.Container.IsEmpty != item.HasPose || item.Index < 0) return false; // a pile and a place, or a spot
                    return true;
                default:
                    return false;
            }
        }
    }

    /// <summary>A checked payment request: the real ids the references named.</summary>
    public sealed class PrivateResolvedTransaction
    {
        public PrivateTransaction Transaction;
        public readonly List<TabletopObjectId> RealCards = new List<TabletopObjectId>();
    }

    /// <summary>
    /// PRIVACY-P6C: who may send a payment request; the same for every seat (the host's own included). Privacy and
    /// ownership only: the game's own purchase (TrapFloorBuyFromShopUseCase) then decides the rules (whose turn and
    /// phase, the exact price, the cards in the Console side slots, unlocked, where the bought card goes).
    /// </summary>
    public static class PrivateTransactionPolicy
    {
        public static PrivateRequestVerdict Check(MatchState match, IPrivateSeats seats, PrivateTableHost host, PlayerId sender, PrivateTransaction tx, out PrivateResolvedTransaction resolved)
        {
            resolved = null;
            if (tx == null) return PrivateRequestVerdict.UnknownReference;
            if (!seats.IsSeated(sender)) return PrivateRequestVerdict.NotSeated;
            if (tx.Kind == PrivateTransactionKind.ShopBuy && !seats.IsTurnOf(sender)) return PrivateRequestVerdict.NotYourTurn;
            if (!host.AcceptRequest(tx.RequestId)) return PrivateRequestVerdict.Duplicate; // one Confirm buys once
            if (tx.Kind == PrivateTransactionKind.ShopBuy && tx.Revision != host.Revision) return PrivateRequestVerdict.StaleRevision;
            SeatId seat = PrivateOperationPolicy.SeatOf(match, sender);
            resolved = new PrivateResolvedTransaction { Transaction = tx };
            foreach (PrivateTransactionItem item in tx.Items)
            {
                ReferenceResolution resolution = host.Resolve(sender, item.Card, out TabletopObjectId real);
                if (resolution == ReferenceResolution.Unknown) return PrivateRequestVerdict.UnknownReference;
                if (resolution == ReferenceResolution.HiddenRealId) return PrivateRequestVerdict.HiddenRealId;
                if (!match.Cards.TryGetValue(real, out CardInstanceState card)) return PrivateRequestVerdict.InvalidSource;
                ContainerId at = card.BaseState.ContainerId;
                if (tx.Kind == PrivateTransactionKind.ShopBuy)
                {
                    // A payment card is in the buyer's own Console (the game then checks it is a side slot).
                    if (at.IsEmpty || !seats.IsOwnConsoleSlot(sender, at)) return PrivateRequestVerdict.InvalidSource;
                }
                else
                {
                    // A staged card goes back from the payer's own Console, Hand or the table, to its own Hand, Console
                    // or a spot on the table; never into or out of another seat's places.
                    bool fromOwn = at.IsEmpty || at == seats.HandOf(sender) || seats.IsOwnConsoleSlot(sender, at);
                    if (!fromOwn) return PrivateRequestVerdict.InvalidSource;
                    if (!item.HasPose)
                    {
                        if (!match.Containers.TryGetValue(item.Container, out ContainerState to)) return PrivateRequestVerdict.InvalidDestination;
                        bool toOwn = to.Id == seats.HandOf(sender) || seats.IsOwnConsoleSlot(sender, to.Id);
                        if (!toOwn || !PrivateOperationPolicy.MayUseHere(match, seats, host, sender, seat, to)) return PrivateRequestVerdict.InvalidDestination;
                    }
                }

                resolved.RealCards.Add(real);
            }

            return PrivateRequestVerdict.Accepted;
        }
    }
}
