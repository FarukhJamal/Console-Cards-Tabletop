using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Identifiers;
using NUnit.Framework;

namespace ConsoleCards.Tests.EditMode.Core
{
    public sealed class ExplicitObjectStateTests
    {
        [Test]
        public void CardInstanceState_WhenBaseStateIsCard_AcceptsBaseState()
        {
            TabletopObjectState baseState = CreateBaseState(TabletopObjectKind.Card);
            CardInstanceState state = new CardInstanceState(baseState, CardFace.FaceDown);

            Assert.That(state.BaseState, Is.SameAs(baseState));
            Assert.That(state.Face, Is.EqualTo(CardFace.FaceDown));
        }

        [Test]
        public void CardInstanceState_WhenBaseStateIsNull_ThrowsArgumentNullException()
        {
            Assert.That(
                () => new CardInstanceState(null, CardFace.FaceUp),
                Throws.TypeOf<System.ArgumentNullException>());
        }

        [Test]
        public void CardInstanceState_WhenBaseStateIsNotCard_ThrowsArgumentException()
        {
            TabletopObjectState baseState = CreateBaseState(TabletopObjectKind.Pawn);

            Assert.That(
                () => new CardInstanceState(baseState, CardFace.FaceUp),
                Throws.ArgumentException);
        }

        [Test]
        public void CardInstanceState_SetFace_UpdatesFace()
        {
            CardInstanceState state = new CardInstanceState(CreateBaseState(TabletopObjectKind.Card), CardFace.FaceDown);

            state.SetFace(CardFace.FaceUp);

            Assert.That(state.Face, Is.EqualTo(CardFace.FaceUp));
        }

        [Test]
        public void PawnState_WhenBaseStateIsPawn_AcceptsBaseState()
        {
            TabletopObjectState baseState = CreateBaseState(TabletopObjectKind.Pawn);
            PawnState state = new PawnState(baseState);

            Assert.That(state.BaseState, Is.SameAs(baseState));
        }

        [Test]
        public void PawnState_WhenBaseStateIsNull_ThrowsArgumentNullException()
        {
            Assert.That(
                () => new PawnState(null),
                Throws.TypeOf<System.ArgumentNullException>());
        }

        [Test]
        public void PawnState_WhenBaseStateIsNotPawn_ThrowsArgumentException()
        {
            TabletopObjectState baseState = CreateBaseState(TabletopObjectKind.Card);

            Assert.That(
                () => new PawnState(baseState),
                Throws.ArgumentException);
        }

        [Test]
        public void TokenState_WhenBaseStateIsToken_AcceptsBaseState()
        {
            TabletopObjectState baseState = CreateBaseState(TabletopObjectKind.Token);
            TokenState state = new TokenState(baseState);

            Assert.That(state.BaseState, Is.SameAs(baseState));
        }

        [Test]
        public void TokenState_WhenBaseStateIsNull_ThrowsArgumentNullException()
        {
            Assert.That(
                () => new TokenState(null),
                Throws.TypeOf<System.ArgumentNullException>());
        }

        [Test]
        public void TokenState_WhenBaseStateIsNotToken_ThrowsArgumentException()
        {
            TabletopObjectState baseState = CreateBaseState(TabletopObjectKind.Card);

            Assert.That(
                () => new TokenState(baseState),
                Throws.ArgumentException);
        }

        private static TabletopObjectState CreateBaseState(TabletopObjectKind kind)
        {
            return new TabletopObjectState(
                TabletopObjectId.New(),
                ObjectDefinitionId.New(),
                kind,
                TabletopPose.Default,
                ContainerId.Empty,
                PlayerId.Empty,
                ObjectVisibility.Public,
                false);
        }
    }
}
