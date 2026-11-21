using Lazpoly.Core.Board;
using Lazpoly.Core.Events;
using Lazpoly.Core.Rules;
using Lazpoly.Core.State;
using Lazpoly.Presentation.Game;
using NUnit.Framework;

namespace Lazpoly.Presentation.Tests
{
    /// <summary>What the property card may offer: the levels, their prices and rents, and why a level is stamped as unavailable.</summary>
    public class PropertyChoiceTests
    {
        private static readonly int[] Rents = { 50, 150, 400, 700 };

        private static readonly BoardDefinition Board = new BoardDefinition(new[]
        {
            SquareDefinition.Start("Başlangıç"),
            SquareDefinition.Property("Sur Yolu", 60, 4, 1, 50, Rents),
            SquareDefinition.Property("Kuru Yolu", 60, 4, 1, 50, Rents),
            SquareDefinition.Property("Liman", 100, 10, 2),
        });

        private static readonly RulesConfig Rules = new RulesConfig(1000, 100, 6, 3, 2, 50);

        private static GameState State(int money = 1000)
        {
            return GameState.CreateInitial(Board, new RulesConfig(money, 100, 6, 3, 2, 50),
                new[] { new PlayerSeat(1, "Ayşe"), new PlayerSeat(2, "Can") });
        }

        private static void Own(GameState state, int square, int owner) =>
            GameStateReducer.Apply(state, new PropertyOwnerChangedEvent(square, GameState.NoOwner, owner));

        private static void Arrive(GameState state, int player, int square, bool passedStart = false)
        {
            GameStateReducer.Apply(state, new PlayerMovedEvent(player, state.FindPlayer(player).Position, square, passedStart));
            GameStateReducer.Apply(state, new PhaseChangedEvent(GamePhase.TurnEnd));
        }

        private static PropertyChoice Choice(GameState state, int square, bool buying) =>
            PropertyChoice.For(Board, Rules, state, state.FindPlayer(1), square, buying);

        [Test]
        public void BuyingOnTheFirstLapOffersTheLandAndOneHouse()
        {
            GameState state = State();
            Arrive(state, 1, 1);
            PropertyChoice choice = Choice(state, 1, buying: true);

            Assert.IsTrue(choice.Buying);
            Assert.IsTrue(choice.Developable);
            Assert.AreEqual(0, choice.Level);
            Assert.AreEqual(60, choice.Price);
            Assert.AreEqual(1, choice.Allowed, "one house until the start square is passed");
            Assert.AreEqual(1, choice.Most);
            Assert.AreEqual(1, choice.Top);
            Assert.AreEqual(0, choice.DefaultPick, "the land is picked first");
            Assert.IsTrue(choice.AffordsLand);
            Assert.AreEqual(60, choice.CostOf(0));
            Assert.AreEqual(110, choice.CostOf(1), "land plus one house");
            Assert.AreEqual(4, choice.RentAt(0));
            Assert.AreEqual(50, choice.RentAt(1));
            Assert.AreEqual(400, choice.RentAt(3));
            Assert.AreEqual(700, choice.RentAt(4), "the hotel is the fourth level");
            Assert.IsNull(choice.Locked(0));
            Assert.IsNull(choice.Locked(1));
            Assert.AreEqual("İlk turdan sonra", choice.Locked(2));
            Assert.AreEqual("İlk turdan sonra", choice.Locked(3));
            Assert.AreEqual("3 evden sonra", choice.Locked(4));
            Assert.IsFalse(choice.OffersHotel);
            Assert.IsTrue(choice.CanPick(1));
            Assert.IsFalse(choice.CanPick(2));
            Assert.AreEqual(1, choice.Clamp(7));
        }

        [Test]
        public void MoneyStampsTheLevelsItDoesNotReach()
        {
            GameState state = State(money: 70);
            Arrive(state, 1, 1);
            PropertyChoice choice = Choice(state, 1, buying: true);
            Assert.IsTrue(choice.AffordsLand);
            Assert.AreEqual(0, choice.Most, "60 for the land leaves 10, not a house");
            Assert.AreEqual("Para yetmez", choice.Locked(1));
            Assert.AreEqual("İlk turdan sonra", choice.Locked(2), "the rules come before the money");

            PropertyChoice broke = Choice(State(money: 30), 1, buying: true);
            Assert.IsFalse(broke.AffordsLand);
            Assert.AreEqual(0, broke.Most);
            Assert.AreEqual(0, broke.Top);
        }

