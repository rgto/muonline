using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Client.Main;
using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Core.Client;
using Client.Main.Core.Utilities;
using Client.Main.Controls.UI;
using Client.Main.Controls.UI.Common;
using Client.Main.Controls.UI.Game.Common;
using Client.Main.Controls.UI.Game.Inventory;
using Client.Main.Networking;
using Client.Main.Models;
using Client.Main.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MUnique.OpenMU.Network.Packets;
using MUnique.OpenMU.Network.Packets.ClientToServer;

namespace Client.Main.Controls.UI.Game
{
    public class VaultControl : UIControl
    {
        // ═══════════════════════════════════════════════════════════════
        // WINDOW DIMENSIONS
        // ═══════════════════════════════════════════════════════════════
        // 8 colunas = InventoryConstants.RowSize do servidor (stride do protocolo).
        public static int Columns => Hud.VaultLayout.GridCols;
        // TOTAL de linhas do baú no servidor (InventoryConstants.WarehouseRows).
        public const int TotalRows = 15;
        // Linhas VISÍVEIS por página (o resto fica nas outras abas).
        public static int VisibleRows => Math.Min(Hud.VaultLayout.GridRows, TotalRows);
        // Rows = total (grid lógico do servidor); a página só muda o que é EXIBIDO.
        public static int Rows => TotalRows;
        public static int PageCount => (int)Math.Ceiling(TotalRows / (float)Math.Max(1, VisibleRows));
        private int _page;   // página atual (0-based)
        private int PageFirstRow => _page * VisibleRows;
        private const int VAULT_SQUARE_WIDTH = 32;
        private const int VAULT_SQUARE_HEIGHT = 32;
        // Margem só do ÍCONE dentro do footprint. O FUNDO do item preenche o footprint
        // inteiro cravado nas linhas da grade (referência oficial do mobile: background
        // alinhado com as linhas; recuar o fundo mostrava o slot atrás — rejeitado).
        private const int ICON_INSET = 2;

        // Dimensões dirigidas pelo VaultLayout (editor hud-edit-vault :5191).
        private static int HEADER_HEIGHT => Hud.VaultLayout.DragBarH;
        private static readonly int GRID_WIDTH = Columns * VAULT_SQUARE_WIDTH;
        private static int GRID_HEIGHT => VisibleRows * VAULT_SQUARE_HEIGHT;
        private static int WINDOW_WIDTH => Hud.VaultLayout.PanelW;
        private static int WINDOW_HEIGHT => Hud.VaultLayout.PanelH;

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

            public static readonly Color Secondary = new(90, 140, 200);

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

            public static readonly Color Success = new(80, 200, 120);
            public static readonly Color Danger = new(220, 80, 80);
        }

        private static readonly ItemGlowPalette GlowPalette = new(
            Theme.GlowNormal,
            Theme.GlowMagic,
            Theme.GlowExcellent,
            Theme.GlowAncient,
            Theme.GlowLegendary);

        private static VaultControl _instance;

        private readonly List<InventoryItem> _items = new();
        private InventoryItem[,] _itemGrid = new InventoryItem[Columns, Rows];

        private readonly Dictionary<string, Texture2D> _itemTextureCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<(InventoryItem item, int width, int height, bool animated), Texture2D> _bmdPreviewCache = new();

        private Rectangle _headerRect;
        private Rectangle _gridRect;
        private Rectangle _gridFrameRect;
        private Rectangle _darkCardRect;
        private Rectangle _footerRect;
        private Rectangle _zenFieldRect;
        private Rectangle _feeFieldRect;
        private Rectangle _depositButtonRect;
        private Rectangle _withdrawButtonRect;
        private Rectangle _lockButtonRect;
        private Rectangle _expandedButtonRect;
        private Rectangle _closeButtonRect;

        // Assets do visual novo (mesmos do NPC Shop — sem patch).
        private Texture2D _texPanel, _texRect, _texDarkCard, _texGridRow, _texField, _texVaultFrame, _texTab;
        private Texture2D _texClose, _texCloseHover, _texOkCancel;
        private bool _lockHovered, _expandedHovered;

        private RenderTarget2D _staticSurface;
        private bool _staticSurfaceDirty = true;

        private SpriteFont _font;

        private CharacterState _characterState;
        private readonly NetworkManager _networkManager;
        private readonly ILogger<VaultControl> _logger;

        private InventoryItem _hoveredItem;
        private Point _hoveredSlot = new(-1, -1);
        private Point _pendingDropSlot = new(-1, -1);

        private InventoryItem _draggedItem;
        private Point _draggedOriginalSlot = new(-1, -1);

        private GameTime _currentGameTime;
        private bool _wasVisible;
        private bool _closeRequestSent;
        private bool _closeHovered;
        private bool _pendingShow;
        private bool _warmupComplete;

        // Zen transfer
        private const uint MaxZen = 2_000_000_000;
        private const int ZenInputMaxDigits = 10;
        private const double CursorBlinkIntervalMs = 500;
        private bool _zenFieldHovered;
        private bool _depositHovered;
        private bool _withdrawHovered;
        private bool _isZenInputActive;
        private string _zenInputText = string.Empty;
        private double _zenInputBlinkTimer;
        private bool _zenInputShowCursor;
        private VaultMoveMoneyRequest.VaultMoneyMoveDirection _zenMoveDirection = VaultMoveMoneyRequest.VaultMoneyMoveDirection.InventoryToVault;
        private long _vaultZen;

        // Window drag support
        private bool _isDragging;
        private Point _dragOffset;
        private DateTime _lastClickTime = DateTime.MinValue;

        private VaultControl(NetworkManager networkManager = null, ILoggerFactory loggerFactory = null)
        {
            _networkManager = networkManager ?? MuGame.Network;
            var factory = loggerFactory ?? MuGame.AppLoggerFactory;
            _logger = factory?.CreateLogger<VaultControl>();

            BuildLayoutMetrics();

            ControlSize = new Point(WINDOW_WIDTH, WINDOW_HEIGHT);
            ViewSize = ControlSize;
            AutoViewSize = false;
            Interactive = true;
            Visible = false;
            Align = ControlAlign.VerticalCenter | ControlAlign.Left;

            EnsureCharacterState();
        }

        public override bool NonDisposable => true;
        public static VaultControl Instance => _instance ??= new VaultControl();

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
            var L = typeof(Hud.VaultLayout); _ = L;

            _headerRect = new Rectangle(0, 0, WINDOW_WIDTH, HEADER_HEIGHT);

            _gridFrameRect = new Rectangle((int)Hud.VaultLayout.CardX, (int)Hud.VaultLayout.CardY,
                                           (int)Hud.VaultLayout.CardW, (int)Hud.VaultLayout.CardH);
            _darkCardRect = new Rectangle((int)Hud.VaultLayout.DarkCardX, (int)Hud.VaultLayout.DarkCardY,
                                          (int)Hud.VaultLayout.DarkCardW, (int)Hud.VaultLayout.DarkCardH);
            _gridRect = new Rectangle((int)Hud.VaultLayout.GridX, (int)Hud.VaultLayout.GridY,
                                      GRID_WIDTH, GRID_HEIGHT);

            _closeButtonRect = new Rectangle((int)Hud.VaultLayout.CloseX, (int)Hud.VaultLayout.CloseY,
                                             (int)Hud.VaultLayout.CloseW, (int)Hud.VaultLayout.CloseH);

            _zenFieldRect = new Rectangle((int)Hud.VaultLayout.ZenFieldX,
                                          (int)(Hud.VaultLayout.ZenRowY - Hud.VaultLayout.ZenFieldH / 2f),
                                          (int)Hud.VaultLayout.ZenFieldW, (int)Hud.VaultLayout.ZenFieldH);
            _feeFieldRect = new Rectangle((int)Hud.VaultLayout.FeeFieldX,
                                          (int)(Hud.VaultLayout.FeeRowY - Hud.VaultLayout.FeeFieldH / 2f),
                                          (int)Hud.VaultLayout.FeeFieldW, (int)Hud.VaultLayout.FeeFieldH);
            _footerRect = new Rectangle(0, _zenFieldRect.Y, WINDOW_WIDTH, _feeFieldRect.Bottom - _zenFieldRect.Y);

