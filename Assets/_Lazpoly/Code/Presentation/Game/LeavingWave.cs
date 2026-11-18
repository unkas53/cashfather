using System.Collections.Generic;
using Lazpoly.Core.Events;
using Lazpoly.Core.State;
using UnityEngine;

namespace Lazpoly.Presentation.Game
{
    /// <summary>
    /// A player's towns going back to the bank in one breath (a bankruptcy) leave one after another, as a wave running
    /// round the board from the Start, not all at once (the owner's wish, 2026-11-18: it gives the moment its weight).
    /// Pure, so it is tested without a scene.
    /// </summary>
    public static class LeavingWave
    {
        /// <summary>The time between one town starting to leave and the next.</summary>
        public const float StepSeconds = 0.22f;

        /// <summary>The longest the last town waits for the first: however many towns, the wave is over in this time.</summary>
        public const float MaxSeconds = 2.4f;

        /// <summary>
        /// How many more towns of the same player go back to the bank straight after <paramref name="gone"/>, from the
        /// events still waiting (<paramref name="next"/>). Only a town's own buildings coming down may stand between
        /// two of them: towns sold one by one, each with its money, are no wave.
        /// </summary>
        public static int Left(PropertyOwnerChangedEvent gone, IEnumerable<GameEvent> next)
        {
            if (gone == null || next == null || gone.NewOwnerId != GameState.NoOwner || gone.PreviousOwnerId == GameState.NoOwner) return 0;
            int left = 0;
            foreach (GameEvent gameEvent in next)
            {
                if (gameEvent is BuildingLevelChangedEvent building && building.Level == 0) continue;
                if (!(gameEvent is PropertyOwnerChangedEvent town) || town.NewOwnerId != GameState.NoOwner || town.PreviousOwnerId != gone.PreviousOwnerId) break;
                left++;
            }

            return left;
        }

        /// <summary>The time between two towns of a wave of <paramref name="towns"/>: <see cref="StepSeconds"/>, or less where they are many.</summary>
        public static float Step(int towns) => towns <= 1 ? 0f : Mathf.Min(StepSeconds, MaxSeconds / (towns - 1));

        /// <summary>
        /// How long the table waits once <paramref name="gone"/> has started to leave before it shows what comes next
        /// (<paramref name="next"/>): a step of the wave while more of the player's towns follow, nothing after the last
        /// or after a town that goes alone. <paramref name="step"/> is the wave's own step, kept from its first town to
        /// its last (0 when no wave is running), so a long wave stays as fast as it started.
        /// </summary>
        public static float Wait(PropertyOwnerChangedEvent gone, IEnumerable<GameEvent> next, ref float step)
        {
            int left = Left(gone, next);
            if (left == 0)
            {
                step = 0f;
                return 0f;
            }

            if (step <= 0f) step = Step(left + 1);
            return step;
        }

        /// <summary>How long after the first town number <paramref name="index"/> (from 0) of a wave of <paramref name="towns"/> starts to leave.</summary>
        public static float StartOf(int index, int towns) => Mathf.Clamp(index, 0, Mathf.Max(0, towns - 1)) * Step(towns);
    }
}
