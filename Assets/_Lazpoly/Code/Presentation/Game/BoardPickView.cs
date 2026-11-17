using System;
using Lazpoly.Presentation.Tabletop;
using Lazpoly.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;
using static Lazpoly.Presentation.Texts.Lang;

namespace Lazpoly.Presentation.Game
{
    /// <summary>
    /// The pick screen (the owner's wish, 2026-11-17: a nicer one, with a view that does not draw back): a small card
    /// in the look of the game's deeds over the lawn in the middle of the board, where it covers no square, and a name
    /// plate over the square the mouse is on and over the picked one, as the sale screen's plates stand over the towns.
    /// The card's band says what asks and what it costs (<see cref="BoardPick"/>), the line under it what to do or
    /// what was picked, and its two buttons go through with the pick or leave it. The plates take no clicks: the
    /// squares under them do.
    /// </summary>
    public sealed class BoardPickView
    {
        /// <summary>The card's width, its padding and the height of the line under its band, in reference pixels: small enough to stay on the lawn at every supported screen.</summary>
        public const float CardWidth = 400f;
        public const int CardPadding = 14;
        public const float LineHeight = 36f;

        /// <summary>The card's height: its band, its line and its buttons, with the gaps between and the padding round them.</summary>
        public const float CardHeight = 2f * CardPadding + PropertyCard.BandHeight + LineHeight + UiLayout.CardButtonHeight + 2f * UiTheme.Spacing;

        /// <summary>A name plate's size and the letter size of the name on it.</summary>
        public const float TagWidth = 156f;
        public const float TagHeight = 38f;
        public const int TagFontSize = 18;

        private const int NoSquare = -1;

        private readonly GameSceneController _controller;
        private readonly RectTransform _tagLayer;
        private readonly RectTransform _card;
        private readonly UiView _cardView;
        private readonly Plate _pickedPlate;
        private readonly Plate _hoverPlate;
        private BoardPick _pick;
        private bool _allowed;
        private Action _confirm;
        private Action _cancel;

        public BoardPickView(Transform canvas, GameSceneController controller)
        {
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
            // The plates lie under the card, as the sale screen's do.
            _tagLayer = UiRoot.Stretch(canvas, "Pick Tags");
            _hoverPlate = new Plate(_tagLayer, "Pick tag (pointed at)", picked: false);
            _pickedPlate = new Plate(_tagLayer, "Pick tag (picked)", picked: true);
            _card = UiRoot.Rect(canvas, "Pick Card", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            _card.pivot = new Vector2(0.5f, 0.5f);
            _card.sizeDelta = new Vector2(CardWidth, 0f);
            UiRoot.Panel(_card);
            UiBuilder.AddColumnLayout(_card.gameObject, CardPadding, UiTheme.Spacing);
            _card.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _cardView = new UiView(new UiBuilder(_card), RenderCard);
            _tagLayer.gameObject.SetActive(false);
            _card.gameObject.SetActive(false);
        }

        /// <summary>Whether the card is up.</summary>
        public bool Shown => _card.gameObject.activeSelf;

        /// <summary>The card itself.</summary>
        public RectTransform Card => _card;

        /// <summary>What the card says now; null while it is away.</summary>
        public BoardPick Pick => Shown ? _pick : null;

        /// <summary>The square the picked plate stands over, and the one the pointed-at plate does; -1 for a plate that is away.</summary>
        public int PickedSquare => _pickedPlate.Square;
        public int PointedSquare => _hoverPlate.Square;

        /// <summary>
        /// Shows the screen for <paramref name="pick"/> (null puts it away): the card with its words and buttons, the
        /// picked plate over <paramref name="picked"/> once that square may be picked, and the other plate over
        /// <paramref name="pointed"/>, the square that may be picked under the mouse (-1: none). The pick can be
        /// confirmed while a square is picked and the rules allow the move (<paramref name="allowed"/>).
        /// </summary>
        public void Update(BoardPick pick, int picked, int pointed, bool allowed, Action confirm, Action cancel)
        {
            if (pick == null)
            {
                if (Shown || _tagLayer.gameObject.activeSelf) Hide();
                return;
            }

            _pick = pick;
            _allowed = allowed;
            _confirm = confirm;
            _cancel = cancel;
            if (!Shown) _card.gameObject.SetActive(true);
            if (!_tagLayer.gameObject.activeSelf) _tagLayer.gameObject.SetActive(true);
            _cardView.Refresh(HashCode.Combine(pick.Title, pick.Eyebrow, pick.Tone, pick.Line, pick.ConfirmLabel, pick.CancelLabel, pick.Ready && allowed));
            int chosen = pick.Ready ? picked : NoSquare;
            Stand(_pickedPlate, chosen);
            Stand(_hoverPlate, pointed == chosen ? NoSquare : pointed);
        }

        private void Hide()
        {
            _card.gameObject.SetActive(false);
            _tagLayer.gameObject.SetActive(false);
            _pickedPlate.Square = NoSquare;
            _hoverPlate.Square = NoSquare;
            _pick = null;
            _confirm = null;
            _cancel = null;
        }

        private void RenderCard(UiBuilder ui)
        {
            if (_pick == null) return;
            PropertyCard.Band(ui, _pick.Title, _pick.Eyebrow, _pick.Tone, null, null);
            Text line = ui.Label(_pick.Line, UiTextStyle.Small, UiTheme.Text, TextAnchor.MiddleCenter);
            line.font = UiTheme.BoldFont;
            UiBuilder.Fix(line, height: LineHeight);
            UiBuilder.ShrinkToFit(line); // A long town name gets smaller, never a third line.
            // The buttons do whatever the screen was last given, so a card drawn for one square never confirms another.
            ui.Actions(_pick.CancelLabel, () => _cancel?.Invoke(), _pick.ConfirmLabel, () => _confirm?.Invoke(), _pick.Ready && _allowed);
        }

        /// <summary>Puts a plate over a square of the board with the square's name on it, its point on the square; away for no square or one behind the view.</summary>
        private void Stand(Plate plate, int square)
        {
            BoardView board = _controller.Board;
            Camera camera = Camera.main;
            bool up = square >= 0 && board != null && camera != null && square < _controller.Session.Board.Count;
            Vector3 screen = up ? camera.WorldToScreenPoint(board.SquarePosition(square) + Vector3.up * DebtSaleView.TagLift) : Vector3.zero;
            up = up && screen.z > 0f;
            plate.Square = up ? square : NoSquare;
            if (plate.Root.gameObject.activeSelf != up) plate.Root.gameObject.SetActive(up);
            if (!up) return;
            if (plate.Named != square)
            {
                plate.Named = square;
                plate.Name.text = Upper(L(_controller.Session.Board[square].Name));
            }

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_tagLayer, screen, null, out Vector2 local))
            {
                plate.Root.anchoredPosition = local + new Vector2(0f, DebtSaleView.PointerReach);
            }
        }

