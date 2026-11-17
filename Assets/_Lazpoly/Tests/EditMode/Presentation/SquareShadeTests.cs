using System.Collections.Generic;
using Lazpoly.Core.Board;
using Lazpoly.Presentation.Data;
using Lazpoly.Presentation.Models;
using Lazpoly.Presentation.Tabletop;
using NUnit.Framework;
using UnityEngine;

namespace Lazpoly.Presentation.Tests
{
    /// <summary>
    /// The owner's wish (2026-11-17): while a square is picked on the board the squares that may be picked no longer
    /// light up; the ones that may not go dark, and their buildings with them.
    /// </summary>
    public class SquareShadeTests
    {
        [Test]
        public void DarkeningKeepsHowSeeThroughAColourIs()
        {
            var colour = new Color(0.8f, 0.4f, 0.2f, 0.5f);
            Color dark = SquareShade.Darkened(colour, 0.55f);
            Assert.AreEqual(0.36f, dark.r, 1e-4f);
            Assert.AreEqual(0.18f, dark.g, 1e-4f);
            Assert.AreEqual(0.09f, dark.b, 1e-4f);
            Assert.AreEqual(0.5f, dark.a, 1e-4f);
            Assert.AreEqual(colour, SquareShade.Darkened(colour, 0f));
            Assert.AreEqual(new Color(0f, 0f, 0f, 0.5f), SquareShade.Darkened(colour, 2f), "never past black");
        }

        [Test]
        public void AModelIsDrawnDarkAndGetsItsOwnColoursBack()
        {
            var holder = new GameObject("Shade Test");
            try
            {
                var red = new Color(0.9f, 0.3f, 0.2f);
                GameObject part = ModelParts.Create("Part", holder.transform, MeshFactory.Cylinder(8), red);
                var renderer = part.GetComponent<Renderer>();
                Assume.That(renderer.sharedMaterial, Is.Not.Null, "the model material is not set up here");
                Assert.IsFalse(SquareShade.IsDark(renderer));

                SquareShade.Apply(holder, BoardView.DimAmount);
                Assert.IsTrue(SquareShade.IsDark(renderer), "the part is not darkened");
                Color shown = SquareShade.Shown(renderer, 0).Value;
                Assert.Less(shown.r, red.r);
                Assert.Less(shown.g, red.g);
                AssertSame(red, renderer.sharedMaterial.color, "the material itself keeps its colour");

                SquareShade.Apply(holder, 0f);
                Assert.IsFalse(SquareShade.IsDark(renderer), "the part stayed dark");
                Assert.IsNull(SquareShade.Shown(renderer, 0));
                SquareShade.Apply(null, 0.5f); // Nothing to darken is no error.
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }

        [Test]
        public void AChoiceLeavesItsSquaresAsTheyAreAndDarkensTheRestWithTheirBuildings()
        {
            BoardData data = BoardDataLoader.LoadFromResources();
            var holder = new GameObject("Board Test");
            try
            {
                BoardView board = BoardView.Create(holder.transform, data);
                var towns = new List<int>();
                for (int i = 0; i < data.Board.Count && towns.Count < 2; i++)
                {
                    if (data.Board[i].Type == SquareType.Property && data.Board[i].IsDevelopable) towns.Add(i);
                }

                int picked = towns[0], other = towns[1];
                foreach (int town in towns)
                {
                    board.SetOwner(town, Color.red);
                    board.SetBuildingLevel(town, 2);
                }

                Color pickedTile = TileOf(holder, picked).color, otherTile = TileOf(holder, other).color;
                board.Highlight(new[] { picked }, dimOthers: true);

                Assert.IsFalse(board.IsDimmed(picked));
                AssertSame(pickedTile, TileOf(holder, picked).color, "a square that may be picked is not lit up");
                foreach (GameObject piece in board.BuildingPieces(picked))
                {
                    foreach (Renderer part in piece.GetComponentsInChildren<Renderer>(true)) Assert.IsFalse(SquareShade.IsDark(part), "its buildings keep their colours");
                }

                Assert.IsTrue(board.IsDimmed(other));
                AssertSame(Color.Lerp(otherTile, Color.black, BoardView.DimAmount), TileOf(holder, other).color, "a square that may not be picked goes dark");
                int parts = 0;
                foreach (GameObject piece in board.BuildingPieces(other))
                {
                    foreach (Renderer part in piece.GetComponentsInChildren<Renderer>(true))
                    {
                        if (SquareShade.ColorProperty(part.sharedMaterial) == -1) continue;
                        parts++;
                        Assert.IsTrue(SquareShade.IsDark(part), $"'{part.name}' of a dark town's building is not darkened");
                    }
                }

                Assert.Greater(parts, 0, "the town has no building to darken");
                for (int i = 0; i < data.Board.Count; i++) Assert.AreEqual(i != picked, board.IsDimmed(i), $"square {i}");

                // A hovered group is lit as before and darkens nothing.
                board.Highlight(new[] { picked });
                Assert.IsFalse(board.IsDimmed(other));
                AssertSame(otherTile, TileOf(holder, other).color, "the rest keeps its colour");
                Assert.Greater(TileOf(holder, picked).color.r + TileOf(holder, picked).color.g + TileOf(holder, picked).color.b,
                    pickedTile.r + pickedTile.g + pickedTile.b - 1e-4f, "the hovered town is no darker");

                // Once the choice is over everything has its own colours again.
                board.Highlight(new[] { picked }, dimOthers: true);
                board.Highlight(null);
                AssertSame(pickedTile, TileOf(holder, picked).color, "the picked square after the choice");
                AssertSame(otherTile, TileOf(holder, other).color, "the dark square after the choice");
                foreach (Renderer part in holder.GetComponentsInChildren<Renderer>(true)) Assert.IsFalse(SquareShade.IsDark(part), $"'{part.name}' stayed dark");
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }

        private static void AssertSame(Color expected, Color actual, string what)
        {
            Assert.AreEqual(expected.r, actual.r, 1e-3f, what);
            Assert.AreEqual(expected.g, actual.g, 1e-3f, what);
            Assert.AreEqual(expected.b, actual.b, 1e-3f, what);
            Assert.AreEqual(expected.a, actual.a, 1e-3f, what);
        }

        private static Material TileOf(GameObject holder, int square)
        {
            foreach (Transform child in holder.GetComponentsInChildren<Transform>(true))
            {
                if (child.name.StartsWith($"Square {square:00} ")) return child.GetComponent<Renderer>().sharedMaterial;
            }

            Assert.Fail($"square {square} has no tile");
            return null;
        }
    }
}