            // Fileira de 4 botões: Deposit / Withdraw / Lock / Expanded.
            float bx = Hud.VaultLayout.BtnRowX, bw = Hud.VaultLayout.BtnW, bgap = Hud.VaultLayout.BtnGap;
            int by = (int)Hud.VaultLayout.BtnRowY, bh = (int)Hud.VaultLayout.BtnH;
            _depositButtonRect = new Rectangle((int)bx, by, (int)bw, bh);
            _withdrawButtonRect = new Rectangle((int)(bx + (bw + bgap)), by, (int)bw, bh);
            _lockButtonRect = new Rectangle((int)(bx + 2 * (bw + bgap)), by, (int)bw, bh);
            _expandedButtonRect = new Rectangle((int)(bx + 3 * (bw + bgap)), by, (int)bw, bh);
        }

        public override async Task Load()
        {
            await base.Load();
            _font = GraphicsManager.Instance.Font;

            var tl = TextureLoader.Instance;
            async Task<Texture2D> L(string p) { try { return await tl.PrepareAndGetTexture(p); } catch { return null; } }
            _texPanel = await L("Interface/Imprint/imprint_panel.OZP");
            _texRect = await L("Interface/Inventory/inv_rect.OZP");
            _texDarkCard = await L("Interface/Imprint/imprint_dark_card.OZP");
            _texGridRow = await L("Interface/Inventory/inv_grid_row.OZP");
            _texField = await L("Interface/Inventory/inv_field.OZP");
            _texVaultFrame = await L("Interface/Inventory/inv_vault_frame.OZP");  // moldura VAZADA (moldura2.png)
            _texTab = await L("Interface/Imprint/imprint_tab.OZP");               // aba vermelha (mesma do NPC Shop)
            _texClose = await L("Interface/Imprint/imprint_close.OZP");
            _texCloseHover = await L("Interface/Imprint/imprint_close_hover.OZP");
            _texOkCancel = await L("Interface/CharCreate/ok_cancel.OZT");

            InvalidateStaticSurface();
        }

        private static bool Tex(Texture2D t) => t != null && !t.IsDisposed;

        // 9-slice (borda m px) — moldura inv_rect (m=8).
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

        // Botão padrão do cliente: metade "Save" (verde) da folha ok_cancel 467x80.
        private void DrawStandardButton(SpriteBatch sb, Rectangle r, string label, float fontSize, bool hovered)
        {
            if (Tex(_texOkCancel)) sb.Draw(_texOkCancel, r, new Rectangle(0, 0, 233, 80), Color.White * Alpha);
            var pixel = GraphicsManager.Instance.Pixel;
            if (hovered && pixel != null) sb.Draw(pixel, r, Color.White * 0.12f);
            if (_font == null || string.IsNullOrEmpty(label)) return;
            float s = fontSize / 25f;
            Vector2 sz = _font.MeasureString(label) * s;
            float maxW = r.Width - 8f;
            if (sz.X > maxW && sz.X > 0f) { s *= maxW / sz.X; sz = _font.MeasureString(label) * s; }
            var pos = new Vector2(r.X + (r.Width - sz.X) / 2f, r.Y + (r.Height - sz.Y) / 2f);
            sb.DrawString(_font, label, pos + Vector2.One, Color.Black * 0.6f, 0f, Vector2.Zero, s, SpriteEffects.None, 0f);
            sb.DrawString(_font, label, pos, new Color(230, 225, 215), 0f, Vector2.Zero, s, SpriteEffects.None, 0f);
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            EnsureCharacterState();
            _currentGameTime = gameTime;

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
                if (!_wasVisible)
                {
                    _closeRequestSent = false;
                }

                if (_isZenInputActive &&
                    MuGame.Instance.Keyboard.IsKeyDown(Keys.Escape) &&
                    MuGame.Instance.PrevKeyboard.IsKeyUp(Keys.Escape))
                {
                    CancelZenInput();
                    Scene?.ConsumeKeyboardEscape();
                    return;
                }

                if (MuGame.Instance.Keyboard.IsKeyDown(Keys.Escape) &&
                    MuGame.Instance.PrevKeyboard.IsKeyUp(Keys.Escape))
                {
                    CloseWindow();
                    return;
                }

                Point mousePos = MuGame.Instance.UiMouseState.Position;
                bool leftPressed = MuGame.Instance.UiMouseState.LeftButton == ButtonState.Pressed;
                bool leftJustPressed = leftPressed && MuGame.Instance.PrevUiMouseState.LeftButton == ButtonState.Released;
                bool leftJustReleased = !leftPressed && MuGame.Instance.PrevUiMouseState.LeftButton == ButtonState.Pressed;

                UpdateChromeHover(mousePos);

                // Handle close button
                if (leftJustPressed && _closeHovered)
                {
                    CloseWindow();
                    return;
                }

                HandleZenInputKeyboard();

                // Handle window dragging
                if (leftJustPressed && IsMouseOverDragArea(mousePos) && !_isDragging && _draggedItem == null)
                {
                    DateTime now = DateTime.Now;
                    if ((now - _lastClickTime).TotalMilliseconds < 500)
                    {
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
                    if (HandleZenControlsMouseInput(mousePos, leftJustPressed))
                    {
                        Scene?.SetMouseInputConsumed();
                    }
                    else
                    {
                        UpdateHoverState();
                        HandleMouseInput();
                    }
                }

                PrecacheAnimatedPreviews();
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

            var zenRect = Translate(_zenFieldRect);
            _zenFieldHovered = zenRect.Contains(mousePos);

            var depRect = Translate(_depositButtonRect);
            _depositHovered = depRect.Contains(mousePos);

            var wdrRect = Translate(_withdrawButtonRect);
            _withdrawHovered = wdrRect.Contains(mousePos);

            _lockHovered = Translate(_lockButtonRect).Contains(mousePos);
            _expandedHovered = Translate(_expandedButtonRect).Contains(mousePos);
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

                DrawGridOverlays(spriteBatch);
                DrawVaultItems(spriteBatch);
                DrawCloseButton(spriteBatch);
                DrawPageTabs(spriteBatch);
                DrawZenButtons(spriteBatch);
                DrawZenText(spriteBatch);
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
                _characterState.VaultItemsChanged -= RefreshVaultContent;
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

        public void CloseWindow()
        {
            if (!Visible) return;
            Visible = false;
            HandleVisibilityLost();
            _wasVisible = false;
        }

        public InventoryItem GetDraggedItem() => _draggedItem;

        public void DrawPickedPreview(SpriteBatch spriteBatch, GameTime gameTime)
        {
            if (_draggedItem == null || spriteBatch == null) return;

            int w = _draggedItem.Definition.Width * VAULT_SQUARE_WIDTH;
            int h = _draggedItem.Definition.Height * VAULT_SQUARE_HEIGHT;

            var mouse = MuGame.Instance.UiMouseState.Position;
            var destRect = new Rectangle(mouse.X - w / 2, mouse.Y - h / 2, w, h);

            // Use cached previews only to avoid render-target switches while a sprite batch is active.
            Texture2D texture = ResolveItemTexture(_draggedItem, w, h, animated: false, allowGenerate: false)
                                ?? ResolveItemTexture(
                                    _draggedItem,
                                    _draggedItem.Definition.Width * InventoryControl.INVENTORY_SQUARE_WIDTH,
                                    _draggedItem.Definition.Height * InventoryControl.INVENTORY_SQUARE_HEIGHT,
                                    animated: false,
                                    allowGenerate: false);

            if (texture != null)
            {
                spriteBatch.Draw(texture, destRect, Color.White * 0.9f);
            }
            else if (GraphicsManager.Instance?.Pixel != null)
            {
                spriteBatch.Draw(GraphicsManager.Instance.Pixel, destRect, Theme.Accent * 0.8f);
            }
        }

        public Point GetSlotAtScreenPosition(Point screenPos)
        {
            // Converte a posição da TELA (linhas visíveis da página) para o slot LÓGICO
            // do servidor somando a linha inicial da página.
            var local = ItemGridRenderHelper.GetSlotAtScreenPosition(DisplayRectangle, _gridRect, Columns, VisibleRows,
                                                                     VAULT_SQUARE_WIDTH, VAULT_SQUARE_HEIGHT, screenPos);
            if (local.X < 0 || local.Y < 0) return new Point(-1, -1);
            return new Point(local.X, local.Y + PageFirstRow);
        }

        public bool CanPlaceAt(Point gridSlot, InventoryItem item)
        {
            if (item?.Definition == null) return false;

            for (int y = 0; y < item.Definition.Height; y++)
            {
                for (int x = 0; x < item.Definition.Width; x++)
                {
                    int gx = gridSlot.X + x;
                    int gy = gridSlot.Y + y;

                    if (gx < 0 || gx >= Columns || gy < 0 || gy >= Rows)
                        return false;

                    var occupant = _itemGrid[gx, gy];
                    if (occupant != null && occupant != item)
                        return false;
                }
            }

            return true;
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

            UiDrawHelper.DrawCornerAccents(spriteBatch, rect, Theme.Secondary * 0.5f);
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

        private void DrawFilledCircle(SpriteBatch spriteBatch, int centerX, int centerY, int radius, Color color)
        {
            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel == null || radius <= 0) return;

            for (int y = -radius; y <= radius; y++)
            {
                int halfWidth = (int)MathF.Sqrt(radius * radius - y * y);
                if (halfWidth > 0)
                {
                    spriteBatch.Draw(pixel, new Rectangle(centerX - halfWidth, centerY + y, halfWidth * 2, 1), color);
                }
            }
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
            _staticSurface = new RenderTarget2D(gd, (int)MathF.Ceiling(WINDOW_WIDTH * k), (int)MathF.Ceiling(WINDOW_HEIGHT * k),
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

            var fullRect = new Rectangle(0, 0, WINDOW_WIDTH, WINDOW_HEIGHT);

            // 1. Painel-template (barra de título assada). Fallback: fundo antigo.
            if (Tex(_texPanel)) spriteBatch.Draw(_texPanel, fullRect, Color.White);
            else DrawWindowBackground(spriteBatch, fullRect);

            // 2. Título sobre a barra.
            if (Hud.VaultLayout.TitleTextEnabled && _font != null)
            {
                string title = Hud.VaultLayout.TitleMessage;
                float ts = Hud.VaultLayout.TitleTextFont / 25f;
                Vector2 size = _font.MeasureString(title) * ts;
                var pos = new Vector2(Hud.VaultLayout.TitleTextX - size.X / 2f, Hud.VaultLayout.TitleTextY - size.Y / 2f);
                spriteBatch.DrawString(_font, title, pos + Vector2.One, Color.Black * 0.6f, 0f, Vector2.Zero, ts, SpriteEffects.None, 0f);
                spriteBatch.DrawString(_font, title, pos, new Color(235, 210, 150), 0f, Vector2.Zero, ts, SpriteEffects.None, 0f);
            }

            // 3. Dark card — peça INDEPENDENTE (posição/tamanho próprios).
            if (Hud.VaultLayout.DarkCardEnabled && Tex(_texDarkCard))
                spriteBatch.Draw(_texDarkCard, _darkCardRect, Color.White);
            if (Tex(_texGridRow))
            {
                // A arte tem 8 células: a 1ª traz a borda ESQUERDA e a 8ª a DIREITA.
                // Usar (c % 8) fazia a 9ª coluna repetir a célula-de-borda → GAP visível.
                // Mapa correto p/ qualquer nº de colunas: 0 = primeira, 7 = última,
                // miolo cicla só entre as células internas (1..6).
                const int SRC_CELLS = 8;
                int cellSrcW = _texGridRow.Width / SRC_CELLS;
                for (int r = 0; r < VisibleRows; r++)
                {
                    for (int c = 0; c < Columns; c++)
                    {
                        int srcIdx = c == 0 ? 0
                                   : c == Columns - 1 ? SRC_CELLS - 1
                                   : 1 + ((c - 1) % (SRC_CELLS - 2));
                        var cell = new Rectangle(_gridRect.X + c * VAULT_SQUARE_WIDTH,
                                                 _gridRect.Y + r * VAULT_SQUARE_HEIGHT,
                                                 VAULT_SQUARE_WIDTH, VAULT_SQUARE_HEIGHT);
                        var src = new Rectangle(cellSrcW * srcIdx, 0, cellSrcW, _texGridRow.Height);
                        spriteBatch.Draw(_texGridRow, cell, src, Color.White);
                    }
                }
            }

            // 3b. Moldura VAZADA — peça INDEPENDENTE, por cima da grade (miolo transparente).
            if (Hud.VaultLayout.CardEnabled && Tex(_texVaultFrame))
                Draw9Slice(spriteBatch, _texVaultFrame, _gridFrameRect, 30);

            // 4. Linhas Zen / Storage fee: rótulo + campo (arte de input padrão).
            if (Hud.VaultLayout.ZenRowEnabled)
                DrawFieldRow(spriteBatch, "Zen", Hud.VaultLayout.ZenLabelX, Hud.VaultLayout.ZenRowY, _zenFieldRect);
            if (Hud.VaultLayout.FeeRowEnabled)
                DrawFieldRow(spriteBatch, "Storage fee", Hud.VaultLayout.FeeLabelX, Hud.VaultLayout.FeeRowY, _feeFieldRect);
        }

        // Rótulo dourado + moldura do campo (o VALOR é dinâmico, desenhado por cima).
        private void DrawFieldRow(SpriteBatch sb, string label, float labelX, float rowY, Rectangle field)
        {
            if (Tex(_texField)) sb.Draw(_texField, field, Color.White);
            if (_font == null) return;
            float s = Hud.VaultLayout.ZenFont / 25f;
            Vector2 sz = _font.MeasureString(label) * s;
            var pos = new Vector2(labelX, rowY - sz.Y / 2f);
            sb.DrawString(_font, label, pos + Vector2.One, Color.Black * 0.6f, 0f, Vector2.Zero, s, SpriteEffects.None, 0f);
            sb.DrawString(_font, label, pos, new Color(230, 200, 110), 0f, Vector2.Zero, s, SpriteEffects.None, 0f);
        }

        private void DrawModernHeader(SpriteBatch spriteBatch)
        {
            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel == null) return;

            var headerBg = new Rectangle(8, 6, WINDOW_WIDTH - 16, HEADER_HEIGHT - 8);
            DrawPanel(spriteBatch, headerBg, Theme.BgMid);

            spriteBatch.Draw(pixel, new Rectangle(20, 8, WINDOW_WIDTH - 40, 2), Theme.Secondary * 0.8f);
            spriteBatch.Draw(pixel, new Rectangle(30, 10, WINDOW_WIDTH - 60, 1), Theme.Secondary * 0.3f);

            if (_font != null)
            {
                string title = "VAULT";
                float scale = 0.50f;
                Vector2 size = _font.MeasureString(title) * scale;
                Vector2 pos = new((WINDOW_WIDTH - size.X) / 2, (HEADER_HEIGHT - size.Y) / 2 + 2);

                spriteBatch.Draw(pixel, new Rectangle((int)pos.X - 20, (int)pos.Y - 4, (int)size.X + 40, (int)size.Y + 8),
                                new Color(90, 140, 200, 30));

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

            DrawSectionHeader(spriteBatch, "STORED ITEMS", _gridFrameRect.X, _gridFrameRect.Y + 4, _gridFrameRect.Width);
            DrawPanel(spriteBatch, _gridFrameRect, Theme.BgMid);

            spriteBatch.Draw(pixel, _gridRect, Theme.SlotBg);

            spriteBatch.Draw(pixel, new Rectangle(_gridRect.X, _gridRect.Y, _gridRect.Width, 2), Color.Black * 0.4f);
            spriteBatch.Draw(pixel, new Rectangle(_gridRect.X, _gridRect.Y, 2, _gridRect.Height), Color.Black * 0.3f);

            Color gridLine = new(40, 48, 60, 100);
            Color gridLineMajor = new(55, 65, 80, 120);

            for (int x = 1; x < Columns; x++)
            {
                int lineX = _gridRect.X + x * VAULT_SQUARE_WIDTH;
                bool isMajor = x == Columns / 2;
                spriteBatch.Draw(pixel, new Rectangle(lineX, _gridRect.Y, 1, _gridRect.Height), isMajor ? gridLineMajor : gridLine);
            }

            for (int y = 1; y < Rows; y++)
            {
                int lineY = _gridRect.Y + y * VAULT_SQUARE_HEIGHT;
                bool isMajor = y == Rows / 2;
                spriteBatch.Draw(pixel, new Rectangle(_gridRect.X, lineY, _gridRect.Width, 1), isMajor ? gridLineMajor : gridLine);
            }

            spriteBatch.Draw(pixel, new Rectangle(_gridRect.X, _gridRect.Bottom - 1, _gridRect.Width, 1), Theme.BorderHighlight * 0.2f);
            spriteBatch.Draw(pixel, new Rectangle(_gridRect.Right - 1, _gridRect.Y, 1, _gridRect.Height), Theme.BorderHighlight * 0.15f);
        }

        private void DrawModernFooter(SpriteBatch spriteBatch)
        {
            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel == null) return;

            int sepY = _footerRect.Y - 4;
            UiDrawHelper.DrawHorizontalGradient(spriteBatch, new Rectangle(30, sepY, (WINDOW_WIDTH - 60) / 2, 1),
                                  Color.Transparent, Theme.Secondary * 0.4f);
            UiDrawHelper.DrawHorizontalGradient(spriteBatch, new Rectangle(WINDOW_WIDTH / 2, sepY, (WINDOW_WIDTH - 60) / 2, 1),
                                  Theme.Secondary * 0.4f, Color.Transparent);

            DrawPanel(spriteBatch, _footerRect, Theme.BgMid);

            // Zen coin icon
            int coinX = _footerRect.X + 18;
            int coinY = _footerRect.Y + _footerRect.Height / 2;

            DrawFilledCircle(spriteBatch, coinX, coinY, 10, Theme.AccentDim);
            DrawFilledCircle(spriteBatch, coinX, coinY, 7, Theme.Accent);
            DrawFilledCircle(spriteBatch, coinX - 2, coinY - 2, 3, Theme.AccentBright * 0.6f);

            // Zen field background
            DrawPanel(spriteBatch, _zenFieldRect, Theme.SlotBg);
            var innerField = new Rectangle(_zenFieldRect.X + 2, _zenFieldRect.Y + 2,
                                           _zenFieldRect.Width - 4, _zenFieldRect.Height - 4);
            spriteBatch.Draw(pixel, innerField, Theme.BgDarkest * 0.7f);
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

        private void DrawZenText(SpriteBatch spriteBatch)
        {
            if (_font == null) return;

            var zenRect = Translate(_zenFieldRect);
            var pixel = GraphicsManager.Instance.Pixel;

            if (_isZenInputActive && pixel != null)
            {
                var borderColor = Theme.AccentBright * 0.9f;
                spriteBatch.Draw(pixel, new Rectangle(zenRect.X, zenRect.Y, zenRect.Width, 2), borderColor);
                spriteBatch.Draw(pixel, new Rectangle(zenRect.X, zenRect.Bottom - 2, zenRect.Width, 2), borderColor);
                spriteBatch.Draw(pixel, new Rectangle(zenRect.X, zenRect.Y, 2, zenRect.Height), borderColor);
                spriteBatch.Draw(pixel, new Rectangle(zenRect.Right - 2, zenRect.Y, 2, zenRect.Height), borderColor);
            }

            string zenText = _isZenInputActive
                ? $"{(_zenMoveDirection == VaultMoveMoneyRequest.VaultMoneyMoveDirection.InventoryToVault ? "IN" : "OUT")}: {(_zenInputText.Length == 0 ? "0" : _zenInputText)}{(_zenInputShowCursor ? "|" : string.Empty)}"
                : _vaultZen.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);

            // Alinhado à DIREITA crescendo pra esquerda (padrão dos campos de moeda),
            // com auto-shrink pra nunca estourar o campo.
            float scale = Hud.VaultLayout.ZenFont / 25f;
            Vector2 size = _font.MeasureString(zenText) * scale;
            float maxW = zenRect.Width - 16f;
            if (size.X > maxW && size.X > 0f) { scale *= maxW / size.X; size = _font.MeasureString(zenText) * scale; }
            Vector2 pos = new(zenRect.Right - 8f - size.X, zenRect.Y + (zenRect.Height - size.Y) / 2);

            spriteBatch.DrawString(_font, zenText, pos + Vector2.One, Color.Black * 0.6f,
                                  0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            spriteBatch.DrawString(_font, zenText, pos, new Color(212, 62, 42) * Alpha,
                                  0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

            // Storage fee (dado do servidor; sem taxa configurada = 0).
            var feeRect = Translate(_feeFieldRect);
            string feeText = "0";
            float fs = Hud.VaultLayout.ZenFont / 25f;
            Vector2 fsz = _font.MeasureString(feeText) * fs;
            Vector2 fpos = new(feeRect.Right - 8f - fsz.X, feeRect.Y + (feeRect.Height - fsz.Y) / 2);
            spriteBatch.DrawString(_font, feeText, fpos + Vector2.One, Color.Black * 0.6f,
                                  0f, Vector2.Zero, fs, SpriteEffects.None, 0f);
            spriteBatch.DrawString(_font, feeText, fpos, new Color(212, 62, 42) * Alpha,
                                  0f, Vector2.Zero, fs, SpriteEffects.None, 0f);
        }

        // Abas de página (1, 2, ...) — como nas lojas de NPC. Desenhadas AO VIVO
        // porque a aba ativa muda sem invalidar a superfície estática.
        private void DrawPageTabs(SpriteBatch spriteBatch)
        {
            if (!Hud.VaultLayout.PageTabsEnabled || _font == null || PageCount <= 1) return;
            var pixel = GraphicsManager.Instance.Pixel;

            for (int i = 0; i < PageCount; i++)
            {
                var r = Translate(PageTabRect(i));
                bool active = i == _page;

                // Aba VERMELHA — mesma arte do NPC Shop (imprint_tab). Inativa fica esmaecida.
                if (Tex(_texTab))
                    spriteBatch.Draw(_texTab, r, Color.White * (active ? 1f : 0.6f) * Alpha);
                else if (Tex(_texOkCancel))
                    spriteBatch.Draw(_texOkCancel, r, new Rectangle(0, 0, 233, 80), Color.White * (active ? 1f : 0.55f) * Alpha);

                string lbl = (i + 1).ToString();
                float s = Hud.VaultLayout.PageTabFont / 25f;
                var sz = _font.MeasureString(lbl) * s;
                var pos = new Vector2(r.X + (r.Width - sz.X) / 2f, r.Y + (r.Height - sz.Y) / 2f);
                spriteBatch.DrawString(_font, lbl, pos + Vector2.One, Color.Black * 0.6f, 0f, Vector2.Zero, s, SpriteEffects.None, 0f);
                spriteBatch.DrawString(_font, lbl, pos, active ? new Color(235, 210, 150) : new Color(170, 160, 145),
                                       0f, Vector2.Zero, s, SpriteEffects.None, 0f);
            }
        }

        private static Rectangle PageTabRect(int i)
            => new((int)(Hud.VaultLayout.PageTabX + i * (Hud.VaultLayout.PageTabW + Hud.VaultLayout.PageTabGap)),
                   (int)Hud.VaultLayout.PageTabY,
                   (int)Hud.VaultLayout.PageTabW, (int)Hud.VaultLayout.PageTabH);

        // Clique nas abas: troca a página exibida (slots do servidor não mudam).
        private bool HandlePageTabClick(Point mousePos)
        {
            if (!Hud.VaultLayout.PageTabsEnabled || PageCount <= 1) return false;
            for (int i = 0; i < PageCount; i++)
            {
                if (!Translate(PageTabRect(i)).Contains(mousePos)) continue;
                if (_page != i)
                {
                    _page = i;
                    SoundController.Instance.PlayBuffer("Sound/iButton.wav");
                }
                return true;
            }
            return false;
        }

        private void DrawZenButtons(SpriteBatch spriteBatch)
        {
            if (!Hud.VaultLayout.ButtonsEnabled || _font == null) return;

            float f = Hud.VaultLayout.BtnFont;
            DrawStandardButton(spriteBatch, Translate(_depositButtonRect), Hud.VaultLayout.Btn1Label, f, _depositHovered);
            DrawStandardButton(spriteBatch, Translate(_withdrawButtonRect), Hud.VaultLayout.Btn2Label, f, _withdrawHovered);
            DrawStandardButton(spriteBatch, Translate(_lockButtonRect), Hud.VaultLayout.Btn3Label, f, _lockHovered);
            DrawStandardButton(spriteBatch, Translate(_expandedButtonRect), Hud.VaultLayout.Btn4Label, f, _expandedHovered);

            // Realce da direção ativa do movimento de Zen (IN = Deposit, OUT = Withdraw).
            var pixel = GraphicsManager.Instance.Pixel;
            if (_isZenInputActive && pixel != null)
            {
                var active = _zenMoveDirection == VaultMoveMoneyRequest.VaultMoneyMoveDirection.InventoryToVault
                    ? Translate(_depositButtonRect) : Translate(_withdrawButtonRect);
                spriteBatch.Draw(pixel, new Rectangle(active.X, active.Y, active.Width, 2), Theme.Accent);
                spriteBatch.Draw(pixel, new Rectangle(active.X, active.Bottom - 2, active.Width, 2), Theme.Accent);
                spriteBatch.Draw(pixel, new Rectangle(active.X, active.Y, 2, active.Height), Theme.Accent);
                spriteBatch.Draw(pixel, new Rectangle(active.Right - 2, active.Y, 2, active.Height), Theme.Accent);
            }
        }

        private void DrawZenButton(SpriteBatch spriteBatch, Rectangle localRect, string label, bool hovered, VaultMoveMoneyRequest.VaultMoneyMoveDirection direction)
        {
            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel == null || _font == null) return;

            var rect = Translate(localRect);
            bool selected = _isZenInputActive && _zenMoveDirection == direction;

            Color bg = Theme.BgDarkest * 0.7f;
            if (hovered) bg = Color.Lerp(bg, Theme.BgLight, 0.25f);
            if (selected) bg = Color.Lerp(bg, Theme.Secondary, 0.45f);

            Color border = selected ? Theme.Accent : Theme.BorderInner;

            spriteBatch.Draw(pixel, rect, bg);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 2), border);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2), border);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 2, rect.Height), border);
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - 2, rect.Y, 2, rect.Height), border);

            float scale = 0.30f;
            Vector2 size = _font.MeasureString(label) * scale;
            Vector2 pos = new(rect.X + (rect.Width - size.X) / 2, rect.Y + (rect.Height - size.Y) / 2);
            Color textColor = selected ? Theme.TextWhite : Theme.TextGray;

            spriteBatch.DrawString(_font, label, pos + Vector2.One, Color.Black * 0.6f, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            spriteBatch.DrawString(_font, label, pos, textColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        private void DrawGridOverlays(SpriteBatch spriteBatch)
        {
            var pixel = GraphicsManager.Instance.Pixel;
            if (pixel == null) return;

            Point gridOrigin = new(DisplayRectangle.X + _gridRect.X, DisplayRectangle.Y + _gridRect.Y);
            var activeDragged = _draggedItem ?? InventoryControl.Instance?.GetDraggedItem();

            // Drag preview highlight
            if (activeDragged != null && _pendingDropSlot.X >= 0)
            {
                // Match inventory: highlight the entire footprint (green=valid, red=invalid)
                for (int y = 0; y < Rows; y++)
                {
                    for (int x = 0; x < Columns; x++)
                    {
                        var highlight = GetSlotHighlightColor(new Point(x, y), activeDragged);
                        if (!highlight.HasValue)
                            continue;

                        var rect = new Rectangle(
                            gridOrigin.X + x * VAULT_SQUARE_WIDTH,
                            gridOrigin.Y + y * VAULT_SQUARE_HEIGHT,
                            VAULT_SQUARE_WIDTH, VAULT_SQUARE_HEIGHT);
                        spriteBatch.Draw(pixel, rect, highlight.Value);
                    }
                }
            }

            // Sem highlight de hover/célula fora do drag (mobile é touch; o quadrado claro
            // ficava preso no último toque e usava dims cruas — "vazava" do slot).
        }

        // Dimensões EFETIVAS do item na grade (só VISUAL — drag/moves continuam com as
        // dims reais): clampa na borda da grade, no FIM DA PÁGINA visível (item 2x2+ na
        // última linha da página desenhava por cima da moldura/Zen) e no vizinho ocupado.
        // Mesmo padrão do EffectiveGridDims do InventoryControl.
        private (int W, int H) EffectiveVaultDims(InventoryItem item, int rowOnPage)
        {
            int x = item.GridPosition.X, y = item.GridPosition.Y;
            int defW = Math.Max(1, item.Definition?.Width ?? 1);
            int defH = Math.Max(1, item.Definition?.Height ?? 1);
            int w = Math.Min(defW, Columns - x);
            int h = Math.Min(defH, Math.Min(Rows - y, VisibleRows - rowOnPage));

            foreach (var other in _items)
            {
                if (ReferenceEquals(other, item)) continue;
                int ox = other.GridPosition.X, oy = other.GridPosition.Y;
                if (oy >= y && oy < y + defH && ox > x) w = Math.Min(w, ox - x);
                if (ox >= x && ox < x + defW && oy > y) h = Math.Min(h, oy - y);
            }

            return (Math.Max(1, w), Math.Max(1, h));
        }

        private void DrawVaultItems(SpriteBatch spriteBatch)
        {
            var font = _font ?? GraphicsManager.Instance.Font;
            Point gridOrigin = new(DisplayRectangle.X + _gridRect.X, DisplayRectangle.Y + _gridRect.Y);
            var pixel = GraphicsManager.Instance.Pixel;
            var jewelEntries = new List<(InventoryItem Item, Rectangle Rect)>();

            foreach (var item in _items)
            {
                if (item == _draggedItem) continue;

                // Só desenha o que pertence à PÁGINA atual.
                int rowOnPage = item.GridPosition.Y - PageFirstRow;
                if (rowOnPage < 0 || rowOnPage >= VisibleRows) continue;

                var (effW, effH) = EffectiveVaultDims(item, rowOnPage);
                var rect = new Rectangle(
                    gridOrigin.X + item.GridPosition.X * VAULT_SQUARE_WIDTH,
                    gridOrigin.Y + rowOnPage * VAULT_SQUARE_HEIGHT,
                    effW * VAULT_SQUARE_WIDTH,
                    effH * VAULT_SQUARE_HEIGHT);

                // FUNDO alinhado ao footprint (cravado nas linhas da grade, como o mobile
                // oficial); só o ÍCONE tem margem, e o glow fica DENTRO (nunca por cima
                // dos vizinhos — era isso que "vazava do slot").
                bool isHovered = item == _hoveredItem;
                Texture2D texture = ResolveItemTexture(item, rect.Width - ICON_INSET * 2, rect.Height - ICON_INSET * 2, isHovered, allowGenerate: true);

                // Cell background — footprint inteiro (1px de folga pra linha da grade).
                if (pixel != null)
                {
                    var bgRect = new Rectangle(rect.X + 1, rect.Y + 1, rect.Width - 2, rect.Height - 2);
                    spriteBatch.Draw(pixel, bgRect, isHovered ? Theme.SlotHover : Theme.SlotBg);
                }

                // Glow similar to inventory — para DENTRO, sem cobrir a linha da grade.
                Color glowColor = ItemUiHelper.GetItemGlowColor(item, GlowPalette);
                if (glowColor.A > 0 || isHovered)
                {
                    Color finalGlow = isHovered ? Color.Lerp(glowColor, Theme.Accent, 0.4f) : glowColor;
                    finalGlow.A = (byte)Math.Min(255, finalGlow.A + (isHovered ? 40 : 0));
                    var glowRect = new Rectangle(rect.X + 1, rect.Y + 1, rect.Width - 2, rect.Height - 2);
                    ItemUiHelper.DrawItemGlow(spriteBatch, pixel, glowRect, finalGlow);
                }

                if (texture != null)
                {
                    Rectangle iconRect = ItemGridRenderHelper.FitRect(rect, texture.Width, texture.Height, ICON_INSET);
                    spriteBatch.Draw(texture, iconRect, Color.White * Alpha);

                    if (JewelShineOverlay.ShouldShine(item))
                    {
                        jewelEntries.Add((item, iconRect));
                    }
                }
                else if (pixel != null)
                {
                    var phRect = new Rectangle(rect.X + 1, rect.Y + 1, rect.Width - 2, rect.Height - 2);
                    ItemGridRenderHelper.DrawItemPlaceholder(spriteBatch, pixel, font, phRect, item, Theme.BgLight, Theme.TextGray * 0.8f);
                }

                if (font != null && item.Definition.BaseDurability == 0 && item.Definition.MagicDurability == 0 && item.Durability > 1)
                {
                    ItemGridRenderHelper.DrawItemStackCount(spriteBatch, font, rect, item.Durability, Theme.TextGold, Alpha);
                }

                if (font != null && item.Details.Level > 0)
                {
                    ItemGridRenderHelper.DrawItemLevelBadge(spriteBatch, pixel, font, rect, item.Details.Level,
                                       lvl => lvl >= 9 ? Theme.Danger :
                                              lvl >= 7 ? Theme.Accent :
                                              lvl >= 4 ? Theme.AccentDim :
                                              Theme.TextGray,
                                       new Color(0, 0, 0, 180));
                }
            }

            if (jewelEntries.Count > 0)
            {
                JewelShineOverlay.DrawBatch(spriteBatch, jewelEntries, _currentGameTime, Alpha, UiScaler.SpriteTransform);
            }
        }

        private void DrawTooltip(SpriteBatch spriteBatch)
        {
            if (_hoveredItem == null || _font == null) return;

            var lines = ItemUiHelper.BuildTooltipLines(_hoveredItem);
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
            totalHeight += 6; // separator after first line

            int tooltipWidth = maxWidth + paddingX * 2;
            int tooltipHeight = totalHeight + paddingY * 2;

            Point mouse = MuGame.Instance.UiMouseState.Position;
            var itemRect = new Rectangle(
                DisplayRectangle.X + _gridRect.X + _hoveredItem.GridPosition.X * VAULT_SQUARE_WIDTH,
                DisplayRectangle.Y + _gridRect.Y + (_hoveredItem.GridPosition.Y - PageFirstRow) * VAULT_SQUARE_HEIGHT,
                _hoveredItem.Definition.Width * VAULT_SQUARE_WIDTH,
                _hoveredItem.Definition.Height * VAULT_SQUARE_HEIGHT);

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

            var shadowRect = new Rectangle(tooltipRect.X + 4, tooltipRect.Y + 4, tooltipRect.Width, tooltipRect.Height);
            spriteBatch.Draw(pixel, shadowRect, Color.Black * 0.5f);

            UiDrawHelper.DrawVerticalGradient(spriteBatch, tooltipRect, new Color(20, 24, 32, 252), new Color(12, 14, 18, 254));

            bool isExcellent = _hoveredItem.Details.IsExcellent;
            bool isAncient = _hoveredItem.Details.IsAncient;
            bool isHighLevel = _hoveredItem.Details.Level >= 7;

            Color borderColor = isExcellent ? Theme.GlowExcellent :
                                isAncient ? Theme.GlowAncient :
                                isHighLevel ? Theme.Accent :
                                Theme.TextWhite;

            const int borderThickness = 2;
            spriteBatch.Draw(pixel, new Rectangle(tooltipRect.X, tooltipRect.Y, tooltipRect.Width, borderThickness), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(tooltipRect.X, tooltipRect.Bottom - borderThickness, tooltipRect.Width, borderThickness), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(tooltipRect.X, tooltipRect.Y, borderThickness, tooltipRect.Height), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(tooltipRect.Right - borderThickness, tooltipRect.Y, borderThickness, tooltipRect.Height), borderColor);

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

            Point mousePos = mouse.Position;

            if (_draggedItem == null)
            {
                if (_hoveredItem != null)
                {
                    BeginDrag(_hoveredItem);
                    Scene?.SetMouseInputConsumed();
                }
                return;
            }

            AttemptDrop(mousePos);
            Scene?.SetMouseInputConsumed();
        }

        private void UpdateHoverState()
        {
            var mouse = MuGame.Instance.UiMouseState.Position;
            var externalDragged = InventoryControl.Instance?.GetDraggedItem();

            if (_draggedItem != null)
            {
                var dropSlot = GetSlotAtScreenPosition(mouse);
                _pendingDropSlot = dropSlot.X >= 0 ? dropSlot : new Point(-1, -1);
                _hoveredItem = null;
                _hoveredSlot = dropSlot;
                return;
            }
            else if (externalDragged != null)
            {
                var dropSlot = GetSlotAtScreenPosition(mouse);
                _pendingDropSlot = dropSlot.X >= 0 ? dropSlot : new Point(-1, -1);
                _hoveredItem = null;
                _hoveredSlot = dropSlot;
                return;
            }

            _hoveredSlot = GetSlotAtScreenPosition(mouse);
            _hoveredItem = GetItemAt(mouse);
        }

        private void PrecacheAnimatedPreviews()
        {
            if (_currentGameTime == null)
                return;

            CacheAnimatedPreview(_hoveredItem);
            CacheAnimatedPreview(_draggedItem);
        }

        private void CacheAnimatedPreview(InventoryItem item)
        {
            if (item == null)
                return;

            // MESMO tamanho que o hover do DrawVaultItems pede (dims efetivas - inset).
            int rowOnPage = item.GridPosition.Y - PageFirstRow;
            var (effW, effH) = EffectiveVaultDims(item, Math.Max(0, rowOnPage));
            int w = effW * VAULT_SQUARE_WIDTH - ICON_INSET * 2;
            int h = effH * VAULT_SQUARE_HEIGHT - ICON_INSET * 2;
            var key = (item, w, h, true);
            if (_bmdPreviewCache.TryGetValue(key, out var existing) && existing != null)
            {
                return;
            }

            try
            {
                var animated = BmdPreviewRenderer.GetAnimatedPreview(item, w, h, _currentGameTime);
                if (animated != null)
                {
                    _bmdPreviewCache[key] = animated;
                }
            }
            catch
            {
                // ignore and leave cache empty; will fall back to static
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // DRAG & DROP
        // ═══════════════════════════════════════════════════════════════

        private void BeginDrag(InventoryItem item)
        {
            _draggedItem = item;
            _draggedOriginalSlot = item.GridPosition;
            RemoveItemFromGrid(item);
            _hoveredItem = null;
            _pendingDropSlot = new Point(-1, -1);

            // Pre-cache drag preview while we're outside the draw pass to avoid render-target thrashing.
            int w = item.Definition.Width * VAULT_SQUARE_WIDTH;
            int h = item.Definition.Height * VAULT_SQUARE_HEIGHT;
            _ = ResolveItemTexture(item, w, h, animated: false);
        }

        private void AttemptDrop(Point mousePos)
        {
            if (_draggedItem == null) return;

            var dropSlot = GetSlotAtScreenPosition(mousePos);
            var inventory = InventoryControl.Instance;
            bool dropped = false;

            if (dropSlot.X >= 0 && CanPlaceAt(dropSlot, _draggedItem))
            {
                PlaceDraggedItem(dropSlot);
                // MESMO tamanho que o DrawVaultItems pede (dims efetivas - inset).
                int dropRow = _draggedItem.GridPosition.Y - PageFirstRow;
                var (dw, dh) = EffectiveVaultDims(_draggedItem, Math.Max(0, dropRow));
                _ = ResolveItemTexture(_draggedItem,
                    dw * VAULT_SQUARE_WIDTH - ICON_INSET * 2, dh * VAULT_SQUARE_HEIGHT - ICON_INSET * 2,
                    animated: Constants.ENABLE_ITEM_MATERIAL_ANIMATION);
                if (dropSlot != _draggedOriginalSlot)
                {
                    SendVaultMove(_draggedOriginalSlot, dropSlot);
                }
                dropped = true;
            }
            else if (inventory != null && inventory.Visible && inventory.DisplayRectangle.Contains(mousePos))
            {
                Point invSlot = inventory.GetSlotAtScreenPositionPublic(mousePos);
                if (invSlot.X >= 0 && inventory.CanPlaceAt(invSlot, _draggedItem))
                {
                    MoveItemToInventory(invSlot, inventory);
                    dropped = true;
                }
            }

            if (!dropped)
            {
                ReturnDraggedItem();
            }

            _draggedItem = null;
            _draggedOriginalSlot = new Point(-1, -1);
            _pendingDropSlot = new Point(-1, -1);
        }

        private void ReturnDraggedItem()
        {
            if (_draggedItem != null && _draggedOriginalSlot.X >= 0)
            {
                PlaceItemOnGrid(_draggedItem, _draggedOriginalSlot);
            }
        }

        private void PlaceDraggedItem(Point newSlot)
        {
            if (_draggedItem == null) return;
            PlaceItemOnGrid(_draggedItem, newSlot);
        }

        private void MoveItemToInventory(Point targetSlot, InventoryControl inventory)
        {
            if (_draggedItem == null) return;

            byte fromSlot = (byte)(_draggedOriginalSlot.Y * Columns + _draggedOriginalSlot.X);
            byte toSlot = (byte)(InventoryControl.InventorySlotOffsetConstant +
                                 (targetSlot.Y * InventoryControl.Columns) + targetSlot.X);

            var svc = _networkManager?.GetCharacterService();
            var state = _networkManager?.GetCharacterState();
            var version = _networkManager?.TargetVersion ?? TargetProtocolVersion.Season6;

            if (svc != null && state != null)
            {
                state.StashPendingVaultMove(fromSlot, 0xFF);

                var raw = _draggedItem.RawData ?? Array.Empty<byte>();

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await svc.SendStorageItemMoveAsync(ItemStorageKind.Vault, fromSlot, ItemStorageKind.Inventory, toSlot, version, raw);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Failed to move item from vault to inventory.");
                    }

                    await Task.Delay(1200);

                    if (_networkManager != null && state.IsVaultMovePending(fromSlot, 0xFF))
                    {
                        MuGame.ScheduleOnMainThread(state.RaiseVaultItemsChanged);
                    }

                    if (_networkManager != null && state.IsInventoryMovePending(toSlot, toSlot))
                    {
                        MuGame.ScheduleOnMainThread(state.RaiseInventoryChanged);
                    }
                });
            }

            _items.Remove(_draggedItem);
            inventory?.BringToFront();
        }

        private void SendVaultMove(Point fromSlot, Point toSlot)
        {
            byte from = (byte)(fromSlot.Y * Columns + fromSlot.X);
            byte to = (byte)(toSlot.Y * Columns + toSlot.X);

            if (_networkManager == null) return;

            var svc = _networkManager.GetCharacterService();
            var state = _networkManager.GetCharacterState();

            if (svc == null || state == null) return;

            state.StashPendingVaultMove(from, to);

            var raw = _draggedItem?.RawData ?? Array.Empty<byte>();
            var version = _networkManager.TargetVersion;

            _ = Task.Run(async () =>
            {
                try
                {
                    await svc.SendStorageItemMoveAsync(ItemStorageKind.Vault, from, ItemStorageKind.Vault, to, version, raw);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to move item inside vault.");
                }

                await Task.Delay(1200);
                if (_networkManager != null && state.IsVaultMovePending(from, to))
                {
                    MuGame.ScheduleOnMainThread(state.RaiseVaultItemsChanged);
                }
            });
        }

        // ═══════════════════════════════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════════════════════════════

        private Rectangle Translate(Rectangle rect)
            => new(DisplayRectangle.X + rect.X, DisplayRectangle.Y + rect.Y, rect.Width, rect.Height);

        private InventoryItem GetItemAt(Point mousePos)
        {
            if (!DisplayRectangle.Contains(mousePos)) return null;

            Point gridOrigin = new(DisplayRectangle.X + _gridRect.X, DisplayRectangle.Y + _gridRect.Y);

            foreach (var item in _items)
            {
                if (item == _draggedItem) continue;

                // Só considera itens da PÁGINA visível.
                int rowOnPage = item.GridPosition.Y - PageFirstRow;
                if (rowOnPage < 0 || rowOnPage >= VisibleRows) continue;

                // Mesmas dims EFETIVAS do desenho, pro hover casar com o visual.
                var (effW, effH) = EffectiveVaultDims(item, rowOnPage);
                var rect = new Rectangle(
                    gridOrigin.X + item.GridPosition.X * VAULT_SQUARE_WIDTH,
                    gridOrigin.Y + rowOnPage * VAULT_SQUARE_HEIGHT,
                    effW * VAULT_SQUARE_WIDTH,
                    effH * VAULT_SQUARE_HEIGHT);

                if (rect.Contains(mousePos)) return item;
            }

            return null;
        }

        private void HandleVisibilityLost()
        {
            CancelZenInput();
            SendCloseNpcRequest();
            _characterState?.ClearVaultItems();
            _items.Clear();
            ClearGrid();
            _itemTextureCache.Clear();
            _bmdPreviewCache.Clear();
            _draggedItem = null;
            _hoveredItem = null;
            _pendingDropSlot = new Point(-1, -1);
            _isDragging = false;
            _pendingShow = false;
            _warmupComplete = false;
        }

        public void SetVaultZen(uint amount)
        {
            _vaultZen = amount;
        }

        private void SendCloseNpcRequest()
        {
            if (_closeRequestSent) return;
            _closeRequestSent = true;
            var svc = _networkManager?.GetCharacterService();
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
                _characterState.VaultItemsChanged += RefreshVaultContent;
            }
        }

        private void RefreshVaultContent()
        {
            if (_characterState == null) return;

            bool wasVisible = Visible;

            _items.Clear();
            ClearGrid();

            var vaultItems = _characterState.GetVaultItems();
            foreach (var kv in vaultItems)
            {
                byte slot = kv.Key;
                byte[] data = kv.Value;

                int gridX = slot % Columns;
                int gridY = slot / Columns;

                var def = ItemDatabase.GetItemDefinition(data)
                    ?? new ItemDefinition(0, ItemDatabase.GetItemName(data) ?? "Unknown Item", 1, 1, "Interface/newui_item_box.tga");

                var item = new InventoryItem(def, new Point(gridX, gridY), data);
                if (data.Length > 2)
                {
                    item.Durability = data[2];
                }

                _items.Add(item);
                PlaceItemOnGrid(item, item.GridPosition);
            }

            foreach (var item in _items)
            {
                if (!string.IsNullOrEmpty(item.Definition.TexturePath) &&
                    !item.Definition.TexturePath.EndsWith(".bmd", StringComparison.OrdinalIgnoreCase))
                {
                    _ = TextureLoader.Instance.Prepare(item.Definition.TexturePath);
                }
            }

            if (_items.Count > 0 || _vaultZen > 0)
            {
                // Align left with padding before showing, then freeze position to avoid auto realignment
                ForceAlignNow();
                Align = ControlAlign.None;
                // Use deferred show - warmup happens in Update(), window shows one frame later
                // to avoid black screen flicker from render target switches during Draw().
                // Only trigger pending show if not already visible (to avoid re-triggering sound on refresh)
                if (!wasVisible)
                {
                    _pendingShow = true;
                    _warmupComplete = false;
                }
                _closeRequestSent = false;
                _isDragging = false;
            }
        }

        private bool HandleZenControlsMouseInput(Point mousePos, bool leftJustPressed)
        {
            if (_draggedItem != null)
            {
                return false;
            }

            if (_isZenInputActive)
            {
                if (!leftJustPressed)
                {
                    return false;
                }

                if (_depositHovered)
                {
                    BeginZenInput(VaultMoveMoneyRequest.VaultMoneyMoveDirection.InventoryToVault);
                    return true;
                }

                if (_withdrawHovered)
                {
                    BeginZenInput(VaultMoveMoneyRequest.VaultMoneyMoveDirection.VaultToInventory);
                    return true;
                }

                if (_zenFieldHovered)
                {
                    return true;
                }

                CancelZenInput();
                return true;
            }

            if (!leftJustPressed)
            {
                return false;
            }

            // Abas de página têm prioridade (ficam acima da grade).
            if (HandlePageTabClick(MuGame.Instance.UiMouseState.Position))
            {
                return true;
            }

            if (_depositHovered)
            {
                BeginZenInput(VaultMoveMoneyRequest.VaultMoneyMoveDirection.InventoryToVault);
                return true;
            }

            if (_withdrawHovered)
            {
                BeginZenInput(VaultMoveMoneyRequest.VaultMoneyMoveDirection.VaultToInventory);
                return true;
            }

            // Lock / Expanded: sem pacote no OpenMU ainda — consomem o clique (placeholder).
            if (_lockHovered || _expandedHovered)
            {
                SoundController.Instance.PlayBuffer("Sound/iButton.wav");
                return true;
            }

            if (_zenFieldHovered)
            {
                BeginZenInput(_zenMoveDirection);
                return true;
            }

            return false;
        }

        private void BeginZenInput(VaultMoveMoneyRequest.VaultMoneyMoveDirection direction)
        {
            _zenMoveDirection = direction;
            if (!_isZenInputActive)
            {
                _zenInputText = string.Empty;
                _zenInputBlinkTimer = 0;
                _zenInputShowCursor = true;
            }

            _isZenInputActive = true;
        }

        private void CancelZenInput()
        {
            _isZenInputActive = false;
            _zenInputText = string.Empty;
            _zenInputBlinkTimer = 0;
            _zenInputShowCursor = false;
        }

        private void HandleZenInputKeyboard()
        {
            if (!_isZenInputActive || !Visible)
            {
                return;
            }

            if (_currentGameTime != null)
            {
                _zenInputBlinkTimer += _currentGameTime.ElapsedGameTime.TotalMilliseconds;
                if (_zenInputBlinkTimer >= CursorBlinkIntervalMs)
                {
                    _zenInputShowCursor = !_zenInputShowCursor;
                    _zenInputBlinkTimer = 0;
                }
            }

            var keysPressed = MuGame.Instance.Keyboard.GetPressedKeys();
            foreach (var key in keysPressed)
            {
                if (MuGame.Instance.PrevKeyboard.IsKeyUp(key))
                {
                    if (key == Keys.Back)
                    {
                        if (_zenInputText.Length > 0)
                        {
                            _zenInputText = _zenInputText[..^1];
                        }
                    }
                    else if (key == Keys.Enter)
                    {
                        Scene?.ConsumeKeyboardEnter();
                        CommitZenMove();
                        return;
                    }
                    else if (key == Keys.Escape)
                    {
                        Scene?.ConsumeKeyboardEscape();
                        CancelZenInput();
                        return;
                    }
                    else if (TryGetDigitKey(key, out char digit))
                    {
                        if (_zenInputText.Length < ZenInputMaxDigits)
                        {
                            _zenInputText += digit;
                        }
                    }
                }
            }
        }

        private static bool TryGetDigitKey(Keys key, out char digit)
        {
            if (key >= Keys.D0 && key <= Keys.D9)
            {
                digit = (char)('0' + (key - Keys.D0));
                return true;
            }

            if (key >= Keys.NumPad0 && key <= Keys.NumPad9)
            {
                digit = (char)('0' + (key - Keys.NumPad0));
                return true;
            }

            digit = '\0';
            return false;
        }

        private void CommitZenMove()
        {
            if (_networkManager == null || _characterState == null)
            {
                CancelZenInput();
                return;
            }

            uint amount = 0;
            if (!string.IsNullOrWhiteSpace(_zenInputText))
            {
                _ = uint.TryParse(_zenInputText, out amount);
            }

            if (amount == 0)
            {
                CancelZenInput();
                return;
            }

            uint inventoryZen = _characterState.InventoryZen;
            uint vaultZen = _vaultZen <= 0 ? 0 : (uint)Math.Min(_vaultZen, uint.MaxValue);

            if (_zenMoveDirection == VaultMoveMoneyRequest.VaultMoneyMoveDirection.InventoryToVault)
            {
                if (amount > inventoryZen)
                {
                    RequestDialog.ShowInfo("Not enough Zen in inventory.");
                    return;
                }
            }
            else
            {
                if (amount > vaultZen)
                {
                    RequestDialog.ShowInfo("Not enough Zen in vault.");
                    return;
                }

                if (inventoryZen >= MaxZen || (ulong)inventoryZen + amount > MaxZen)
                {
                    RequestDialog.ShowInfo("Inventory Zen limit reached.");
                    return;
                }
            }

            var svc = _networkManager.GetCharacterService();
            if (svc != null)
            {
                _ = svc.SendVaultMoveMoneyAsync(_zenMoveDirection, amount);
            }

            CancelZenInput();
        }

        private void WarmupTexturesSync()
        {
            if (GraphicsManager.Instance?.Sprite == null)
                return;

            foreach (var item in _items)
            {
                // MESMO tamanho que o DrawVaultItems pede (dims efetivas - inset de 2px),
                // senão o warmup povoa uma chave de cache que nunca é usada.
                int rowOnPage = item.GridPosition.Y - PageFirstRow;
                if (rowOnPage < 0 || rowOnPage >= VisibleRows) continue;
                var (effW, effH) = EffectiveVaultDims(item, rowOnPage);
                int w = effW * VAULT_SQUARE_WIDTH - ICON_INSET * 2;
                int h = effH * VAULT_SQUARE_HEIGHT - ICON_INSET * 2;
                _ = ResolveItemTexture(item, w, h, animated: false);
            }
        }

        private void PlaceItemOnGrid(InventoryItem item, Point slot)
        {
            item.GridPosition = slot;
            for (int y = 0; y < item.Definition.Height; y++)
            {
                for (int x = 0; x < item.Definition.Width; x++)
                {
                    int gx = slot.X + x;
                    int gy = slot.Y + y;
                    if (gx >= 0 && gx < Columns && gy >= 0 && gy < Rows)
                    {
                        _itemGrid[gx, gy] = item;
                    }
                }
            }
        }

        private void RemoveItemFromGrid(InventoryItem item)
        {
            for (int y = 0; y < Rows; y++)
            {
                for (int x = 0; x < Columns; x++)
                {
                    if (_itemGrid[x, y] == item)
                    {
                        _itemGrid[x, y] = null;
                    }
                }
            }
        }

        private void ClearGrid() => Array.Clear(_itemGrid, 0, _itemGrid.Length);

        private Color? GetSlotHighlightColor(Point slot, InventoryItem draggedItem)
        {
            if (draggedItem == null || _hoveredSlot.X == -1 || _hoveredSlot.Y == -1)
            {
                return null;
            }

            if (!IsSlotInDropArea(slot, _hoveredSlot, draggedItem))
            {
                return null;
            }

            return CanPlaceAt(_hoveredSlot, draggedItem)
                ? Color.GreenYellow * 0.5f
                : Color.Red * 0.6f;
        }

        private static bool IsSlotInDropArea(Point slot, Point dropPosition, InventoryItem item)
        {
            return slot.X >= dropPosition.X &&
                   slot.X < dropPosition.X + item.Definition.Width &&
                   slot.Y >= dropPosition.Y &&
                   slot.Y < dropPosition.Y + item.Definition.Height;
        }

        private Texture2D ResolveItemTexture(InventoryItem item, int width, int height, bool animated, bool allowGenerate = true)
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
            if (!allowGenerate)
            {
                var rendererCached = BmdPreviewRenderer.TryGetCachedPreview(item, width, height);
                if (rendererCached != null)
                    return rendererCached;
                return null;
            }

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
                    var animatedTexture = BmdPreviewRenderer.GetAnimatedPreview(item, width, height, _currentGameTime);
                    if (animatedTexture != null)
                    {
                        return animatedTexture;
                    }
                }
                catch { return null; }
            }

            var cacheKey = (item, width, height, false);
            if (_bmdPreviewCache.TryGetValue(cacheKey, out var cachedPreview) && cachedPreview != null)
                return cachedPreview;

            // Use cached static preview
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

    }
}