        [Test]
        public void AfterTheFirstLapAllThreeHousesOpenWithThePurchase()
        {
            GameState state = State();
            Arrive(state, 1, 1, passedStart: true);
            PropertyChoice choice = Choice(state, 1, buying: true);
            Assert.AreEqual(3, choice.Allowed);
            Assert.AreEqual(3, choice.Most);
            Assert.AreEqual(210, choice.CostOf(3));
            Assert.IsNull(choice.Locked(3));
            Assert.AreEqual("3 evden sonra", choice.Locked(4), "the hotel is never bought with the land: it has its own card on a later visit");
            Assert.IsFalse(choice.CanPick(4));
        }

        [Test]
        public void TheWholeGroupRaisesTheBareLandsRent()
        {
            GameState state = State();
            Own(state, 2, 1);
            Arrive(state, 1, 1);
            PropertyChoice choice = Choice(state, 1, buying: true);
            Assert.IsTrue(choice.FullGroup, "buying Sur Yolu completes the group");
            Assert.AreEqual(8, choice.RentAt(0));
            Assert.AreEqual(100, choice.RentAt(1), "houses' rent is doubled too (the owner's wish, Faz 26)");
            Assert.AreEqual(1400, choice.RentAt(4), "and the hotel's");

            Own(state, 1, 1);
            Assert.IsTrue(Choice(state, 1, buying: false).FullGroup);
            Assert.IsFalse(PropertyChoice.For(Board, Rules, state, state.FindPlayer(2), 1, buying: true).FullGroup);
        }

        [Test]
        public void AnOwnedTownOffersTheLevelsAboveItsOwn()
        {
            GameState state = State();
            Own(state, 1, 1);
            Arrive(state, 1, 1, passedStart: true);
            PropertyChoice choice = Choice(state, 1, buying: false);
            Assert.IsFalse(choice.Buying);
            Assert.AreEqual(0, choice.Price);
            Assert.AreEqual(3, choice.Allowed);
            Assert.AreEqual(1, choice.DefaultPick, "one more house is picked to begin with");
            Assert.AreEqual(150, choice.CostOf(3));
            Assert.IsFalse(choice.OffersHotel, "the houses' card");

            GameStateReducer.Apply(state, new BuildingLevelChangedEvent(1, 2));
            choice = Choice(state, 1, buying: false);
            Assert.AreEqual(2, choice.Level);
            Assert.AreEqual(1, choice.Allowed);
            Assert.AreEqual(3, choice.Top);
            Assert.IsFalse(choice.CanPick(1), "what stands cannot be unbuilt here");
            Assert.IsTrue(choice.CanPick(2));
            Assert.AreEqual(0, choice.CostOf(2), "staying put costs nothing");
            Assert.AreEqual(50, choice.CostOf(3));
            Assert.AreEqual(3, choice.DefaultPick);
            Assert.AreEqual("3 evden sonra", choice.Locked(4));
            Assert.IsFalse(choice.CanPick(4), "the houses' card never builds the hotel");
        }

        [Test]
        public void TheHotelWaitsForAFreshVisitToThreeHousesAndHasItsOwnCard()
        {
            GameState state = State();
            Own(state, 1, 1);
            Arrive(state, 1, 1, passedStart: true);
            for (int level = 1; level <= 3; level++) GameStateReducer.Apply(state, new BuildingLevelChangedEvent(1, level));
            PropertyChoice choice = Choice(state, 1, buying: false);
            Assert.AreEqual(3, choice.Level);
            Assert.AreEqual(0, choice.Allowed, "the third house went up this visit");
            Assert.AreEqual("Yeniden gelince", choice.Locked(4));
            Assert.IsFalse(choice.CanPick(4));

            Arrive(state, 1, 1);
            choice = Choice(state, 1, buying: false);
            Assert.IsTrue(choice.OffersHotel, "three houses stand: the card is the hotel's own (the owner's rule, 2026-11-21)");
            Assert.AreEqual(1, choice.Allowed);
            Assert.AreEqual(4, choice.Top);
            Assert.IsNull(choice.Locked(4));
            Assert.IsTrue(choice.CanPick(4));
            Assert.AreEqual(4, choice.DefaultPick);
            Assert.AreEqual(50, choice.CostOf(4));
            Assert.AreEqual(400, choice.RentAt(choice.Level), "the rent now");
            Assert.AreEqual(700, choice.RentAt(4), "the rent with the hotel");

            Assert.IsFalse(Choice(state, 1, buying: true).OffersHotel, "never on the purchase card");
            GameStateReducer.Apply(state, new BuildingLevelChangedEvent(1, 4));
            Assert.IsFalse(Choice(state, 1, buying: false).OffersHotel, "the hotel stands: nothing left to offer");
        }

