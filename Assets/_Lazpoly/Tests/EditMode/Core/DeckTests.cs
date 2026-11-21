using System;
using System.Collections.Generic;
using System.Linq;
using Lazpoly.Core.Board;
using Lazpoly.Core.Commands;
using Lazpoly.Core.Engine;
using Lazpoly.Core.Events;
using Lazpoly.Core.Random;
using Lazpoly.Core.State;
using NUnit.Framework;
using static Lazpoly.Core.Tests.TestData;

namespace Lazpoly.Core.Tests
{
    /// <summary>
    /// The cards are one deck (the owner's rule, 2026-11-21): a card that came does not come again before the deck
    /// runs out, then the deck is shuffled and every card not in a hand may come once more.
    /// </summary>
    public class DeckTests
    {
        private const int Plain = 7;
        private const int RentShield = 7;
        private const int Shield = 8;
        private const int Cards = 9;

        // 0 Start, every other square a card square: whatever the dice show, a card is drawn (or Start is passed over).
        private static BoardDefinition Board(bool kept)
        {
            var squares = new SquareDefinition[12];
            squares[0] = SquareDefinition.Start("Start");
            for (int i = 1; i < squares.Length; i++) squares[i] = SquareDefinition.Card($"Chance {i}");

            var cards = new List<CardDefinition>();
            for (int i = 0; i < Plain; i++) cards.Add(CardDefinition.Collect($"Prize {i}", i + 1));
            if (kept)
            {
                cards.Add(CardDefinition.Strategic("Rent shield", CardKind.RentShield));
                cards.Add(CardDefinition.Strategic("Shield", CardKind.Shield));
            }

            return new BoardDefinition(squares, cards);
        }

        /// <summary>Plays on with whatever is legal and plain (keeps a kept card, lets the oldest go) until <paramref name="draws"/> cards came.</summary>
        private static List<GameEvent> Play(GameEngine engine, int draws)
        {
            var events = new List<GameEvent>();
            for (int step = 0; step < 10000 && events.OfType<CardDrawnEvent>().Count() < draws; step++)
            {
                int me = engine.State.CurrentPlayer.Id;
                GameCommand command;
                switch (engine.State.Phase)
                {
                    case GamePhase.WaitingForRoll: command = new RollDiceCommand(me); break;
                    case GamePhase.AwaitingCardUse: command = new KeepCardCommand(me); break;
                    case GamePhase.AwaitingCardDiscard: command = new DiscardCardCommand(me, engine.State.CurrentPlayer.Hand[0]); break;
                    default: command = new EndTurnCommand(me); break;
                }

                CommandResult result = engine.Execute(command);
                Assert.IsTrue(result.IsAccepted, $"{command.GetType().Name} in {engine.State.Phase}: {result.Reason}");
                events.AddRange(result.Events);
            }

            return events;
        }

        /// <summary>The cards that came, deck by deck: a new list starts at every shuffle.</summary>
        private static List<List<int>> Decks(IEnumerable<GameEvent> events)
        {
            var decks = new List<List<int>> { new List<int>() };
            foreach (GameEvent gameEvent in events)
            {
                if (gameEvent is DeckShuffledEvent) decks.Add(new List<int>());
                else if (gameEvent is CardDrawnEvent drawn) decks[decks.Count - 1].Add(drawn.CardIndex);
            }

            return decks;
        }

        [Test]
        public void EveryCardComesOnceBeforeAnyComesAgain()
        {
            for (ulong seed = 1; seed <= 25; seed++)
            {
                var engine = new GameEngine(Board(kept: false), TestRules(), TestSeats(3), new Pcg32Random(seed));
                List<List<int>> decks = Decks(Play(engine, Plain * 4));

                Assert.AreEqual(4, decks.Count, $"seed {seed}: four times through the deck, three shuffles");
                for (int i = 0; i < decks.Count; i++)
                {
                    CollectionAssert.AreEquivalent(Enumerable.Range(0, Plain), decks[i], $"seed {seed}, deck {i}: each card once");
                    if (i > 0) Assert.AreNotEqual(decks[i - 1].Last(), decks[i][0], $"seed {seed}: the same card twice running across a shuffle");
                }
            }
        }

