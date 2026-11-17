using System.Linq;
using Lazpoly.Core.Board;
using Lazpoly.Core.Engine;
using Lazpoly.Core.State;
using Lazpoly.Presentation.Data;
using Lazpoly.Presentation.Game;
using Lazpoly.Presentation.Texts;
using NUnit.Framework;
using Language = Lazpoly.Infra.Settings.Language;

namespace Lazpoly.Presentation.Tests
{
    /// <summary>
    /// Faz 51 (the owner's wish, 2026-11-17): what the pick card over the lawn says for a ride from the bus station, for
    /// the champions' cup and for a strategic card's town, on the shipped board.
    /// </summary>
    public class BoardPickTests
    {
        private BoardData _data;

        [SetUp]
        public void SetUp()
        {
            Lang.Set(Language.Turkish);
            _data = BoardDataLoader.LoadFromResources();
        }

        [TearDown]
        public void BackToTurkish() => Lang.Set(Language.Turkish);

        private BoardDefinition Board => _data.Board;

        private int Corner(SquareType type) => Enumerable.Range(0, Board.Count).First(i => Board[i].Type == type);

        private int Town(string name) => Enumerable.Range(0, Board.Count).Single(i => Board[i].Name == name);

        private int Card(CardKind kind) => Enumerable.Range(0, Board.Cards.Count).First(i => Board.Cards[i].Kind == kind);

        /// <summary>A two-player state on the shipped board with the first player on turn, changed through its snapshot.</summary>
        private GameState State(System.Action<GameStateSnapshot> setup)
        {
            GameState fresh = GameState.CreateInitial(Board, _data.Rules, new[] { new PlayerSeat(1, "Ayşe"), new PlayerSeat(2, "Can") });
            GameStateSnapshot snapshot = GameStateSnapshot.Capture(fresh);
            snapshot.CurrentPlayerIndex = 0;
            setup(snapshot);
            return snapshot.Restore();
        }

        [Test]
        public void ARideNamesItsFeeAndThenItsDestination()
        {
            int station = Corner(SquareType.Travel), cup = Corner(SquareType.Cup), paris = Town("Paris");
            int rivals = Enumerable.Range(0, Board.Count).First(i => GameRules.CanHoldCup(Board, i) && i != paris);
            GameState state = State(s =>
            {
                s.Players[0].Position = station;
                s.Owners[rivals] = 2;
                s.Phase = GamePhase.AwaitingTravelDecision;
            });
            PlayerState me = state.CurrentPlayer;
            string fee = Lang.M(Board[station].Fee);

            BoardPick open = BoardPick.Ride(Board, state, me, station, -1);
            Assert.AreEqual(Board[station].Name, open.Title, "the station asks");
            Assert.AreEqual($"Yolculuk · Ücret {fee}", open.Eyebrow);
            Assert.AreEqual(BoardPick.RideTone, open.Tone);
            Assert.IsFalse(open.Ready, "nothing is picked yet");
            Assert.IsNull(open.Chosen);
            Assert.AreEqual(open.Prompt, open.Line, "the card says what to do");
            StringAssert.Contains("parlayan", open.Prompt);
            Assert.AreEqual($"Git ({fee})", open.ConfirmLabel);
            Assert.AreEqual("Burada kal", open.CancelLabel);

            BoardPick picked = BoardPick.Ride(Board, state, me, station, paris);
            Assert.IsTrue(picked.Ready);
            Assert.AreEqual("Gidilecek yer: Paris", picked.Line);
            Assert.IsNull(picked.Note);

            Assert.IsFalse(BoardPick.Ride(Board, state, me, station, rivals).Ready, "a rival's town is no destination");
            Assert.IsFalse(BoardPick.Ride(Board, state, me, station, cup).Ready, "nor is the cup's corner (the owner's rule, Faz 31)");
            Assert.IsFalse(BoardPick.Ride(Board, state, me, station, station).Ready, "nor the station itself");
            Assert.IsFalse(BoardPick.Ride(Board, state, me, station, Board.Count).Ready, "nor a square off the board");

            Lang.Set(Language.English);
            Assert.AreEqual("Destination: Paris", BoardPick.Ride(Board, state, me, station, paris).Line);
            Assert.AreEqual("Stay here", BoardPick.Ride(Board, state, me, station, paris).CancelLabel);
        }

