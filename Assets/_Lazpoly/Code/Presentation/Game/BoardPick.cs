using Lazpoly.Core.Board;
using Lazpoly.Core.Engine;
using Lazpoly.Core.Rules;
using Lazpoly.Core.State;
using Lazpoly.Presentation.UI;
using UnityEngine;
using static Lazpoly.Presentation.Texts.Lang;

namespace Lazpoly.Presentation.Game
{
    /// <summary>
    /// What the pick card says while the person here chooses a square on the board (the owner's wish, 2026-11-17: no
    /// panel at the bottom and a view that stays where it is): a ride's destination from the bus station, the town the
    /// champions' cup goes to, or the town a strategic card is played at. The card stands over the lawn, so it covers
    /// no square; the squares that may be picked are lit on the board and a click on one of them picks it. Pure words:
    /// <see cref="BoardPickView"/> draws them and the game screen sends the command.
    /// </summary>
    public sealed class BoardPick
    {
        /// <summary>The ribbon of a ride (the bus's blue) and of the cup (the kit's brass).</summary>
        public static readonly Color RideTone = new Color(0.318f, 0.659f, 0.812f);
        public static readonly Color CupTone = UiPalette.Ink.Gold;

        public BoardPick(string title, string eyebrow, Color tone, string prompt, string chosen, string note, string confirmLabel, string cancelLabel)
        {
            Title = title;
            Eyebrow = eyebrow;
            Tone = tone;
            Prompt = prompt;
            Chosen = chosen;
            Note = note;
            ConfirmLabel = confirmLabel;
            CancelLabel = cancelLabel;
        }

        /// <summary>The band: what asks (the square's or the card's name), the small line over it, and the ribbon's colour.</summary>
        public string Title { get; }
        public string Eyebrow { get; }
        public Color Tone { get; }

        /// <summary>What to do while nothing is picked.</summary>
        public string Prompt { get; }

        /// <summary>What the picked square means; null while nothing that may be picked is.</summary>
        public string Chosen { get; }

        /// <summary>A warning about the picked square, said in its place (a shield in an attack's way); null without one.</summary>
        public string Note { get; }

        /// <summary>The button that goes through with it, and the one that does not.</summary>
        public string ConfirmLabel { get; }
        public string CancelLabel { get; }

        /// <summary>Whether a square that may be picked is: only then can the pick be confirmed.</summary>
        public bool Ready => Chosen != null;

        /// <summary>The line under the band: the picked square (or the warning about it, which names it), or what to do.</summary>
        public string Line => Chosen == null ? Prompt : Note ?? Chosen;

        /// <summary>
        /// A ride from the travel square at <paramref name="from"/>: the fee in the band, the destination once one of
        /// the squares a ride may go to (<see cref="GameRules.IsRideDestination"/>) is picked.
        /// </summary>
        public static BoardPick Ride(BoardDefinition board, GameState state, PlayerState player, int from, int picked)
        {
            int fee = board[from].Fee;
            bool chosen = GameRules.IsRideDestination(board, state, player, picked);
            return new BoardPick(L(board[from].Name), F($"Yolculuk · Ücret {fee}"), RideTone,
                L("Tahtada parlayan karelerden gitmek istediğine tıkla."),
                chosen ? F($"Gidilecek yer: {L(board[picked].Name)}") : null, null,
                F($"Git ({fee})"), L("Burada kal"));
        }

        /// <summary>
        /// The champions' cup at the cup square <paramref name="from"/>: the raise and the fee in the band, where the cup
        /// stands now, and for a picked town of the player's the rent as it is and as it would be with the cup.
        /// </summary>
        public static BoardPick Cup(BoardDefinition board, RulesConfig rules, GameState state, PlayerState player, int from, int picked)
        {
            int fee = board[from].Fee;
            int percent = GameRules.NextCupPercent(board, state, from);
            bool chosen = GameRules.CanHoldCup(board, picked) && state.GetOwner(picked) == player.Id;
            string prompt = state.CupSquare == GameState.NoCup
                ? L("Tahtada parlayan şehirlerinden birine tıkla.")
                : F($"Şehirlerinden birine tıkla. Kupa şu an {L(board[state.CupSquare].Name)} şehrinde (%{state.CupPercent}).");
            string line = null;
            if (chosen)
            {
                int now = GameRules.CalculateRent(board, rules, state, picked);
                int then = RentWithCup(board, rules, state, picked, percent);
                line = F($"{L(board[picked].Name)}: kira {now} → {then}");
            }

            return new BoardPick(L(board[from].Name), F($"Kira +%{percent} · Ücret {fee}"), CupTone, prompt, line, null, F($"Kupayı getir ({fee})"), L("Geç"));
        }

        /// <summary>A town's rent once the cup stands on it with a raise of <paramref name="percent"/> (rounded down, as the rules do).</summary>
        public static int RentWithCup(BoardDefinition board, RulesConfig rules, GameState state, int square, int percent) =>
            (int)((long)GameRules.RentWithoutCup(board, rules, state, square) * (100 + percent) / 100);
    }
}