        [Test]
        public void TheHotelsCardCanBeShownForATownThatIsNotThereYet()
        {
            // The screen tour shows the card on a town with no houses, as if it stood at three and its owner had just come.
            GameState state = State();
            Own(state, 1, 1);
            PropertyChoice staged = PropertyChoice.At(Board, Rules, state, state.FindPlayer(1), 1, false, 3, 1);
            Assert.IsTrue(staged.OffersHotel);
            Assert.AreEqual(3, staged.Level);
            Assert.IsTrue(staged.CanPick(4));
            Assert.AreEqual(50, staged.CostOf(4));
            Assert.AreEqual(0, state.GetBuildingLevel(1), "the table itself is not touched");

            PropertyChoice poor = PropertyChoice.At(Board, Rules, State(money: 20), State(money: 20).FindPlayer(1), 1, false, 3, 1);
            Assert.IsTrue(poor.OffersHotel);
            Assert.IsFalse(poor.CanPick(4), "the card still opens when staged; its button stays shut without the money");
            Assert.AreEqual("Para yetmez", poor.Locked(4));
        }

        [Test]
        public void ASquareWithoutBuildingsShowsTheLandAlone()
        {
            GameState state = State();
            Arrive(state, 1, 3);
            PropertyChoice choice = Choice(state, 3, buying: true);
            Assert.IsFalse(choice.Developable);
            Assert.AreEqual(0, choice.Allowed);
            Assert.AreEqual(0, choice.Top);
            Assert.AreEqual(100, choice.CostOf(0));
            Assert.IsTrue(choice.FullGroup, "Liman is alone in its group, so buying it completes the group");
            Assert.AreEqual(20, choice.RentAt(0), "the whole group doubles the land's rent, as the rules do");
            Assert.AreEqual(20, choice.RentAt(3), "a square without buildings keeps its land rent at every level");
        }

        [Test]
        public void TilesAreNamedByLevel()
        {
            Assert.AreEqual("Arsa", PropertyCard.LevelName(0));
            Assert.AreEqual("Ev 1", PropertyCard.LevelName(1));
            Assert.AreEqual("Ev 3", PropertyCard.LevelName(3));
            Assert.AreEqual("Otel", PropertyCard.LevelName(4));
        }

        [Test]
        public void BuyingAResortOffersOnlyTheLandWithTheRentItWouldEarn()
        {
            var board = new BoardDefinition(new[]
            {
                SquareDefinition.Start("Başlangıç"),
                SquareDefinition.Resort("Bodrum", 200, 50, 9),
                SquareDefinition.Resort("Bali", 200, 50, 9),
                SquareDefinition.Resort("Santorini", 200, 50, 9),
            });
            GameState state = GameState.CreateInitial(board, Rules, new[] { new PlayerSeat(1, "Ayşe"), new PlayerSeat(2, "Can") });
            GameStateReducer.Apply(state, new PlayerMovedEvent(1, 0, 3, true));
            GameStateReducer.Apply(state, new PhaseChangedEvent(GamePhase.TurnEnd));

            PropertyChoice first = PropertyChoice.For(board, Rules, state, state.FindPlayer(1), 3, buying: true);
            Assert.IsTrue(first.Resort);
            Assert.IsFalse(first.Developable);
            Assert.AreEqual(0, first.Most, "no houses on a resort, even after a lap");
            Assert.AreEqual(200, first.CostOf(0));
            Assert.AreEqual(50, first.RentAt(0), "the first resort asks 50");

            GameStateReducer.Apply(state, new PropertyOwnerChangedEvent(1, GameState.NoOwner, 1));
            GameStateReducer.Apply(state, new PropertyOwnerChangedEvent(2, GameState.NoOwner, 2));
            Assert.AreEqual(100, PropertyChoice.For(board, Rules, state, state.FindPlayer(1), 3, buying: true).RentAt(0),
                "with the one already held, each of the two asks 100; the other player's resort does not count");

            GameStateReducer.Apply(state, new PropertyOwnerChangedEvent(3, GameState.NoOwner, 1));
            PropertyChoice owned = PropertyChoice.For(board, Rules, state, state.FindPlayer(1), 3, buying: false);
            Assert.AreEqual(100, owned.RentAt(0), "once bought it is not counted twice");
            Assert.AreEqual(0, owned.Top);
            Assert.IsFalse(Choice(State(), 3, buying: true).Resort, "a town is not a resort");
        }
    }
}