        /// <summary>
        /// A name plate with a point down to its square, in the look of the sale screen's plates: cream with a thin edge
        /// for the square under the mouse; pale green, ringed in the gain colour and with the kit's gold tick before the
        /// name for the picked one.
        /// </summary>
        private sealed class Plate
        {
            public readonly RectTransform Root;
            public readonly Text Name;

            /// <summary>The square the plate stands over (-1: away), and the one whose name it carries.</summary>
            public int Square = NoSquare;
            public int Named = NoSquare;

            public Plate(RectTransform layer, string name, bool picked)
            {
                Root = UiRoot.Rect(layer, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                Root.pivot = new Vector2(0.5f, 0f);
                Root.sizeDelta = new Vector2(TagWidth, TagHeight);
                Color fill = picked ? DebtSaleView.PickedFill : UiTheme.Card;
                UiRoot.Background(Root, fill, shadow: true).raycastTarget = false;
                DebtSaleView.AddPointer(Root, fill);
                Color dark = UiTheme.Text;
                DebtSaleView.Edge(Root, picked ? UiTheme.Gain : new Color(dark.r, dark.g, dark.b, DebtSaleView.EdgeAlpha));

                var row = Root.gameObject.AddComponent<HorizontalLayoutGroup>();
                row.padding = new RectOffset(10, 10, 0, 0);
                row.spacing = 6f;
                row.childAlignment = TextAnchor.MiddleCenter;
                row.childControlWidth = true;
                row.childControlHeight = true;
                row.childForceExpandWidth = false;
                row.childForceExpandHeight = false;

                if (picked)
                {
                    float side = TagHeight - 14f;
                    RectTransform box = UiRoot.Rect(Root, "Box", Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
                    UiBuilder.Fix(box.gameObject, side, side);
                    UiRoot.Background(box, UiPalette.Ink.MastheadDeep).raycastTarget = false;
                    RectTransform mark = UiRoot.Rect(box, "Check", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                    mark.sizeDelta = new Vector2(side * 0.6f, side * 0.6f);
                    Sprite tick = KitArt.Get(UiBuilder.CheckIcon);
                    Image sign = UiRoot.Background(mark, UiPalette.Ink.Gold, rounded: tick == null);
                    if (tick != null) sign.sprite = tick;
                    sign.raycastTarget = false;
                }

                Name = new UiBuilder(Root).Label("", UiTextStyle.Body, picked ? UiTheme.Gain : UiTheme.Text, TextAnchor.MiddleCenter);
                Name.font = UiTheme.BlackFont;
                Name.fontSize = TagFontSize;
                Name.raycastTarget = false;
                UiBuilder.Fix(Name, height: TagHeight - 10f).flexibleWidth = 1f;
                UiBuilder.ShrinkToFit(Name);
                Root.gameObject.SetActive(false);
            }
        }
    }
}