        [Test]
        public void TheOrderOfADeckIsNotTheSameEveryTime()
        {
            var orders = new HashSet<string>();
            for (ulong seed = 1; seed <= 25; seed++)
            {
                var engine = new GameEngine(Board(kept: false), TestRules(), TestSeats(2), new Pcg32Random(seed));
                orders.Add(string.Join(",", Decks(Play(engine, Plain))[0]));
            }

            Assert.Greater(orders.Count, 20, "A shuffled deck: the cards come in a different order from game to game.");
        }

        [Test]
        public void KeptAndPlayedCardsStayOutUntilTheDeckIsShuffled()
        {
            for (ulong seed = 1; seed <= 25; seed++)
            {
                var engine = new GameEngine(Board(kept: true), TestRules(), TestSeats(2), new Pcg32Random(seed));
                List<GameEvent> events = Play(engine, Cards * 5);
                foreach (List<int> deck in Decks(events))
                {
                    CollectionAssert.AllItemsAreUnique(deck, $"seed {seed}: a card came twice in one deck");
                }

                // Followed event by event: a card never comes while it is in a hand.
                var held = new HashSet<int>();
                foreach (GameEvent gameEvent in events)
                {
                    switch (gameEvent)
                    {
                        case CardDrawnEvent drawn:
                            Assert.IsFalse(held.Contains(drawn.CardIndex), $"seed {seed}: card {drawn.CardIndex} came while held");
                            if (drawn.Kept) held.Add(drawn.CardIndex);
                            break;
                        case CardDiscardedEvent gone: held.Remove(gone.CardIndex); break;
                        case CardPlayedEvent played: held.Remove(played.CardIndex); break;
                    }
                }
            }
        }

        // Alice on Start with the dice 1 and 2 scripted: she lands on a card square.
        private static GameEngine Restored(Action<GameStateSnapshot> setup, params int[] dice)
        {
            BoardDefinition board = Board(kept: true);
            var fresh = new GameEngine(board, TestRules(), TestSeats(2), new ScriptedDice());
            GameStateSnapshot snapshot = GameStateSnapshot.Capture(fresh.State);
            setup(snapshot);
            return GameEngine.Restore(board, TestRules(), snapshot, new ScriptedDice(dice));
        }

        [Test]
        public void TheLastCardOfTheDeckComesThenTheDeckIsShuffledWithoutIt()
        {
            GameEngine engine = Restored(s =>
            {
                s.DrawnCards.AddRange(new[] { 0, 1, 3, 4, 5, 6, RentShield, Shield });
                s.LastDrawnCard = 5;
            }, 1, 2, 0, 1, 2, 2);

            CollectionAssert.AreEqual(new[] { 2 }, GameRules.DrawableCards(engine.Board, engine.State));
            CommandResult last = engine.Execute(new RollDiceCommand(Alice));
            Assert.AreEqual(2, last.Events.OfType<CardDrawnEvent>().Single().CardIndex, "the one card left");
            Assert.IsFalse(last.Events.Any(e => e is DeckShuffledEvent), "the deck is shuffled when a card is needed, not before");
            Assert.AreEqual(0, GameRules.DrawableCards(engine.Board, engine.State).Count);
            Assert.AreEqual(2, engine.State.LastDrawnCard);

            engine.Execute(new EndTurnCommand(Alice));
            CommandResult first = engine.Execute(new RollDiceCommand(Bob));
            List<GameEvent> events = first.Events.ToList();
            int shuffled = events.FindIndex(e => e is DeckShuffledEvent);
            int drawn = events.FindIndex(e => e is CardDrawnEvent);
            Assert.IsTrue(shuffled >= 0 && shuffled < drawn, "shuffled, then drawn");
            Assert.AreEqual(3, ((CardDrawnEvent)events[drawn]).CardIndex, "the third of the new deck, which begins without card 2: 0, 1, 3");
            CollectionAssert.AreEqual(new[] { 3 }, engine.State.DrawnCards);
            CollectionAssert.Contains(GameRules.DrawableCards(engine.Board, engine.State), 2, "it is in the new deck all the same");
        }

