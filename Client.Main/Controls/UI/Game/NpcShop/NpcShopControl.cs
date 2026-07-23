using System;
using System.Collections.Generic;
using System.Linq;
using Client.Main;
using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Core.Client;
using Client.Main.Core.Utilities;
using Client.Main.Controls.UI.Common;
using Client.Main.Controls.UI.Game.Common;
using Client.Main.Controls.UI.Game.Inventory;
using Client.Main.Controls.UI;
using Client.Main.Models;
using Client.Main.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Client.Main.Controls.UI.Game
{
    public class NpcShopControl : UIControl
    {
        // ═══════════════════════════════════════════════════════════════
        // SHOP MODE
        // ═══════════════════════════════════════════════════════════════
        public enum ShopMode
        {
            BuyAndSell = 1,
            Repair = 2
        }

        // ═══════════════════════════════════════════════════════════════
        // WINDOW DIMENSIONS
        // ═══════════════════════════════════════════════════════════════
        // 8 colunas = stride do protocolo (slot = y*8 + x); linhas/colunas ajustáveis pelo editor.
        private static int SHOP_COLUMNS => Hud.NpcShopLayout.GridCols;
        private static int SHOP_ROWS => Hud.NpcShopLayout.GridRows;
        private const int SHOP_SQUARE_WIDTH = 32;
        private const int SHOP_SQUARE_HEIGHT = 32;

        private static int GRID_WIDTH => SHOP_COLUMNS * SHOP_SQUARE_WIDTH;
        private static int GRID_HEIGHT => SHOP_ROWS * SHOP_SQUARE_HEIGHT;
        // Dimensões dirigidas pelo NpcShopLayout (editor hud-edit-npcshop :5190).
        private static int WINDOW_WIDTH => Hud.NpcShopLayout.PanelW;
        private int WindowHeight => Hud.NpcShopLayout.PanelH;
        private static int HEADER_HEIGHT => Hud.NpcShopLayout.DragBarH;

        // ═══════════════════════════════════════════════════════════════
        // MODERN DARK THEME
        // ═══════════════════════════════════════════════════════════════
        private static class Theme
        {
            public static readonly Color BgDarkest = new(8, 10, 14, 252);
            public static readonly Color BgDark = new(16, 20, 26, 250);
            public static readonly Color BgMid = new(24, 30, 38, 248);
            public static readonly Color BgLight = new(35, 42, 52, 245);

            public static readonly Color Accent = new(212, 175, 85);
            public static readonly Color AccentBright = new(255, 215, 120);
            public static readonly Color AccentDim = new(140, 115, 55);
            public static readonly Color AccentGlow = new(255, 200, 80, 40);

            public static readonly Color BorderOuter = new(5, 6, 8, 255);
            public static readonly Color BorderInner = new(60, 70, 85, 200);
            public static readonly Color BorderHighlight = new(100, 110, 130, 120);

            public static readonly Color SlotBg = new(12, 15, 20, 240);
            public static readonly Color SlotBorder = new(45, 52, 65, 180);
            public static readonly Color SlotHover = new(70, 85, 110, 150);
            public static readonly Color SlotSelected = new(212, 175, 85, 100);

            public static readonly Color GlowNormal = new(150, 150, 150, 25);
            public static readonly Color GlowMagic = new(100, 150, 255, 50);
            public static readonly Color GlowExcellent = new(120, 255, 120, 60);
            public static readonly Color GlowAncient = new(80, 200, 255, 70);
            public static readonly Color GlowLegendary = new(255, 180, 80, 70);

            public static readonly Color TextWhite = new(240, 240, 245);
            public static readonly Color TextGold = new(255, 220, 130);
            public static readonly Color TextGray = new(160, 165, 175);
        }

        private static readonly ItemGlowPalette GlowPalette = new(
            Theme.GlowNormal,
            Theme.GlowMagic,
            Theme.GlowExcellent,
            Theme.GlowAncient,
            Theme.GlowLegendary);

        private static NpcShopControl _instance;

        private readonly List<InventoryItem> _items = new();
        private readonly Dictionary<string, Texture2D> _itemTextureCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<(InventoryItem item, int width, int height, bool animated), Texture2D> _bmdPreviewCache = new();

        private Rectangle _headerRect;
        private Rectangle _gridRect;
        private Rectangle _gridFrameRect;
        private Rectangle _buttonAreaRect;
        private Rectangle _footerRect;
        private Rectangle _closeButtonRect;
        private Rectangle _repairButtonRect;
        private Rectangle _repairAllButtonRect;
        private bool _repairButtonHovered;
        private bool _repairAllButtonHovered;

        private RenderTarget2D _staticSurface;
        private bool _staticSurfaceDirty = true;

        // Assets do visual novo (todos já existentes no jogo — sem patch).
        private Texture2D _texPanel, _texTab, _texRect, _texDarkCard, _texGridRow;
        private Texture2D _texClose, _texCloseHover, _texBtnDark, _texBtnGold, _texOkCancel, _texTooltip;
        private Rectangle _pageTabRect, _bottomBtnRect;
        private bool _bottomBtnHovered;

        private SpriteFont _font;
        private CharacterState _characterState;

        private InventoryItem _hoveredItem;
        private Point _hoveredSlot = new(-1, -1);
        private GameTime _currentGameTime;

        private bool _wasVisible;
        private bool _closeRequestSent;
        private bool _closeHovered;
        private bool _pendingShow;
        private bool _warmupComplete;

        // Drag support
        private bool _isDragging;
        private Point _dragOffset;
        private DateTime _lastClickTime = DateTime.MinValue;

        // Repair mode
        private ShopMode _shopMode = ShopMode.BuyAndSell;
        private bool _isRepairShop = false;

        private NpcShopControl()
        {
            BuildLayoutMetrics();

            ControlSize = new Point(WINDOW_WIDTH, WindowHeight);
            ViewSize = ControlSize;
            AutoViewSize = false;
            Interactive = true;
            Visible = false;
            Align = ControlAlign.VerticalCenter | ControlAlign.Left;

            EnsureCharacterState();
        }

        public override bool NonDisposable => true;
        public static NpcShopControl Instance => _instance ??= new NpcShopControl();
        public static bool IsOpen => _instance?.Visible == true;

        /// <summary>
        /// Forces immediate position calculation based on Align property.
        /// Call this before showing the control to prevent position flickering.
        /// </summary>
        private void ForceAlignNow()
        {
            if (Parent == null || Align == ControlAlign.None)
                return;

            const int padding = 20;

            if (Align.HasFlag(ControlAlign.Top))
                Y = padding;
            else if (Align.HasFlag(ControlAlign.Bottom))
                Y = Parent.DisplaySize.Y - DisplaySize.Y - padding;
            else if (Align.HasFlag(ControlAlign.VerticalCenter))
                Y = (Parent.DisplaySize.Y / 2) - (DisplaySize.Y / 2);

            if (Align.HasFlag(ControlAlign.Left))
                X = padding;
            else if (Align.HasFlag(ControlAlign.Right))
                X = Parent.DisplaySize.X - DisplaySize.X - padding;
            else if (Align.HasFlag(ControlAlign.HorizontalCenter))
                X = (Parent.DisplaySize.X / 2) - (DisplaySize.X / 2);
        }

        private void BuildLayoutMetrics()
        {
            _headerRect = new Rectangle(0, 0, WINDOW_WIDTH, HEADER_HEIGHT);

            _gridFrameRect = new Rectangle((int)Hud.NpcShopLayout.CardX, (int)Hud.NpcShopLayout.CardY,
                                           (int)Hud.NpcShopLayout.CardW, (int)Hud.NpcShopLayout.CardH);
            _gridRect = new Rectangle((int)Hud.NpcShopLayout.GridX, (int)Hud.NpcShopLayout.GridY,
                                      GRID_WIDTH, GRID_HEIGHT);

            _closeButtonRect = new Rectangle((int)Hud.NpcShopLayout.CloseX, (int)Hud.NpcShopLayout.CloseY,
                                             (int)Hud.NpcShopLayout.CloseW, (int)Hud.NpcShopLayout.CloseH);
            _pageTabRect = new Rectangle((int)Hud.NpcShopLayout.PageTabX, (int)Hud.NpcShopLayout.PageTabY,
                                         (int)Hud.NpcShopLayout.PageTabW, (int)Hud.NpcShopLayout.PageTabH);
            _bottomBtnRect = new Rectangle((int)Hud.NpcShopLayout.BottomBtnX, (int)Hud.NpcShopLayout.BottomBtnY,
                                           (int)Hud.NpcShopLayout.BottomBtnW, (int)Hud.NpcShopLayout.BottomBtnH);

            // Linha do rodapé (só referência de texto).
            _footerRect = new Rectangle(0, (int)(Hud.NpcShopLayout.FooterTextY - 12), WINDOW_WIDTH, 24);
            _buttonAreaRect = Rectangle.Empty;

            _repairButtonRect = new Rectangle((int)Hud.NpcShopLayout.RepairBtnX, (int)Hud.NpcShopLayout.RepairBtnY,
                                              (int)Hud.NpcShopLayout.RepairBtnW, (int)Hud.NpcShopLayout.RepairBtnH);
            _repairAllButtonRect = new Rectangle((int)(Hud.NpcShopLayout.RepairBtnX + Hud.NpcShopLayout.RepairBtnW + Hud.NpcShopLayout.RepairBtnGap),
                                                 (int)Hud.NpcShopLayout.RepairBtnY,
                                                 (int)Hud.NpcShopLayout.RepairBtnW, (int)Hud.NpcShopLayout.RepairBtnH);
        }

        public override async System.Threading.Tasks.Task Load()
        {
            await base.Load();
            _font = GraphicsManager.Instance.Font;

            var tl = TextureLoader.Instance;
            async System.Threading.Tasks.Task<Texture2D> L(string p) { try { return await tl.PrepareAndGetTexture(p); } catch { return null; } }
            _texPanel = await L("Interface/Imprint/imprint_panel.OZP");
            _texTab = await L("Interface/Imprint/imprint_tab.OZP");
            _texRect = await L("Interface/Inventory/inv_rect.OZP");
            _texDarkCard = await L("Interface/Imprint/imprint_dark_card.OZP");
            _texGridRow = await L("Interface/Inventory/inv_grid_row.OZP");
            _texClose = await L("Interface/Imprint/imprint_close.OZP");
            _texCloseHover = await L("Interface/Imprint/imprint_close_hover.OZP");
            _texBtnDark = await L("Interface/Inventory/inv_btn_dark.OZP");
            _texBtnGold = await L("Interface/Inventory/inv_btn_gold.OZP");
            _texOkCancel = await L("Interface/CharCreate/ok_cancel.OZT");   // botão padrão do cliente
            _texTooltip = await L("Interface/Inventory/inv_tooltip.OZP");   // tooltip padrão (mesmo do inventário)

            InvalidateStaticSurface();
        }

        private static bool Tex(Texture2D t) => t != null && !t.IsDisposed;

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            EnsureCharacterState();

            // Handle deferred show - wait one frame after warmup to avoid black screen
            if (_pendingShow && !Visible)
            {
                if (_warmupComplete)
                {
                    // Warmup done in previous frame, now safe to show
                    Visible = true;
                    BringToFront();
                    SoundController.Instance.PlayBuffer("Sound/iCreateWindow.wav");
                    _pendingShow = false;
                    _warmupComplete = false;
                }
                else
                {
                    // Do warmup this frame, show next frame
                    WarmupTexturesSync();
                    InvalidateStaticSurface();
                    EnsureStaticSurface();
                    _warmupComplete = true;
                }
            }

            if (Visible)
            {
                _currentGameTime = gameTime;

                if (MuGame.Instance.Keyboard.IsKeyDown(Keys.Escape) &&
                    MuGame.Instance.PrevKeyboard.IsKeyUp(Keys.Escape))
                {
                    Visible = false;
                    HandleVisibilityLost();
                    _wasVisible = false;
                    return;
                }

                // Handle 'L' key for repair mode toggle (only if repair shop and no dragged item)
                if (_isRepairShop &&
                    MuGame.Instance.Keyboard.IsKeyDown(Keys.L) &&
                    MuGame.Instance.PrevKeyboard.IsKeyUp(Keys.L))
                {
                    // Only toggle if not dragging an item
                    if (InventoryControl.Instance?.GetDraggedItem() == null)
                    {
                        ToggleRepairMode();
                        SoundController.Instance.PlayBuffer("Sound/iButton.wav");
                    }
                }

                Point mousePos = MuGame.Instance.UiMouseState.Position;
                bool leftPressed = MuGame.Instance.UiMouseState.LeftButton == ButtonState.Pressed;
                bool leftJustPressed = leftPressed && MuGame.Instance.PrevUiMouseState.LeftButton == ButtonState.Released;
                bool leftJustReleased = !leftPressed && MuGame.Instance.PrevUiMouseState.LeftButton == ButtonState.Pressed;

                UpdateChromeHover(mousePos);

                // Handle close button
                if (leftJustPressed && (_closeHovered || _bottomBtnHovered))
                {
                    SoundController.Instance.PlayBuffer("Sound/iButton.wav");
                    Visible = false;
                    HandleVisibilityLost();
                    return;
                }

                // Handle repair buttons (only if repair shop)
                if (_isRepairShop && leftJustPressed)
                {
                    if (_repairButtonHovered)
                    {
                        // Toggle repair mode
                        ToggleRepairMode();
                        SoundController.Instance.PlayBuffer("Sound/iButton.wav");
                        return;
                    }
                    else if (_repairAllButtonHovered)
                    {
                        // Repair all items
                        var svc = MuGame.Network?.GetCharacterService();
                        if (svc != null)
                        {
                            _ = svc.SendRepairItemRequestAsync(0xFF, false); // 0xFF = repair all
                            SoundController.Instance.PlayBuffer("Sound/iButton.wav");
                        }
                        return;
                    }
                }

                // Handle window dragging
                if (leftJustPressed && IsMouseOverDragArea(mousePos) && !_isDragging)
                {
                    DateTime now = DateTime.Now;
                    if ((now - _lastClickTime).TotalMilliseconds < 500)
                    {
                        // Double-click to reset position
                        Align = ControlAlign.None;
                        _lastClickTime = DateTime.MinValue;
                    }
                    else
                    {
                        _isDragging = true;
                        _dragOffset = new Point(mousePos.X - X, mousePos.Y - Y);
                        Align = ControlAlign.None;
                        _lastClickTime = now;
                    }
                }
                else if (leftJustReleased && _isDragging)
                {
                    _isDragging = false;
                }
                else if (_isDragging && leftPressed)
                {
                    X = mousePos.X - _dragOffset.X;
                    Y = mousePos.Y - _dragOffset.Y;
                }

                if (!_isDragging)
                {
                    UpdateHoverState();
                    HandleMouseInput();
                }
            }
            else if (_wasVisible)
            {
                HandleVisibilityLost();
            }

            _wasVisible = Visible;
        }

        private bool IsMouseOverDragArea(Point mousePos)
        {
            Rectangle headerScreen = Translate(_headerRect);
            Rectangle closeScreen = Translate(_closeButtonRect);
            return headerScreen.Contains(mousePos) && !closeScreen.Contains(mousePos);
        }

        private void UpdateChromeHover(Point mousePos)
        {
            var closeRect = Translate(_closeButtonRect);
            _closeHovered = closeRect.Contains(mousePos);
            _bottomBtnHovered = Hud.NpcShopLayout.BottomBtnEnabled && Translate(_bottomBtnRect).Contains(mousePos);

            // Handle repair button hover (only show if repair shop)
            if (_isRepairShop)
            {
                var repairRect = Translate(_repairButtonRect);
                var repairAllRect = Translate(_repairAllButtonRect);
                _repairButtonHovered = repairRect.Contains(mousePos);
                _repairAllButtonHovered = repairAllRect.Contains(mousePos);
            }
            else
            {
                _repairButtonHovered = false;
                _repairAllButtonHovered = false;
            }
        }

        public override void Draw(GameTime gameTime)
        {
            if (!Visible) return;

            EnsureStaticSurface();

            var gm = GraphicsManager.Instance;
            var spriteBatch = gm?.Sprite;
            if (spriteBatch == null) return;

            SpriteBatchScope? scope = null;
            if (!SpriteBatchScope.BatchIsBegun)
            {
                scope = new SpriteBatchScope(spriteBatch, SpriteSortMode.Deferred, BlendState.AlphaBlend, transform: UiScaler.SpriteTransform);
            }

            try
            {
                if (_staticSurface != null && !_staticSurface.IsDisposed)
                {
                    spriteBatch.Draw(_staticSurface, DisplayRectangle, Color.White * Alpha);
                }

                // Sem highlight de célula/hover (mobile é touch; oficial não tem) — o
                // quadrado claro ficava "vazando" sobre a grade no último toque.
                DrawShopItems(spriteBatch);
                DrawCloseButton(spriteBatch);
                DrawBottomButtonHover(spriteBatch);
                if (_isRepairShop)
                {
                    DrawRepairButtons(spriteBatch);
                }
            }
            finally
            {
                scope?.Dispose();
            }
        }

        public override void DrawAfter(GameTime gameTime)
        {
            if (!Visible || _hoveredItem == null) return;

            var gm = GraphicsManager.Instance;
            var spriteBatch = gm?.Sprite;
            if (spriteBatch == null) return;

            SpriteBatchScope? scope = null;
            if (!SpriteBatchScope.BatchIsBegun)
            {
                scope = new SpriteBatchScope(spriteBatch, SpriteSortMode.Deferred, BlendState.AlphaBlend, transform: UiScaler.SpriteTransform);
            }

            try
            {
                DrawTooltip(spriteBatch);
            }
            finally
            {
                scope?.Dispose();
            }
        }

        public override void Dispose()
        {
            base.Dispose();

            if (_characterState != null)
            {
                _characterState.ShopItemsChanged -= RefreshShopContent;
                _characterState = null;
            }

            _staticSurface?.Dispose();
            _staticSurface = null;
        }

        protected override void OnScreenSizeChanged()
        {
            base.OnScreenSizeChanged();
            InvalidateStaticSurface();
        }

        // ═══════════════════════════════════════════════════════════════
        // DRAWING PRIMITIVES
        // ═══════════════════════════════════════════════════════════════

        private void DrawWindowBackground(SpriteBatch spriteBatch, Rectangle rect)
        {
            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel == null) return;

            spriteBatch.Draw(pixel, rect, Theme.BorderOuter);

            var innerRect = new Rectangle(rect.X + 2, rect.Y + 2, rect.Width - 4, rect.Height - 4);
            UiDrawHelper.DrawVerticalGradient(spriteBatch, innerRect, Theme.BgDark, Theme.BgDarkest);

            spriteBatch.Draw(pixel, new Rectangle(innerRect.X, innerRect.Y, innerRect.Width, 1), Theme.BorderInner * 0.5f);
            spriteBatch.Draw(pixel, new Rectangle(innerRect.X, innerRect.Y, 1, innerRect.Height), Theme.BorderInner * 0.3f);

            UiDrawHelper.DrawCornerAccents(spriteBatch, rect, Theme.Accent * 0.4f);
        }

        private void DrawPanel(SpriteBatch spriteBatch, Rectangle rect, Color bgColor, bool withBorder = true)
        {
            UiDrawHelper.DrawPanel(spriteBatch, rect, bgColor,
                withBorder ? Theme.BorderInner * 0.8f : (Color?)null,
                withBorder ? Theme.BorderOuter : (Color?)null,
                withBorder ? Theme.BorderInner * 0.6f : null);
        }

        private void DrawSectionHeader(SpriteBatch spriteBatch, string title, int x, int y, int width)
        {
            if (_font == null) return;

            float scale = 0.32f;
            Vector2 size = _font.MeasureString(title) * scale;
            float textX = x + (width - size.X) / 2;

            spriteBatch.DrawString(_font, title, new Vector2(textX + 1, y + 1), Color.Black * 0.6f,
                                   0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            spriteBatch.DrawString(_font, title, new Vector2(textX, y), Theme.TextGold,
                                   0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        // ═══════════════════════════════════════════════════════════════
        // STATIC SURFACE RENDERING
        // ═══════════════════════════════════════════════════════════════

        private void EnsureStaticSurface()
        {
            if (!_staticSurfaceDirty && _staticSurface != null && !_staticSurface.IsDisposed)
                return;

            var gd = GraphicsManager.Instance?.GraphicsDevice;
            if (gd == null) return;

            // SUPERAMOSTRADA na resolução física (lição do inventário: texto nítido).
            float k = MathF.Max(1f, UiScaler.Scale * Constants.RENDER_SCALE);
            _staticSurface?.Dispose();
            _staticSurface = new RenderTarget2D(gd, (int)MathF.Ceiling(WINDOW_WIDTH * k), (int)MathF.Ceiling(WindowHeight * k),
                                                false, SurfaceFormat.Color, DepthFormat.None);

            var previousTargets = gd.GetRenderTargets();
            gd.SetRenderTarget(_staticSurface);
            gd.Clear(Color.Transparent);

            var spriteBatch = GraphicsManager.Instance.Sprite;
            using (new SpriteBatchScope(spriteBatch, SpriteSortMode.Deferred, BlendState.AlphaBlend,
                       GraphicsManager.GetQualityLinearSamplerState(), transform: Matrix.CreateScale(k, k, 1f)))
            {
                DrawStaticElements(spriteBatch);
            }

            gd.SetRenderTargets(previousTargets);
            _staticSurfaceDirty = false;
        }

        private void InvalidateStaticSurface() => _staticSurfaceDirty = true;

        private void DrawStaticElements(SpriteBatch spriteBatch)
        {
            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel == null) return;

            var fullRect = new Rectangle(0, 0, WINDOW_WIDTH, WindowHeight);

            // 1. Painel-template (barra de título assada). Fallback: fundo antigo.
            if (Tex(_texPanel)) spriteBatch.Draw(_texPanel, fullRect, Color.White);
            else DrawWindowBackground(spriteBatch, fullRect);

            // 2. Título sobre a barra.
            if (Hud.NpcShopLayout.TitleTextEnabled && _font != null)
            {
                string title = Hud.NpcShopLayout.TitleMessage;
                float ts = Hud.NpcShopLayout.TitleTextFont / 25f;
                Vector2 size = _font.MeasureString(title) * ts;
                var pos = new Vector2(Hud.NpcShopLayout.TitleTextX - size.X / 2f, Hud.NpcShopLayout.TitleTextY - size.Y / 2f);
                spriteBatch.DrawString(_font, title, pos + Vector2.One, Color.Black * 0.6f, 0f, Vector2.Zero, ts, SpriteEffects.None, 0f);
                spriteBatch.DrawString(_font, title, pos, new Color(235, 210, 150), 0f, Vector2.Zero, ts, SpriteEffects.None, 0f);
            }

            // 3. Aba de página "1".
            if (Hud.NpcShopLayout.PageTabEnabled)
            {
                if (Tex(_texTab)) spriteBatch.Draw(_texTab, _pageTabRect, Color.White);
                if (_font != null)
                {
                    float ps = Hud.NpcShopLayout.PageTabFont / 25f;
                    Vector2 sz = _font.MeasureString("1") * ps;
                    var pp = new Vector2(_pageTabRect.X + (_pageTabRect.Width - sz.X) / 2f,
                                         _pageTabRect.Y + (_pageTabRect.Height - sz.Y) / 2f);
                    spriteBatch.DrawString(_font, "1", pp + Vector2.One, Color.Black * 0.6f, 0f, Vector2.Zero, ps, SpriteEffects.None, 0f);
                    spriteBatch.DrawString(_font, "1", pp, new Color(235, 210, 150), 0f, Vector2.Zero, ps, SpriteEffects.None, 0f);
                }
            }

            // 4. Card (dark card + moldura) e a GRADE (células esverdeadas como no oficial).
            if (Hud.NpcShopLayout.CardEnabled)
            {
                if (Tex(_texDarkCard)) spriteBatch.Draw(_texDarkCard, _gridFrameRect, Color.White);
                if (Tex(_texRect)) Draw9Slice(spriteBatch, _texRect, _gridFrameRect);
            }
            if (Tex(_texGridRow))
            {
                // 1ª célula = borda esquerda, 8ª = direita; miolo cicla só entre as internas
                // (usar c%8 fazia a 9ª coluna repetir a borda e abrir um GAP).
                const int SRC_CELLS = 8;
                int cellSrcW = _texGridRow.Width / SRC_CELLS;
                for (int r = 0; r < SHOP_ROWS; r++)
                {
                    for (int c = 0; c < SHOP_COLUMNS; c++)
                    {
                        int srcIdx = c == 0 ? 0
                                   : c == SHOP_COLUMNS - 1 ? SRC_CELLS - 1
                                   : 1 + ((c - 1) % (SRC_CELLS - 2));
                        var cell = new Rectangle(_gridRect.X + c * SHOP_SQUARE_WIDTH,
                                                 _gridRect.Y + r * SHOP_SQUARE_HEIGHT,
                                                 SHOP_SQUARE_WIDTH, SHOP_SQUARE_HEIGHT);
                        var src = new Rectangle(cellSrcW * srcIdx, 0, cellSrcW, _texGridRow.Height);
                        spriteBatch.Draw(_texGridRow, cell, src, Color.White);
                    }
                }
            }

            // 5. Rodapé (hint) — dourado, centrado.
            if (Hud.NpcShopLayout.FooterTextEnabled && _font != null)
            {
                string hint = _isRepairShop
                    ? (_shopMode == ShopMode.Repair ? "Repair mode - Click items" : "Buy/Sell - Press 'L' to repair")
                    : "Click item to buy";
                float fs = Hud.NpcShopLayout.FooterTextFont / 25f;
                Vector2 size = _font.MeasureString(hint) * fs;
                var pos = new Vector2((WINDOW_WIDTH - size.X) / 2f, Hud.NpcShopLayout.FooterTextY - size.Y / 2f);
                spriteBatch.DrawString(_font, hint, pos + Vector2.One, Color.Black * 0.6f, 0f, Vector2.Zero, fs, SpriteEffects.None, 0f);
                spriteBatch.DrawString(_font, hint, pos, new Color(230, 200, 110), 0f, Vector2.Zero, fs, SpriteEffects.None, 0f);
            }

            // 6. Botão inferior ("Cancel Item Sale" — fecha a loja). Hover no dinâmico.
            if (Hud.NpcShopLayout.BottomBtnEnabled)
                DrawStandardButton(spriteBatch, _bottomBtnRect, Hud.NpcShopLayout.BottomBtnLabel, Hud.NpcShopLayout.BottomBtnFont);
        }

        // Botão padrão do cliente: metade "Save" (verde) da folha ok_cancel 467x80.
        private void DrawStandardButton(SpriteBatch sb, Rectangle r, string label, float fontSize)
        {
            if (Tex(_texOkCancel)) sb.Draw(_texOkCancel, r, new Rectangle(0, 0, 233, 80), Color.White);
            if (_font == null || string.IsNullOrEmpty(label)) return;
            float s = fontSize / 25f;
            Vector2 sz = _font.MeasureString(label) * s;
            float maxW = r.Width - 14f;
            if (sz.X > maxW && sz.X > 0f) { s *= maxW / sz.X; sz = _font.MeasureString(label) * s; }
            // Mesmo acabamento do DrawOkButton do Skill Imprint (cor 230,225,215 + sombra 0.6).
            var pos = new Vector2(r.X + (r.Width - sz.X) / 2f, r.Y + (r.Height - sz.Y) / 2f);
            sb.DrawString(_font, label, pos + Vector2.One, Color.Black * 0.6f, 0f, Vector2.Zero, s, SpriteEffects.None, 0f);
            sb.DrawString(_font, label, pos, new Color(230, 225, 215), 0f, Vector2.Zero, s, SpriteEffects.None, 0f);
        }

        // 9-slice (borda m px) — moldura inv_rect (m=8) e tooltip (m=3).
        private static void Draw9Slice(SpriteBatch sb, Texture2D tex, Rectangle dst, int m = 8)
        {
            int tw = tex.Width, th = tex.Height;
            if (dst.Width < m * 2 + 2 || dst.Height < m * 2 + 2) { sb.Draw(tex, dst, Color.White); return; }
            int cx = tw - 2 * m, cy = th - 2 * m;
            int dcx = dst.Width - 2 * m, dcy = dst.Height - 2 * m;
            sb.Draw(tex, new Rectangle(dst.X, dst.Y, m, m), new Rectangle(0, 0, m, m), Color.White);
            sb.Draw(tex, new Rectangle(dst.Right - m, dst.Y, m, m), new Rectangle(tw - m, 0, m, m), Color.White);
            sb.Draw(tex, new Rectangle(dst.X, dst.Bottom - m, m, m), new Rectangle(0, th - m, m, m), Color.White);
            sb.Draw(tex, new Rectangle(dst.Right - m, dst.Bottom - m, m, m), new Rectangle(tw - m, th - m, m, m), Color.White);
            sb.Draw(tex, new Rectangle(dst.X + m, dst.Y, dcx, m), new Rectangle(m, 0, cx, m), Color.White);
            sb.Draw(tex, new Rectangle(dst.X + m, dst.Bottom - m, dcx, m), new Rectangle(m, th - m, cx, m), Color.White);
            sb.Draw(tex, new Rectangle(dst.X, dst.Y + m, m, dcy), new Rectangle(0, m, m, cy), Color.White);
            sb.Draw(tex, new Rectangle(dst.Right - m, dst.Y + m, m, dcy), new Rectangle(tw - m, m, m, cy), Color.White);
            sb.Draw(tex, new Rectangle(dst.X + m, dst.Y + m, dcx, dcy), new Rectangle(m, m, cx, cy), Color.White);
        }

        private void DrawModernHeader(SpriteBatch spriteBatch)
        {
            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel == null) return;

            var headerBg = new Rectangle(8, 6, WINDOW_WIDTH - 16, HEADER_HEIGHT - 8);
            DrawPanel(spriteBatch, headerBg, Theme.BgMid);

            spriteBatch.Draw(pixel, new Rectangle(20, 8, WINDOW_WIDTH - 40, 2), Theme.Accent * 0.8f);
            spriteBatch.Draw(pixel, new Rectangle(30, 10, WINDOW_WIDTH - 60, 1), Theme.AccentDim * 0.4f);

            if (_font != null)
            {
                string title = "NPC SHOP";
                float scale = 0.50f;
                Vector2 size = _font.MeasureString(title) * scale;
                Vector2 pos = new((WINDOW_WIDTH - size.X) / 2, (HEADER_HEIGHT - size.Y) / 2 + 2);

                spriteBatch.Draw(pixel, new Rectangle((int)pos.X - 20, (int)pos.Y - 4, (int)size.X + 40, (int)size.Y + 8),
                                Theme.AccentGlow * 0.3f);

                spriteBatch.DrawString(_font, title, pos + new Vector2(2, 2), Color.Black * 0.5f,
                                       0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                spriteBatch.DrawString(_font, title, pos, Theme.TextWhite,
                                       0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }

            int sepY = HEADER_HEIGHT - 2;
            UiDrawHelper.DrawHorizontalGradient(spriteBatch, new Rectangle(20, sepY, (WINDOW_WIDTH - 40) / 2, 1),
                                  Color.Transparent, Theme.BorderInner);
            UiDrawHelper.DrawHorizontalGradient(spriteBatch, new Rectangle(WINDOW_WIDTH / 2, sepY, (WINDOW_WIDTH - 40) / 2, 1),
                                  Theme.BorderInner, Color.Transparent);
        }

        private void DrawModernGridSection(SpriteBatch spriteBatch)
        {
            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel == null) return;

            DrawSectionHeader(spriteBatch, "ITEMS FOR SALE", _gridFrameRect.X, _gridFrameRect.Y + 4, _gridFrameRect.Width);
            DrawPanel(spriteBatch, _gridFrameRect, Theme.BgMid);

            spriteBatch.Draw(pixel, _gridRect, Theme.SlotBg);

            spriteBatch.Draw(pixel, new Rectangle(_gridRect.X, _gridRect.Y, _gridRect.Width, 2), Color.Black * 0.4f);
            spriteBatch.Draw(pixel, new Rectangle(_gridRect.X, _gridRect.Y, 2, _gridRect.Height), Color.Black * 0.3f);

            Color gridLine = new(40, 48, 60, 100);
            Color gridLineMajor = new(55, 65, 80, 120);

            for (int x = 1; x < SHOP_COLUMNS; x++)
            {
                int lineX = _gridRect.X + x * SHOP_SQUARE_WIDTH;
                bool isMajor = x == SHOP_COLUMNS / 2;
                spriteBatch.Draw(pixel, new Rectangle(lineX, _gridRect.Y, 1, _gridRect.Height), isMajor ? gridLineMajor : gridLine);
            }

            for (int y = 1; y < SHOP_ROWS; y++)
            {
                int lineY = _gridRect.Y + y * SHOP_SQUARE_HEIGHT;
                bool isMajor = y == SHOP_ROWS / 2;
                spriteBatch.Draw(pixel, new Rectangle(_gridRect.X, lineY, _gridRect.Width, 1), isMajor ? gridLineMajor : gridLine);
            }

            spriteBatch.Draw(pixel, new Rectangle(_gridRect.X, _gridRect.Bottom - 1, _gridRect.Width, 1), Theme.BorderHighlight * 0.2f);
            spriteBatch.Draw(pixel, new Rectangle(_gridRect.Right - 1, _gridRect.Y, 1, _gridRect.Height), Theme.BorderHighlight * 0.15f);
        }

        private void DrawModernButtonArea(SpriteBatch spriteBatch)
        {
            if (_buttonAreaRect.Height == 0) return;

            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel == null) return;

            DrawPanel(spriteBatch, _buttonAreaRect, Theme.BgMid);
        }

        private void DrawModernFooter(SpriteBatch spriteBatch)
        {
            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel == null) return;

            int sepY = _footerRect.Y - 4;
            UiDrawHelper.DrawHorizontalGradient(spriteBatch, new Rectangle(30, sepY, (WINDOW_WIDTH - 60) / 2, 1),
                                  Color.Transparent, Theme.Accent * 0.4f);
            UiDrawHelper.DrawHorizontalGradient(spriteBatch, new Rectangle(WINDOW_WIDTH / 2, sepY, (WINDOW_WIDTH - 60) / 2, 1),
                                  Theme.Accent * 0.4f, Color.Transparent);

            DrawPanel(spriteBatch, _footerRect, Theme.BgMid);

            if (_font != null)
            {
                string hint = _isRepairShop
                    ? (_shopMode == ShopMode.Repair ? "Repair mode - Click items" : "Buy/Sell - Press 'L' to repair")
                    : "Click item to buy";
                float scale = 0.38f;
                Vector2 size = _font.MeasureString(hint) * scale;
                int hintX = _footerRect.X;
                Vector2 pos = new(hintX + ((_footerRect.Width - (hintX - _footerRect.X)) - size.X) / 2,
                                  _footerRect.Y + (_footerRect.Height - size.Y) / 2);

                spriteBatch.DrawString(_font, hint, pos + Vector2.One, Color.Black * 0.5f,
                                       0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                spriteBatch.DrawString(_font, hint, pos, Theme.TextGold,
                                       0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }
        }

        private void DrawRepairButtons(SpriteBatch spriteBatch)
        {
            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel == null || _font == null) return;

            DrawRepairButton(spriteBatch, Translate(_repairButtonRect), "Repair item",
                             _shopMode == ShopMode.Repair, _repairButtonHovered);
            DrawRepairButton(spriteBatch, Translate(_repairAllButtonRect), "Repair all",
                             false, _repairAllButtonHovered);
        }

        private void DrawRepairButton(SpriteBatch spriteBatch, Rectangle rect, string label, bool active, bool hovered)
        {
            DrawStandardButton(spriteBatch, rect, label, Hud.NpcShopLayout.RepairBtnFont);
            if (active && GraphicsManager.Instance.Pixel != null)
                DrawBorderRect(spriteBatch, GraphicsManager.Instance.Pixel, rect, Theme.Accent);
            if (hovered && GraphicsManager.Instance.Pixel != null)
                spriteBatch.Draw(GraphicsManager.Instance.Pixel, rect, Color.White * 0.12f);
        }

        private static void DrawBorderRect(SpriteBatch sb, Texture2D pixel, Rectangle r, Color c)
        {
            sb.Draw(pixel, new Rectangle(r.X, r.Y, r.Width, 1), c);
            sb.Draw(pixel, new Rectangle(r.X, r.Bottom - 1, r.Width, 1), c);
            sb.Draw(pixel, new Rectangle(r.X, r.Y, 1, r.Height), c);
            sb.Draw(pixel, new Rectangle(r.Right - 1, r.Y, 1, r.Height), c);
        }

        // ═══════════════════════════════════════════════════════════════
        // DYNAMIC DRAWING
        // ═══════════════════════════════════════════════════════════════

        private void DrawCloseButton(SpriteBatch spriteBatch)
        {
            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel == null) return;

            var rect = Translate(_closeButtonRect);
            var tex = _closeHovered && Tex(_texCloseHover) ? _texCloseHover : _texClose;
            if (Tex(tex))
            {
                spriteBatch.Draw(tex, rect, Color.White * Alpha);
                return;
            }

            // Fallback: X procedural
            Color btnColor = _closeHovered ? Theme.Accent : Theme.TextGray;
            int cx = rect.X + rect.Width / 2;
            int cy = rect.Y + rect.Height / 2;
            int halfSize = 6;
            int thickness = 2;
            for (int i = -halfSize; i <= halfSize; i++)
            {
                spriteBatch.Draw(pixel, new Rectangle(cx + i - thickness / 2, cy + i - thickness / 2, thickness, thickness), btnColor);
                spriteBatch.Draw(pixel, new Rectangle(cx + i - thickness / 2, cy - i - thickness / 2, thickness, thickness), btnColor);
            }
        }

        // Realce de hover do botão inferior (a placa+label estão assadas na superfície estática).
        private void DrawBottomButtonHover(SpriteBatch spriteBatch)
        {
            if (!Hud.NpcShopLayout.BottomBtnEnabled || !_bottomBtnHovered) return;
            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel == null) return;
            spriteBatch.Draw(pixel, Translate(_bottomBtnRect), Color.White * 0.15f);
        }

        private void DrawShopItems(SpriteBatch spriteBatch)
        {
            var font = _font ?? GraphicsManager.Instance.Font;
            var pixel = GraphicsManager.Instance.Pixel;
            var jewelEntries = new List<(InventoryItem Item, Rectangle Rect)>();

            foreach (var item in _items)
            {
                var rect = ItemFootprint(item);

                bool isHovered = item == _hoveredItem;

                // Estilo oficial (RealMU): item DIRETO na grade — sem box, sem glow, sem
                // borda, sem hover. Sprite com INSET de 2px pra nunca cobrir as linhas
                // da grade (senão itens vizinhos parecem "vazar" um no outro).
                var texRect = new Rectangle(rect.X + 2, rect.Y + 2, rect.Width - 4, rect.Height - 4);
                Texture2D texture = ResolveItemTexture(item, texRect.Width, texRect.Height, isHovered);

                if (texture != null)
                {
                    // Fit com proporção dentro do slot: sprites não-BMD eram esticados
                    // anisotropicamente no texRect (pra BMD o fit é identidade, o preview
                    // já é gerado no tamanho do texRect).
                    Rectangle iconRect = ItemGridRenderHelper.FitRect(rect, texture.Width, texture.Height, 2);
                    spriteBatch.Draw(texture, iconRect, Color.White * Alpha);

                    if (JewelShineOverlay.ShouldShine(item))
                    {
                        jewelEntries.Add((item, iconRect));
                    }
                }
                else if (pixel != null)
                {
                    ItemGridRenderHelper.DrawItemPlaceholder(spriteBatch, pixel, font, texRect, item, Theme.BgLight, Theme.TextGray * 0.8f);
                }

                if (font != null && item.Definition.BaseDurability == 0 && item.Definition.MagicDurability == 0 && item.Durability > 1)
                {
                    ItemGridRenderHelper.DrawItemStackCount(spriteBatch, font, rect, item.Durability, Theme.TextGold, Alpha);
                }

                ItemGridRenderHelper.DrawItemLevelBadge(spriteBatch, GraphicsManager.Instance.Pixel, font, rect, item.Details.Level,
               lvl => lvl >= 9 ? Theme.AccentBright :
                      lvl >= 7 ? Theme.Accent :
                      lvl >= 4 ? Theme.AccentDim :
                      Theme.TextGray,
               new Color(0, 0, 0, 180));

                if (jewelEntries.Count > 0)
                {
                    JewelShineOverlay.DrawBatch(spriteBatch, jewelEntries, _currentGameTime, Alpha, UiScaler.SpriteTransform);
                }
            }
        }

        private void DrawTooltip(SpriteBatch spriteBatch)
        {
            if (_hoveredItem == null || _font == null) return;

            var lines = ItemUiHelper.BuildTooltipLines(_hoveredItem);
            int buyPrice = ItemPriceCalculator.CalculateBuyPrice(_hoveredItem);
            if (buyPrice > 0)
            {
                lines.Add(($"Buy Price: {buyPrice} Zen", Theme.TextGold));
            }
            const float scale = 0.44f;
            const int lineSpacing = 4;
            const int paddingX = 14;
            const int paddingY = 12;

            int maxWidth = 0;
            int totalHeight = 0;
            foreach (var (text, _) in lines)
            {
                Vector2 sz = _font.MeasureString(text) * scale;
                maxWidth = Math.Max(maxWidth, (int)MathF.Ceiling(sz.X));
                totalHeight += (int)MathF.Ceiling(sz.Y) + lineSpacing;
            }
            totalHeight += 6;

            int tooltipWidth = maxWidth + paddingX * 2;
            int tooltipHeight = totalHeight + paddingY * 2;

            Point mouse = MuGame.Instance.UiMouseState.Position;
            var itemRect = ItemFootprint(_hoveredItem);

            Rectangle tooltipRect = new(mouse.X + 16, mouse.Y + 16, tooltipWidth, tooltipHeight);
            Rectangle screenBounds = new(0, 0, UiScaler.VirtualSize.X, UiScaler.VirtualSize.Y);

            if (tooltipRect.Intersects(itemRect))
            {
                tooltipRect.X = itemRect.X - tooltipWidth - 8;
                tooltipRect.Y = itemRect.Y;

                if (tooltipRect.X < 10 || tooltipRect.Intersects(itemRect))
                {
                    tooltipRect.X = itemRect.X;
                    tooltipRect.Y = itemRect.Y - tooltipHeight - 8;

                    if (tooltipRect.Y < 10)
                    {
                        tooltipRect.X = itemRect.X;
                        tooltipRect.Y = itemRect.Bottom + 8;
                    }
                }
            }

            tooltipRect.X = Math.Clamp(tooltipRect.X, 10, screenBounds.Right - tooltipRect.Width - 10);
            tooltipRect.Y = Math.Clamp(tooltipRect.Y, 10, screenBounds.Bottom - tooltipRect.Height - 10);

            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel == null) return;

            bool isExcellent = _hoveredItem.Details.IsExcellent;
            bool isAncient = _hoveredItem.Details.IsAncient;
            bool isHighLevel = _hoveredItem.Details.Level >= 7;

            Color borderColor = isExcellent ? Theme.GlowExcellent :
                                isAncient ? Theme.GlowAncient :
                                isHighLevel ? Theme.Accent :
                                Theme.TextWhite;

            // Fundo padrão do cliente (inv_tooltip 9-slice, sem sombra) — igual ao inventário.
            if (Tex(_texTooltip))
            {
                Draw9Slice(spriteBatch, _texTooltip, tooltipRect, 3);
            }
            else
            {
                UiDrawHelper.DrawVerticalGradient(spriteBatch, tooltipRect, new Color(20, 24, 32, 252), new Color(12, 14, 18, 254));
                const int borderThickness = 2;
                spriteBatch.Draw(pixel, new Rectangle(tooltipRect.X, tooltipRect.Y, tooltipRect.Width, borderThickness), borderColor);
                spriteBatch.Draw(pixel, new Rectangle(tooltipRect.X, tooltipRect.Bottom - borderThickness, tooltipRect.Width, borderThickness), borderColor);
                spriteBatch.Draw(pixel, new Rectangle(tooltipRect.X, tooltipRect.Y, borderThickness, tooltipRect.Height), borderColor);
                spriteBatch.Draw(pixel, new Rectangle(tooltipRect.Right - borderThickness, tooltipRect.Y, borderThickness, tooltipRect.Height), borderColor);
            }

            int textY = tooltipRect.Y + paddingY;
            bool firstLine = true;
            foreach (var (text, color) in lines)
            {
                Vector2 textSize = _font.MeasureString(text) * scale;
                int textX = tooltipRect.X + (tooltipRect.Width - (int)textSize.X) / 2;

                spriteBatch.DrawString(_font, text, new Vector2(textX + 1, textY + 1), Color.Black * 0.7f,
                                       0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                Color lineColor = firstLine ? borderColor : color;
                spriteBatch.DrawString(_font, text, new Vector2(textX, textY), lineColor,
                                       0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

                textY += (int)textSize.Y + lineSpacing;

                if (firstLine)
                {
                    textY += 2;
                    spriteBatch.Draw(pixel, new Rectangle(tooltipRect.X + 8, textY, tooltipRect.Width - 16, 1), borderColor * 0.3f);
                    textY += 4;
                    firstLine = false;
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // INPUT HANDLING
        // ═══════════════════════════════════════════════════════════════

        private void HandleMouseInput()
        {
            var mouse = MuGame.Instance.UiMouseState;
            var prev = MuGame.Instance.PrevUiMouseState;

            bool leftJustPressed = mouse.LeftButton == ButtonState.Pressed &&
                                   prev.LeftButton == ButtonState.Released;

            if (!leftJustPressed) return;

            // Prevent input when a modal dialog is open (e.g., sell confirmation)
            if (IsModalDialogOpen()) return;
            if (Scene?.FocusControl != this) return;

            // Ignore shop clicks while dragging an item from inventory/vault (so a sell drop doesn't auto-buy a shop item)
            if (InventoryControl.Instance?.GetDraggedItem() != null || VaultControl.Instance?.GetDraggedItem() != null) return;

            Point mousePos = mouse.Position;

            if (DisplayRectangle.Contains(mousePos))
            {
                Scene?.SetMouseInputConsumed();
            }

            if (_hoveredItem == null) return;

            byte slot = (byte)(_hoveredItem.GridPosition.Y * SHOP_COLUMNS + _hoveredItem.GridPosition.X);
            var svc = MuGame.Network?.GetCharacterService();
            if (svc != null)
            {
                _ = svc.SendBuyItemFromNpcRequestAsync(slot);
            }
        }

        private void UpdateHoverState()
        {
            var mousePos = MuGame.Instance.UiMouseState.Position;
            _hoveredSlot = ItemGridRenderHelper.GetSlotAtScreenPosition(DisplayRectangle, _gridRect, SHOP_COLUMNS, SHOP_ROWS, SHOP_SQUARE_WIDTH, SHOP_SQUARE_HEIGHT, mousePos);
            _hoveredItem = GetItemAt(mousePos);
        }

        // ═══════════════════════════════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════════════════════════════

        private Rectangle Translate(Rectangle rect)
            => new(DisplayRectangle.X + rect.X, DisplayRectangle.Y + rect.Y, rect.Width, rect.Height);

        private InventoryItem GetItemAt(Point mousePos)
        {
            if (!DisplayRectangle.Contains(mousePos)) return null;

            foreach (var item in _items)
            {
                if (ItemFootprint(item).Contains(mousePos)) return item;
            }

            return null;
        }

        // Dimensões EFETIVAS do item na grade: as dims do item.bmd (S21) podem divergir das
        // do servidor (S6) que POSICIONOU os itens — sem clamp, o retângulo invade o vizinho
        // e a moldura ("itens vazando"). Limita pela próxima célula ocupada e pelas bordas.
        private (int W, int H) EffectiveDims(InventoryItem item)
        {
            int x = item.GridPosition.X, y = item.GridPosition.Y;
            int defW = Math.Max(1, item.Definition?.Width ?? 1);
            int defH = Math.Max(1, item.Definition?.Height ?? 1);
            int w = Math.Min(defW, SHOP_COLUMNS - x);
            int h = Math.Min(defH, SHOP_ROWS - y);

            foreach (var other in _items)
            {
                if (ReferenceEquals(other, item)) continue;
                int ox = other.GridPosition.X, oy = other.GridPosition.Y;
                // Vizinhos na MESMA faixa de linhas limitam a largura pelos DOIS lados:
                // se a fileira está empacotada de 1 em 1, até o último item (sem vizinho à
                // direita) rende 1 de largura — senão ele "vaza" pro espaço vazio ao lado.
                if (oy >= y && oy < y + defH && ox != x)
                    w = Math.Min(w, Math.Abs(ox - x));
                if (ox >= x && ox < x + defW && oy != y)
                    h = Math.Min(h, Math.Abs(oy - y));
            }

            return (Math.Max(1, w), Math.Max(1, h));
        }

        // Retângulo (em coords de tela) que o item realmente ocupa na grade.
        private Rectangle ItemFootprint(InventoryItem item)
        {
            var (w, h) = EffectiveDims(item);
            return new Rectangle(
                DisplayRectangle.X + _gridRect.X + item.GridPosition.X * SHOP_SQUARE_WIDTH,
                DisplayRectangle.Y + _gridRect.Y + item.GridPosition.Y * SHOP_SQUARE_HEIGHT,
                w * SHOP_SQUARE_WIDTH,
                h * SHOP_SQUARE_HEIGHT);
        }

        private void HandleVisibilityLost()
        {
            SendCloseNpcRequest();
            _characterState?.ClearShopItems();
            _items.Clear();
            _itemTextureCache.Clear();
            _bmdPreviewCache.Clear();
            _hoveredItem = null;
            _hoveredSlot = new Point(-1, -1);
            _isDragging = false;
            _pendingShow = false;

            // Reset repair mode when closing shop
            _shopMode = ShopMode.BuyAndSell;
            _warmupComplete = false;
        }

        private bool IsModalDialogOpen()
        {
            var scene = Scene;
            if (scene == null) return false;

            for (int i = scene.Controls.Count - 1; i >= 0; i--)
            {
                if (scene.Controls[i] is DialogControl dialog && dialog.Visible)
                {
                    return true;
                }
            }

            return false;
        }

        private void SendCloseNpcRequest()
        {
            if (_closeRequestSent) return;
            _closeRequestSent = true;
            var svc = MuGame.Network?.GetCharacterService();
            if (svc != null)
            {
                _ = svc.SendCloseNpcRequestAsync();
            }
        }

        private void EnsureCharacterState()
        {
            if (_characterState != null) return;

            _characterState = MuGame.Network?.GetCharacterState();
            if (_characterState != null)
            {
                _characterState.ShopItemsChanged += RefreshShopContent;
            }
        }

        private void RefreshShopContent()
        {
            if (_characterState == null) return;

            _items.Clear();
            _itemTextureCache.Clear();
            _bmdPreviewCache.Clear();

            var shopItems = _characterState.GetShopItems();
            int maxSlots = SHOP_COLUMNS * SHOP_ROWS;
            foreach (var kv in shopItems)
            {
                byte slot = kv.Key;
                if (slot >= maxSlots)
                    continue;

                byte[] data = kv.Value;

                int gridX = slot % SHOP_COLUMNS;
                int gridY = slot / SHOP_COLUMNS;

                var def = ItemDatabase.GetItemDefinition(data)
                    ?? new ItemDefinition(0, ItemDatabase.GetItemName(data) ?? "Unknown Item", 1, 1, "Interface/newui_item_box.tga");

                var item = new InventoryItem(def, new Point(gridX, gridY), data);
                if (data.Length > 2)
                {
                    item.Durability = data[2];
                }

                _items.Add(item);
            }

            foreach (var item in _items)
            {
                if (!string.IsNullOrEmpty(item.Definition.TexturePath) &&
                    !item.Definition.TexturePath.EndsWith(".bmd", StringComparison.OrdinalIgnoreCase))
                {
                    _ = TextureLoader.Instance.Prepare(item.Definition.TexturePath);
                }
            }

            if (_items.Count > 0)
            {
                // Align left with padding before showing, then freeze position to avoid auto realignment
                ForceAlignNow();
                Align = ControlAlign.None;
                // Use deferred show - warmup happens in Update(), window shows one frame later
                // to avoid black screen flicker from render target switches during Draw().
                _pendingShow = true;
                _warmupComplete = false;
                _closeRequestSent = false;
                _isDragging = false;
            }
        }

        private void WarmupTexturesSync()
        {
            if (GraphicsManager.Instance?.Sprite == null)
                return;

            foreach (var item in _items)
            {
                // MESMO tamanho que o Draw pede (footprint efetivo - inset de 2px),
                // senão o warmup povoa uma chave de cache que nunca é usada.
                var rect = ItemFootprint(item);
                _ = ResolveItemTexture(item, rect.Width - 4, rect.Height - 4, animated: false);
            }
        }

        private Texture2D ResolveItemTexture(InventoryItem item, int width, int height, bool animated)
        {
            if (item?.Definition == null) return null;

            string texturePath = item.Definition.TexturePath;
            if (string.IsNullOrEmpty(texturePath)) return null;

            bool isBmd = texturePath.EndsWith(".bmd", StringComparison.OrdinalIgnoreCase);

            if (!isBmd)
            {
                if (_itemTextureCache.TryGetValue(texturePath, out var cached) && cached != null)
                    return cached;

                var tex = TextureLoader.Instance.GetTexture2D(texturePath);
                if (tex != null) _itemTextureCache[texturePath] = tex;
                return tex;
            }

            bool isHovered = animated;

            // Material animation for non-hovered items (if enabled)
            if (!isHovered && Constants.ENABLE_ITEM_MATERIAL_ANIMATION)
            {
                try
                {
                    var mat = BmdPreviewRenderer.GetMaterialAnimatedPreview(item, width, height, _currentGameTime);
                    if (mat != null)
                    {
                        return mat;
                    }
                }
                catch { }
            }

            if (isHovered)
            {
                try
                {
                    return BmdPreviewRenderer.GetSmoothAnimatedPreview(item, width, height, _currentGameTime);
                }
                catch { return null; }
            }

            var cacheKey = (item, width, height, false);
            if (_bmdPreviewCache.TryGetValue(cacheKey, out var cachedPreview) && cachedPreview != null)
                return cachedPreview;

            try
            {
                var preview = BmdPreviewRenderer.GetPreview(item, width, height);
                if (preview != null)
                {
                    _bmdPreviewCache[cacheKey] = preview;
                }
                return preview;
            }
            catch { return null; }
        }

        // ═══════════════════════════════════════════════════════════════
        // REPAIR MODE
        // ═══════════════════════════════════════════════════════════════

        public ShopMode GetShopMode() => _shopMode;
        public bool IsRepairShop => _isRepairShop;
        public bool IsRepairMode => _shopMode == ShopMode.Repair;

        public void SetRepairShop(bool canRepair)
        {
            _isRepairShop = canRepair;
            if (!canRepair && _shopMode == ShopMode.Repair)
            {
                // If NPC can't repair, reset to buy/sell mode
                _shopMode = ShopMode.BuyAndSell;
            }
            BuildLayoutMetrics();
            var newSize = new Point(WINDOW_WIDTH, WindowHeight);
            ControlSize = newSize;
            ViewSize = newSize;              // <-- KLUCZ: utrzymuj ViewSize = ControlSize gdy AutoViewSize=false
            InvalidateStaticSurface();
        }

        public void ToggleRepairMode()
        {
            if (!_isRepairShop) return;

            if (_shopMode == ShopMode.BuyAndSell)
            {
                _shopMode = ShopMode.Repair;
            }
            else
            {
                _shopMode = ShopMode.BuyAndSell;
            }

            InvalidateStaticSurface();

            // TODO: Notify inventory control of mode change
            // InventoryControl.Instance?.SetRepairMode(_shopMode == ShopMode.Repair);
        }

    }
}
