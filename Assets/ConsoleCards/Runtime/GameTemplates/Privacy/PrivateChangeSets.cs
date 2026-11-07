using System;
using System.Collections.Generic;
using System.IO;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Dice;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Domain.Seats;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.GameTemplates.Privacy
{
    // PRIVACY-P6B (s151, doc 10 §60). The recipient change set: what one recipient's own table must change to match its
    // projection of the host's table after ANY accepted change (one card, many cards, a shuffle, a turn's own steps, an
    // Undo). The host computes it from the projection it last sent that recipient and the projection now, so a change
    // the private path did not describe one by one still reaches every recipient (filtered), and nothing is sent that
    // the recipient may not know. Development only.

    /// <summary>One object as the recipient's table holds it after the change (a card back carries the hidden definition).</summary>
    public sealed class PrivateObjectRecord
    {
        public TabletopObjectId Reference;
        public ObjectDefinitionId Definition;
        public TabletopObjectKind Kind;
        public TabletopPose Pose;
        public ContainerId Container;
        public PlayerId Owner;
        public ObjectVisibility Visibility;
        public bool Locked;
        public bool FaceUp;
    }

    /// <summary>
    /// A card that leaves the recipient's table. Named by the reference the recipient holds it by, except a card the
    /// recipient could identify that is now hidden from it: then by its old public place (container and index), so
    /// its real id is not sent again (P6A rule).
    /// </summary>
    public sealed class PrivateRemoval
    {
        public TabletopObjectId Reference;
        public ContainerId From;
        public int FromIndex = -1;
    }

    /// <summary>The order of one container whose order the recipient may know.</summary>
    public sealed class PrivateContainerOrder
    {
        public ContainerId Container;
        public List<TabletopObjectId> Order;
    }

    /// <summary>The public part of the game's status card (the same for everyone): round, phase, players, the turn player's card.</summary>
    public sealed class PrivateHudSnapshot
    {
        public string Round;
        public string Phase;
        public bool HasTurnColor;
        public float TurnR, TurnG, TurnB;
        public List<PrivateHudPlayer> Players = new List<PrivateHudPlayer>();
        public bool HasCard;
        public string CardTitle, CardAvatar, CardKeys, CardStatus, CardInHand, CardFloor;

        /// <summary>For "did it change": every field in order.</summary>
        public string Signature()
        {
            System.Text.StringBuilder b = new System.Text.StringBuilder();
            b.Append(Round).Append('|').Append(Phase).Append('|').Append(HasTurnColor).Append(TurnR).Append(TurnG).Append(TurnB);
            foreach (PrivateHudPlayer p in Players) b.Append('|').Append(p.Name).Append(p.R).Append(p.G).Append(p.B).Append(p.A).Append(p.Active);
            b.Append('|').Append(HasCard).Append(CardTitle).Append(CardAvatar).Append(CardKeys).Append(CardStatus).Append(CardInHand).Append(CardFloor);
            return b.ToString();
        }
    }

    public sealed class PrivateHudPlayer
    {
        public string Name;
        public float R, G, B, A;
        public bool Active;
    }

    public sealed class PrivateChangeSet
    {
        public List<PrivateRemoval> Removed = new List<PrivateRemoval>();
        public List<PrivateObjectRecord> Upserts = new List<PrivateObjectRecord>();
        public List<PrivateContainerOrder> Orders = new List<PrivateContainerOrder>();
        /// <summary>The public turn after the change (null: unchanged).</summary>
        public PrivateTurnSnapshot Turn;
        /// <summary>The public status card after the change (null: unchanged).</summary>
        public PrivateHudSnapshot Hud;
        /// <summary>The change cannot be described as object changes (a container or a non-card object made or removed): the host sends this recipient its own package instead.</summary>
        public bool NeedsPackage;
        public string PackageReason;

        public bool IsEmpty => Removed.Count == 0 && Upserts.Count == 0 && Orders.Count == 0 && Turn == null && Hud == null && !NeedsPackage;
    }

    /// <summary>Host side: the change set between the recipient's last table and its projection now.</summary>
    public static class PrivateChangeSetBuilder
    {
        private const double PoseTolerance = 0.0005;
        private const float RotationTolerance = 0.05f;

        /// <param name="host">The host's full table (to tell a concealed card from a removed one).</param>
        /// <param name="previous">The recipient's table as last sent.</param>
        /// <param name="previousHiddenOrder">The containers whose order was hidden from the recipient then.</param>
        /// <param name="current">The recipient's projection now (restored).</param>
        /// <param name="currentHiddenOrder">The containers whose order is hidden from it now.</param>
        public static PrivateChangeSet Build(MatchState host, MatchState previous, ICollection<ContainerId> previousHiddenOrder, MatchState current, ICollection<ContainerId> currentHiddenOrder)
        {
            PrivateChangeSet set = new PrivateChangeSet();
            if (previous == null) { set.NeedsPackage = true; set.PackageReason = "no table was sent yet"; return set; }

            if (!SameKeys(previous.Containers, current.Containers)) { set.NeedsPackage = true; set.PackageReason = "a container was made or removed"; return set; }
            if (!SameKeys(previous.Pawns, current.Pawns) || !SameKeys(previous.Dice, current.Dice) || !SameKeys(previous.Tokens, current.Tokens))
            {
                set.NeedsPackage = true;
                set.PackageReason = "a pawn, die or token was made or removed";
                return set;
            }

            if (ConsoleSidesDiffer(previous, current)) { set.NeedsPackage = true; set.PackageReason = "a Console was turned over"; return set; }

            // Cards that leave the recipient's table.
            List<TabletopObjectId> removedIds = new List<TabletopObjectId>();
            foreach (KeyValuePair<TabletopObjectId, CardInstanceState> pair in previous.Cards)
                if (!current.Cards.ContainsKey(pair.Key)) removedIds.Add(pair.Key);
            removedIds.Sort((a, b) => a.Value.CompareTo(b.Value)); // by reference: the order of the list tells nothing
            foreach (TabletopObjectId id in removedIds)
            {
                CardInstanceState was = previous.Cards[id];
                bool knew = !was.BaseState.DefinitionId.Equals(RecipientSnapshotProjection.HiddenDefinition);
                if (knew && host.Cards.ContainsKey(id))
                {
                    // Concealed: named by its old place, never again by its real id.
                    ContainerId from = was.BaseState.ContainerId;
                    if (from.IsEmpty)
                    {
                        // A card loose on the table that it could see (live P6B finding: an Undo taking one back into a
                        // hand sent the other guests their whole package). Named by its place among the loose cards of
                        // the table as last sent (ordered by reference), never again by its real id.
                        set.Removed.Add(new PrivateRemoval { Reference = TabletopObjectId.Empty, From = ContainerId.Empty, FromIndex = LooseOrdinal(previous, id) });
                        continue;
                    }

                    if (previousHiddenOrder.Contains(from) || !previous.Containers.TryGetValue(from, out ContainerState left))
                    {
                        set.NeedsPackage = true;
                        set.PackageReason = "a card it could see was hidden from a place whose order it may not know";
                        set.Removed.Clear();
                        set.Upserts.Clear();
                        return set;
                    }

                    set.Removed.Add(new PrivateRemoval { Reference = TabletopObjectId.Empty, From = from, FromIndex = left.IndexOf(id) });
                }
                else
                {
                    set.Removed.Add(new PrivateRemoval { Reference = id, From = was.BaseState.ContainerId });
                }
            }

            // Cards new to the recipient's table, or changed.
            List<TabletopObjectId> upsertIds = new List<TabletopObjectId>();
            foreach (KeyValuePair<TabletopObjectId, CardInstanceState> pair in current.Cards)
                if (!previous.Cards.TryGetValue(pair.Key, out CardInstanceState was) || CardChanged(was, pair.Value)) upsertIds.Add(pair.Key);
            upsertIds.Sort((a, b) => a.Value.CompareTo(b.Value));
            foreach (TabletopObjectId id in upsertIds) set.Upserts.Add(Record(current.Cards[id].BaseState, current.Cards[id].Face == CardFace.FaceUp));

            // Pawns, dice and tokens: public; only where they lie.
            AddPoseChanges(set, previous, current);

            // Orders the recipient may know, where they differ.
            foreach (KeyValuePair<ContainerId, ContainerState> pair in current.Containers)
            {
                if (currentHiddenOrder.Contains(pair.Key)) continue;
                ContainerState was = previous.Containers[pair.Key];
                if (SameOrder(was, pair.Value)) continue;
                set.Orders.Add(new PrivateContainerOrder { Container = pair.Key, Order = new List<TabletopObjectId>(pair.Value.ObjectIds) });
            }

            set.Orders.Sort((a, b) => a.Container.Value.CompareTo(b.Container.Value));
            return set;
        }

        /// <summary>The place of a loose card among a table's loose cards, ordered by reference (both sides can count it).</summary>
        public static int LooseOrdinal(MatchState table, TabletopObjectId id)
        {
            List<TabletopObjectId> loose = LooseCards(table);
            return loose.IndexOf(id);
        }

        public static List<TabletopObjectId> LooseCards(MatchState table)
        {
            List<TabletopObjectId> loose = new List<TabletopObjectId>();
            foreach (KeyValuePair<TabletopObjectId, CardInstanceState> pair in table.Cards)
                if (pair.Value.BaseState.ContainerId.IsEmpty) loose.Add(pair.Key);
            loose.Sort((a, b) => a.Value.CompareTo(b.Value));
            return loose;
        }

        private static void AddPoseChanges(PrivateChangeSet set, MatchState previous, MatchState current)
        {
            List<TabletopObjectState> moved = new List<TabletopObjectState>();
            foreach (KeyValuePair<TabletopObjectId, PawnState> pair in current.Pawns)
                if (PoseChanged(previous.Pawns[pair.Key].BaseState, pair.Value.BaseState)) moved.Add(pair.Value.BaseState);
            foreach (KeyValuePair<TabletopObjectId, DieState> pair in current.Dice)
                if (PoseChanged(previous.Dice[pair.Key].BaseState, pair.Value.BaseState)) moved.Add(pair.Value.BaseState);
            foreach (KeyValuePair<TabletopObjectId, TokenState> pair in current.Tokens)
                if (PoseChanged(previous.Tokens[pair.Key].BaseState, pair.Value.BaseState)) moved.Add(pair.Value.BaseState);
            moved.Sort((a, b) => a.Id.Value.CompareTo(b.Id.Value));
            foreach (TabletopObjectState state in moved) set.Upserts.Add(Record(state, true));
        }

        private static PrivateObjectRecord Record(TabletopObjectState state, bool faceUp) => new PrivateObjectRecord
        {
            Reference = state.Id,
            Definition = state.DefinitionId,
            Kind = state.Kind,
            Pose = state.Pose,
            Container = state.ContainerId,
            Owner = state.OwnerPlayerId,
            Visibility = state.Visibility,
            Locked = state.IsUserLocked,
            FaceUp = faceUp
        };

        private static bool CardChanged(CardInstanceState a, CardInstanceState b)
        {
            TabletopObjectState x = a.BaseState, y = b.BaseState;
            if (a.Face != b.Face || x.ContainerId != y.ContainerId || !x.DefinitionId.Equals(y.DefinitionId)) return true;
            if (x.OwnerPlayerId != y.OwnerPlayerId || x.Visibility != y.Visibility || x.IsUserLocked != y.IsUserLocked) return true;
            return y.ContainerId.IsEmpty && PoseChanged(x, y); // a card in a container lies where the container lays it out
        }

        private static bool PoseChanged(TabletopObjectState a, TabletopObjectState b)
        {
            if (a.ContainerId != b.ContainerId) return true;
            return Math.Abs(a.Pose.Position.X - b.Pose.Position.X) > PoseTolerance
                || Math.Abs(a.Pose.Position.Y - b.Pose.Position.Y) > PoseTolerance
                || Math.Abs(a.Pose.RotationDegrees - b.Pose.RotationDegrees) > RotationTolerance;
        }

        private static bool SameOrder(ContainerState a, ContainerState b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++) if (a.GetObjectAt(i) != b.GetObjectAt(i)) return false;
            return true;
        }

        private static bool SameKeys<TKey, TValue>(IReadOnlyDictionary<TKey, TValue> a, IReadOnlyDictionary<TKey, TValue> b)
        {
            if (a.Count != b.Count) return false;
            foreach (TKey key in a.Keys) if (!b.ContainsKey(key)) return false;
            return true;
        }

        /// <summary>True when any seat's Console shows the other side than it did (a change sets cannot describe).</summary>
        public static bool ConsoleSidesDiffer(MatchState previous, MatchState current)
        {
            foreach (KeyValuePair<SeatId, SeatState> pair in current.Seats)
            {
                if (!previous.Seats.TryGetValue(pair.Key, out SeatState was)) return true;
                bool a = was.Console != null && was.Console.ShowsBack;
                bool b = pair.Value.Console != null && pair.Value.Console.ShowsBack;
                if (a != b) return true;
            }

            return false;
        }
    }

    /// <summary>What a change set did to a client's table, for its scene.</summary>
    public sealed class PrivateChangeSummary
    {
        /// <summary>Card views to take down (by the reference they are bound to).</summary>
        public readonly List<TabletopObjectId> Released = new List<TabletopObjectId>();
        /// <summary>Card views to make (the card is in the table now).</summary>
        public readonly List<TabletopObjectId> Created = new List<TabletopObjectId>();
        /// <summary>Pawns, dice and tokens that now lie elsewhere.</summary>
        public readonly List<TabletopObjectId> Moved = new List<TabletopObjectId>();
        /// <summary>Containers to lay out again.</summary>
        public readonly HashSet<ContainerId> Containers = new HashSet<ContainerId>();
        /// <summary>Old references of cards this client could see that are now hidden from it (they authorize nothing any more).</summary>
        public readonly List<TabletopObjectId> Withdrawn = new List<TabletopObjectId>();
    }

    /// <summary>Client side: applies a change set to the client's own table. Null when applied and the digest matches.</summary>
    public static class PrivateChangeSetApplier
    {
        public static string Apply(MatchState table, PrivateChangeSet set, string digest, PrivateChangeSummary summary)
        {
            if (table == null) return "no table";
            if (set == null) return "no change set";
            if (set.NeedsPackage) return "the host sends this computer's package instead (" + set.PackageReason + ")";
            ContainerTransferService transfer = new ContainerTransferService();

            // 1. Resolve every removal before anything moves (places refer to the table as it was).
            List<TabletopObjectId> remove = new List<TabletopObjectId>();
            foreach (PrivateRemoval removal in set.Removed)
            {
                TabletopObjectId id = removal.Reference;
                if (id.IsEmpty)
                {
                    if (removal.From.IsEmpty)
                    {
                        // A loose card, by its place among this table's loose cards (as the table was).
                        List<TabletopObjectId> loose = PrivateChangeSetBuilder.LooseCards(table);
                        if (removal.FromIndex < 0 || removal.FromIndex >= loose.Count) return "a removal names a loose card this table does not have";
                        id = loose[removal.FromIndex];
                    }
                    else
                    {
                        if (removal.FromIndex < 0 || !table.Containers.TryGetValue(removal.From, out ContainerState left) || removal.FromIndex >= left.Count)
                            return "a removal names a place this table cannot read";
                        id = left.GetObjectAt(removal.FromIndex);
                    }

                    summary?.Withdrawn.Add(id);
                }

                if (!table.Cards.TryGetValue(id, out CardInstanceState card)) return "a removal names a card this table does not have";
                if (remove.Contains(id)) return "a card is removed twice";
                remove.Add(id);
                summary?.Released.Add(id);
                if (!card.BaseState.ContainerId.IsEmpty) summary?.Containers.Add(card.BaseState.ContainerId);
            }

            // 2. Take out the removed cards and the old state of every changed card, then put the new ones in.
            foreach (PrivateObjectRecord record in set.Upserts)
            {
                if (record.Kind != TabletopObjectKind.Card) continue;
                if (record.Reference.IsEmpty || record.Definition.IsEmpty) return "a record has no reference or identity";
                if (table.Cards.TryGetValue(record.Reference, out CardInstanceState old))
                {
                    if (!remove.Contains(record.Reference)) remove.Add(record.Reference);
                    if (summary != null && !summary.Released.Contains(record.Reference)) summary.Released.Add(record.Reference);
                    if (!old.BaseState.ContainerId.IsEmpty) summary?.Containers.Add(old.BaseState.ContainerId);
                }
                else if (table.ContainsObject(record.Reference)) return "a record's reference is already another object";
            }

            foreach (TabletopObjectId id in remove) table.RemoveObject(id);

            foreach (PrivateObjectRecord record in set.Upserts)
            {
                if (record.Kind != TabletopObjectKind.Card)
                {
                    if (!table.ContainsObject(record.Reference)) return "a record names a piece this table does not have";
                    table.GetObject(record.Reference).SetPose(record.Pose);
                    summary?.Moved.Add(record.Reference);
                    continue;
                }

                if (!record.Container.IsEmpty && !table.Containers.ContainsKey(record.Container)) return "a record names a container this table does not have";
                TabletopObjectState state = new TabletopObjectState(
                    record.Reference, record.Definition, TabletopObjectKind.Card, record.Pose, ContainerId.Empty,
                    record.Owner, record.Visibility, record.Locked);
                table.AddUncontainedCard(new CardInstanceState(state, record.FaceUp ? CardFace.FaceUp : CardFace.FaceDown));
                if (!record.Container.IsEmpty && !transfer.PlaceIntoContainer(state, table.Containers[record.Container]).Succeeded)
                    return "a card could not be put in its container";
                summary?.Created.Add(record.Reference);
                if (!record.Container.IsEmpty) summary?.Containers.Add(record.Container);
            }

            // 3. The orders it may know.
            foreach (PrivateContainerOrder order in set.Orders)
            {
                if (!table.Containers.TryGetValue(order.Container, out ContainerState container)) return "an order names a container this table does not have";
                try
                {
                    container.ReplaceOrder(order.Order);
                }
                catch (ArgumentException exception)
                {
                    return "an order does not match the container: " + exception.Message;
                }

                summary?.Containers.Add(order.Container);
            }

            return PrivateClientModel.FromTable(table).Digest() == digest ? null : "the table no longer matches the host's check";
        }
    }

    /// <summary>
    /// PRIVACY-P6B: the general private request (variable length, read strictly; plain bytes, never an object graph).
    /// It names the operation and its public arguments in the sender's own references; never a seat or a player.
    /// </summary>
    public enum PrivateOperationKind : byte
    {
        Transfer = 20,
        Flip = 21,
        Shuffle = 22,
        Reorder = 23,
        DrawCount = 24,
        Undo = 25,
    }

    public sealed class PrivateOperation
    {
        public PrivateOperationKind Kind;
        public Guid RequestId;
        public TabletopObjectId Card;
        /// <summary>Transfer: the container the sender saw the card in (stale check). Shuffle / Reorder / DrawCount: the container acted on.</summary>
        public ContainerId Container;
        /// <summary>Transfer / DrawCount: where it goes (empty with a pose: the open table).</summary>
        public ContainerId Destination;
        public bool HasPose;
        public TabletopPose Pose;
        /// <summary>Flip: the face asked for.</summary>
        public bool FaceUp;
        /// <summary>Reorder: from index; DrawCount: how many.</summary>
        public int A;
        /// <summary>Reorder: to index.</summary>
        public int B;
        /// <summary>Undo: the host revision the sender's table shows (a request made before something else happened is stale).</summary>
        public long Revision;
    }

    public static class PrivateOperationCodec
    {
        public const byte Version = 1;
        private const int Length = 1 + 1 + 16 + 16 + 16 + 16 + 1 + 8 + 8 + 4 + 4 + 4 + 1 + 4 + 4 + 8;

        public static bool IsOperation(byte[] bytes, int offset) =>
            bytes != null && offset >= 0 && bytes.Length - offset >= 1 && bytes[offset] >= (byte)PrivateOperationKind.Transfer && bytes[offset] <= (byte)PrivateOperationKind.Undo;

        public static byte[] Write(PrivateOperation op)
        {
            using (MemoryStream stream = new MemoryStream(Length))
            using (BinaryWriter w = new BinaryWriter(stream))
            {
                w.Write((byte)op.Kind);
                w.Write(Version);
                w.Write(op.RequestId.ToByteArray());
                w.Write(op.Card.Value.ToByteArray());
                w.Write(op.Container.Value.ToByteArray());
                w.Write(op.Destination.Value.ToByteArray());
                w.Write(op.HasPose);
                w.Write(op.Pose.Position.X);
                w.Write(op.Pose.Position.Y);
                w.Write(op.Pose.RotationDegrees);
                w.Write(op.Pose.Layer);
                w.Write(op.Pose.LocalOrder);
                w.Write(op.FaceUp);
                w.Write(op.A);
                w.Write(op.B);
                w.Write(op.Revision);
                w.Flush();
                return stream.ToArray();
            }
        }

        /// <summary>False for anything that is not exactly one well-formed operation.</summary>
        public static bool TryRead(byte[] bytes, int offset, out PrivateOperation op)
        {
            op = null;
            if (!IsOperation(bytes, offset) || bytes.Length - offset != Length) return false;
            try
            {
                using (BinaryReader r = new BinaryReader(new MemoryStream(bytes, offset, Length, false)))
                {
                    PrivateOperation read = new PrivateOperation { Kind = (PrivateOperationKind)r.ReadByte() };
                    if (r.ReadByte() != Version) return false;
                    read.RequestId = new Guid(r.ReadBytes(16));
                    read.Card = new TabletopObjectId(new Guid(r.ReadBytes(16)));
                    read.Container = new ContainerId(new Guid(r.ReadBytes(16)));
                    read.Destination = new ContainerId(new Guid(r.ReadBytes(16)));
                    byte hasPose = r.ReadByte();
                    if (hasPose > 1) return false;
                    read.HasPose = hasPose == 1;
                    double x = r.ReadDouble(), y = r.ReadDouble();
                    float rotation = r.ReadSingle();
                    int layer = r.ReadInt32(), localOrder = r.ReadInt32();
                    if (double.IsNaN(x) || double.IsInfinity(x) || double.IsNaN(y) || double.IsInfinity(y) || float.IsNaN(rotation) || float.IsInfinity(rotation)) return false;
                    read.Pose = new TabletopPose(new TableCoordinate(x, y), rotation, layer, localOrder);
                    byte face = r.ReadByte();
                    if (face > 1) return false;
                    read.FaceUp = face == 1;
                    read.A = r.ReadInt32();
                    read.B = r.ReadInt32();
                    read.Revision = r.ReadInt64();
                    if (read.RequestId == Guid.Empty) return false;
                    if (!Shaped(read)) return false;
                    op = read;
                    return true;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        // Each kind names exactly what it needs; anything else is refused.
        private static bool Shaped(PrivateOperation op)
        {
            switch (op.Kind)
            {
                case PrivateOperationKind.Transfer:
                    return !op.Card.IsEmpty && (op.Destination.IsEmpty == op.HasPose) && op.Destination != op.Container;
                case PrivateOperationKind.Flip:
                    return !op.Card.IsEmpty && op.Container.IsEmpty && op.Destination.IsEmpty && !op.HasPose;
                case PrivateOperationKind.Shuffle:
                    return op.Card.IsEmpty && !op.Container.IsEmpty && op.Destination.IsEmpty && !op.HasPose;
                case PrivateOperationKind.Reorder:
                    return !op.Card.IsEmpty && !op.Container.IsEmpty && op.Destination.IsEmpty && !op.HasPose && op.A >= 0 && op.B >= 0;
                case PrivateOperationKind.DrawCount:
                    return op.Card.IsEmpty && !op.Container.IsEmpty && !op.Destination.IsEmpty && !op.HasPose && op.A >= 1 && op.A <= 20;
                case PrivateOperationKind.Undo:
                    return op.Card.IsEmpty && op.Container.IsEmpty && op.Destination.IsEmpty && !op.HasPose && op.Revision >= 0;
                default:
                    return false;
            }
        }
    }
}