        [Test]
        public void TheCupShowsTheRentBeforeAndAfter()
        {
            int corner = Corner(SquareType.Cup), paris = Town("Paris");
            int other = Enumerable.Range(0, Board.Count).First(i => GameRules.CanHoldCup(Board, i) && i != paris);
            int resort = Enumerable.Range(0, Board.Count).First(i => Board[i].IsResort);
            GameState state = State(s =>
            {
                s.Players[0].Position = corner;
                s.Owners[paris] = 1;
                s.Owners[other] = 2;
                s.Owners[resort] = 1;
                s.Phase = GamePhase.AwaitingCupChoice;
            });
            PlayerState me = state.CurrentPlayer;
            string fee = Lang.M(Board[corner].Fee);
            int rent = GameRules.CalculateRent(Board, _data.Rules, state, paris);
            Assert.Greater(rent, 0);

            BoardPick open = BoardPick.Cup(Board, _data.Rules, state, me, corner, -1);
            Assert.AreEqual(Board[corner].Name, open.Title);
            Assert.AreEqual($"Kira +%50 · Ücret {fee}", open.Eyebrow, "the first pick raises the rent by half");
            Assert.AreEqual(BoardPick.CupTone, open.Tone);
            Assert.IsFalse(open.Ready);
            Assert.AreEqual("Tahtada parlayan şehirlerinden birine tıkla.", open.Line);
            Assert.AreEqual($"Kupayı getir ({fee})", open.ConfirmLabel);
            Assert.AreEqual("Geç", open.CancelLabel);

            BoardPick picked = BoardPick.Cup(Board, _data.Rules, state, me, corner, paris);
            Assert.IsTrue(picked.Ready);
            Assert.AreEqual($"Paris: kira {Lang.M(rent)} → {Lang.M(rent * 150 / 100)}", picked.Line);
            Assert.AreEqual(rent * 150 / 100, BoardPick.RentWithCup(Board, _data.Rules, state, paris, 50));

            Assert.IsFalse(BoardPick.Cup(Board, _data.Rules, state, me, corner, other).Ready, "a rival's town cannot take the cup from here");
            Assert.IsFalse(BoardPick.Cup(Board, _data.Rules, state, me, corner, resort).Ready, "nor can a factory city");

            // Once the cup stands somewhere the card says where, and the next pick raises more.
            GameState later = State(s =>
            {
                s.Players[0].Position = corner;
                s.Owners[paris] = 1;
                s.Owners[other] = 2;
                s.CupSquare = other;
                s.CupPercent = 50;
                s.CupPicks = 1;
                s.Phase = GamePhase.AwaitingCupChoice;
            });
            BoardPick moved = BoardPick.Cup(Board, _data.Rules, later, later.CurrentPlayer, corner, -1);
            Assert.AreEqual($"Kira +%100 · Ücret {fee}", moved.Eyebrow);
            StringAssert.Contains(Board[other].Name, moved.Line);
            StringAssert.Contains("%50", moved.Line);
            Assert.AreEqual($"Paris: kira {Lang.M(rent)} → {Lang.M(rent * 2)}", BoardPick.Cup(Board, _data.Rules, later, later.CurrentPlayer, corner, paris).Line);
        }

        [Test]
        public void ACardsTownIsPickedOnTheSameCard()
        {
            int paris = Town("Paris");
            int mine = Enumerable.Range(0, Board.Count).First(i => GameRules.CanHoldCup(Board, i) && i != paris);
            int shield = Card(CardKind.Shield), demolish = Card(CardKind.Demolish);
            GameState state = State(s =>
            {
                s.Owners[paris] = 2;
                s.Owners[mine] = 1;
                s.Shields[paris] = true;
                // Both cards are in the hand of the player on turn, who has not rolled yet: only then may they be played.
                s.Players[0].Hand.Add(shield);
                s.Players[0].Hand.Add(demolish);
            });

            BoardPick open = CardHand.PickFor(Board, state, shield, -1);
            Assert.AreEqual(CardPlay.Name(Board, shield), open.Title, "the card asks");
            Assert.AreEqual(CardArt.Family(CardKind.Shield), open.Eyebrow);
            Assert.IsFalse(open.Ready);
            Assert.AreEqual("Tahtada parlayan şehirlerinden birine tıkla.", open.Line);
            Assert.AreEqual("Oyna", open.ConfirmLabel);
            Assert.AreEqual("Vazgeç", open.CancelLabel);
            Assert.IsFalse(CardHand.PickFor(Board, state, shield, paris).Ready, "a shield goes on one's own town");

            BoardPick own = CardHand.PickFor(Board, state, shield, mine);
            Assert.IsTrue(own.Ready);
            Assert.AreEqual(Board[mine].Name, own.Line);
            StringAssert.Contains(Board[mine].Name, own.ConfirmLabel);

            // An attack on a shielded town says so under the town's name.
            BoardPick attack = CardHand.PickFor(Board, state, demolish, paris);
            Assert.IsTrue(attack.Ready);
            Assert.IsNotNull(attack.Note);
            Assert.AreEqual("Paris\n" + attack.Note, attack.Line);
            Assert.Greater(attack.Tone.r, CardArt.ToneColor(CardArt.ToneFor(CardKind.Demolish)).r, "the ribbon is lighter than the family's deep colour");
        }
    }
}