        [Test]
        public void ACardInAHandIsNotShuffledBackIn()
        {
            GameEngine engine = Restored(s =>
            {
                s.Players[1].Hand.Add(Shield);
                s.DrawnCards.AddRange(Enumerable.Range(0, Cards));
                s.LastDrawnCard = 0;
            }, 1, 2, 6);

            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5, 6, RentShield }, GameRules.ShuffledDeck(engine.Board, engine.State, engine.State.LastDrawnCard));
            CommandResult result = engine.Execute(new RollDiceCommand(Alice));
            Assert.AreEqual(RentShield, result.Events.OfType<CardDrawnEvent>().Single().CardIndex);
            Assert.IsTrue(result.Events.OfType<CardDrawnEvent>().Single().Kept);
        }

        [Test]
        public void ADeckOfOneCardStillDeals()
        {
            var board = new BoardDefinition(
                new[] { SquareDefinition.Start("Start"), SquareDefinition.Card("Chance"), SquareDefinition.Card("Chance"), SquareDefinition.Card("Chance") },
                new[] { CardDefinition.Collect("Prize", 5) });
            var engine = new GameEngine(board, TestRules(), TestSeats(2), new ScriptedDice(1, 2, 0, 1, 2, 0));
            Assert.AreEqual(0, engine.Execute(new RollDiceCommand(Alice)).Events.OfType<CardDrawnEvent>().Single().CardIndex);
            engine.Execute(new EndTurnCommand(Alice));
            CommandResult again = engine.Execute(new RollDiceCommand(Bob));
            Assert.IsTrue(again.Events.Any(e => e is DeckShuffledEvent));
            Assert.AreEqual(0, again.Events.OfType<CardDrawnEvent>().Single().CardIndex, "The only card there is: it comes again.");
        }

        [Test]
        public void TheDeckIsPartOfTheStateThatIsSavedSentAndCompared()
        {
            var engine = new GameEngine(Board(kept: true), TestRules(), TestSeats(2), new Pcg32Random(7));
            Play(engine, 5);
            Assert.AreEqual(5, engine.State.DrawnCards.Count);

            GameStateSnapshot snapshot = GameStateSnapshot.Capture(engine.State);
            CollectionAssert.AreEqual(engine.State.DrawnCards, snapshot.DrawnCards);
            GameState restored = snapshot.Restore();
            CollectionAssert.AreEqual(engine.State.DrawnCards, restored.DrawnCards);
            Assert.AreEqual(engine.State.LastDrawnCard, restored.LastDrawnCard);
            Assert.AreEqual(engine.State.ComputeHash(), restored.ComputeHash());

            ulong before = restored.ComputeHash();
            GameStateReducer.Apply(restored, new DeckShuffledEvent());
            Assert.AreEqual(0, restored.DrawnCards.Count);
            Assert.AreNotEqual(before, restored.ComputeHash(), "A table whose deck differs is out of step.");

            snapshot.DrawnCards.Add(snapshot.DrawnCards[0]);
            Assert.Throws<ArgumentException>(() => snapshot.Restore(), "A card listed twice.");
            snapshot.DrawnCards[snapshot.DrawnCards.Count - 1] = -4;
            Assert.Throws<ArgumentException>(() => snapshot.Restore());
        }

        [Test]
        public void AnOlderSnapshotHasAWholeDeck()
        {
            var engine = new GameEngine(Board(kept: true), TestRules(), TestSeats(2), new ScriptedDice());
            GameStateSnapshot snapshot = GameStateSnapshot.Capture(engine.State);
            snapshot.DrawnCards = null;
            GameState restored = snapshot.Restore();
            Assert.AreEqual(0, restored.DrawnCards.Count);
            Assert.AreEqual(GameState.NoCard, restored.LastDrawnCard);
            Assert.AreEqual(Cards, GameRules.DrawableCards(engine.Board, restored).Count);
        }
    }
}
