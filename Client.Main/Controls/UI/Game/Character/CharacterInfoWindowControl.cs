using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Core.Client;
using Client.Main.Core.Utilities;
using Client.Main.Models;
using Client.Main.Networking;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MUnique.OpenMU.Network.Packets;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Client.Main.Helpers;
using Layout = Client.Main.Controls.UI.Game.Hud.CharacterLayout;

namespace Client.Main.Controls.UI.Game.Character
{
    /// <summary>
    /// Tela de personagem (tecla C) — estilo MU oficial mobile. Painel-template + abas +
    /// 3 cards (info / pontos+frutas / stats). Desenho AO VIVO (padrão das telas Imprint:
    /// texto rasterizado direto na resolução final, sem superfície cacheada). Valores
    /// 100% reais do CharacterState/fórmulas canônicas; linha sem fonte de dados = "-".
    /// Layout dirigido por CharacterLayout (editor hud-edit-character :5189).
    /// </summary>
    public class CharacterInfoWindowControl : UIControl, IUiTexturePreloadable
    {
        private static readonly string[] s_preloadTextures =
        {
            "Interface/Imprint/imprint_panel.OZP",
            "Interface/Imprint/imprint_tab.OZP",
            "Interface/Imprint/imprint_close.OZP",
            "Interface/Imprint/imprint_close_hover.OZP",
            "Interface/Inventory/inv_rect.OZP",
        };

        // ── Cores (referência oficial) ──
        private static readonly Color LabelWhite = new(225, 222, 214);
        private static readonly Color LabelGray = new(178, 174, 166);
        private static readonly Color LabelPurple = new(178, 128, 224);
        private static readonly Color ValueYellow = new(230, 214, 60);
        private static readonly Color SignPlus = new(80, 190, 255);
        private static readonly Color SignMinus = new(140, 135, 125);
        private static readonly Color PctBlue = new(76, 197, 254);
        private static readonly Color TitleGold = new(235, 210, 150);
        // Color.White * f PRÉ-multiplica (AlphaBlend); RGBA cru com alpha baixo viraria branco sólido.
        private static readonly Color HeaderStrip = Color.White * 0.08f;

        private enum RowKind { Header, Sub, SubPurple }

        private sealed class StatRow
        {
            public RowKind Kind;
            public string Label = string.Empty;
            public string Value = string.Empty;
            public bool ShowSign;       // coluna do sinal (+ quando há pontos; - caso contrário)
            public int StatIndex = -1;  // 0=STR 1=AGI 2=VIT 3=ENE 4=CMD (clicável no +)
        }

        private Texture2D _texPanel, _texTab, _texRect, _texClose, _texCloseHover, _texDarkCard;

        private readonly List<StatRow> _rows = new();
        private CharacterState _characterState;
        private NetworkManager _networkManager;
        private readonly ILogger<CharacterInfoWindowControl> _logger;

        private CharacterInfoSnapshot _cachedSnapshot;
        private bool _hasCachedSnapshot;
        private bool _closeHovered;
        private int _hoveredSignRow = -1;

        // Arraste da janela pela barra de título.
        private bool _draggingWin;
        private Point _dragGrab;
        private Point _dragBase;

        // Prompt "quantos pontos?" (abre no toque do +): digita a quantidade e confirma.
        private static readonly string[] StatNames = { "Strength", "Agility", "Stamina", "Energy", "Command" };
        private TextFieldControl _amountInput;
        private bool _promptOpen;
        private int _promptStat = -1;
        private bool _allocBusy;
        private Rectangle PromptBoxLocal => new((Layout.PanelW - 270) / 2, 250, 270, 132);

        public CharacterInfoWindowControl()
        {
            _logger = MuGame.AppLoggerFactory.CreateLogger<CharacterInfoWindowControl>();
            ControlSize = new Point(Layout.PanelW, Layout.PanelH);
            ViewSize = ControlSize;
            AutoViewSize = false;
            Interactive = true;
            Visible = false;

            _networkManager = MuGame.Network;
            var characterState = _networkManager?.GetCharacterState();
            if (characterState != null)
            {
                characterState.AttackSpeedsChanged += OnAttackSpeedsChanged;
            }
        }

        public IEnumerable<string> GetPreloadTexturePaths() => s_preloadTextures;

        public override async Task Load()
        {
            await base.Load();
            var tl = TextureLoader.Instance;
            async Task<Texture2D> L(string p) { try { return await tl.PrepareAndGetTexture(p); } catch { return null; } }
            _texPanel = await L("Interface/Imprint/imprint_panel.OZP");
            _texTab = await L("Interface/Imprint/imprint_tab.OZP");
            _texRect = await L("Interface/Inventory/inv_rect.OZP");
            _texClose = await L("Interface/Imprint/imprint_close.OZP");
            _texCloseHover = await L("Interface/Imprint/imprint_close_hover.OZP");
            _texDarkCard = await L("Interface/Imprint/imprint_dark_card.OZP");

            // Campo numérico do prompt de pontos (mesmo TextField do login — teclado Android).
            _amountInput = TextFieldControl.Create();
            _amountInput.ViewSize = new Point(120, 24);
            _amountInput.ControlSize = _amountInput.ViewSize;
            _amountInput.FontSize = 14f;
            _amountInput.Visible = false;
            _amountInput.Sanitizer = t =>
            {
                var sbNum = new System.Text.StringBuilder();
                foreach (var c in t ?? string.Empty)
                {
                    if (char.IsDigit(c) && sbNum.Length < 4) sbNum.Append(c);
                }
                return sbNum.ToString();
            };
            _amountInput.EnterKeyPressed += (_, __) => ConfirmPrompt();
            Controls.Add(_amountInput);
        }

        private static bool Tex(Texture2D t) => t != null && !t.IsDisposed;

        private Rectangle LR(float x, float y, float w, float h)
            => new(DisplayRectangle.X + (int)x, DisplayRectangle.Y + (int)y, (int)w, (int)h);

        // ═══════════════════════════ Dados (100% reais) ═══════════════════════════

        private void OnAttackSpeedsChanged() => _hasCachedSnapshot = false;

        private void UpdateDisplayData()
        {
            _characterState ??= _networkManager?.GetCharacterState();
            if (_characterState == null) return;

            var snapshot = CharacterInfoSnapshot.Create(_characterState);
            if (_hasCachedSnapshot && snapshot.Equals(_cachedSnapshot)) return;

            BuildRows(snapshot);
            _cachedSnapshot = snapshot;
            _hasCachedSnapshot = true;
        }

        private static string StatText(ushort baseValue, ushort added)
            => added > 0 ? $"{baseValue}+{added}" : baseValue.ToString();

        private void BuildRows(CharacterInfoSnapshot s)
        {
            _rows.Clear();

            bool physical = s.HasPhysicalDamage;
            bool magical = s.HasMagicalDamage;

            _rows.Add(new StatRow { Kind = RowKind.Header, Label = "Strength", Value = StatText(s.BaseStrength, s.AddedStrength), ShowSign = true, StatIndex = 0 });
            _rows.Add(new StatRow { Kind = RowKind.SubPurple, Label = "* (S) ATK Power", Value = "-" });
            _rows.Add(new StatRow { Kind = RowKind.Sub, Label = "* ATK Power", Value = physical ? $"{s.PhysicalMin} ~ {s.PhysicalMax}" : "-" });
            _rows.Add(new StatRow { Kind = RowKind.Sub, Label = "* ATK Rate", Value = s.PvMAttackRate.ToString(), ShowSign = true });
            _rows.Add(new StatRow { Kind = RowKind.Sub, Label = "* PvP ATK Rate", Value = s.PvPAttackRate.ToString(), ShowSign = true });
            _rows.Add(new StatRow { Kind = RowKind.Sub, Label = "* Combat Power", Value = "-" });

            _rows.Add(new StatRow { Kind = RowKind.Header, Label = "Agility", Value = StatText(s.BaseAgility, s.AddedAgility), ShowSign = true, StatIndex = 1 });
            _rows.Add(new StatRow { Kind = RowKind.SubPurple, Label = "* (S) DEF", Value = "-" });
            _rows.Add(new StatRow { Kind = RowKind.Sub, Label = "* DEF", Value = s.Defense.ToString() });
            _rows.Add(new StatRow { Kind = RowKind.Sub, Label = "* ATK Speed", Value = s.AttackSpeed.ToString() });
            _rows.Add(new StatRow { Kind = RowKind.Sub, Label = "* DEF Rate", Value = s.PvMDefenseRate.ToString(), ShowSign = true });
            _rows.Add(new StatRow { Kind = RowKind.Sub, Label = "* PvP DEF Rate", Value = s.PvPDefenseRate.ToString(), ShowSign = true });

            _rows.Add(new StatRow { Kind = RowKind.Header, Label = "Stamina", Value = StatText(s.BaseVitality, s.AddedVitality), ShowSign = true, StatIndex = 2 });
            _rows.Add(new StatRow { Kind = RowKind.Header, Label = "Energy", Value = StatText(s.BaseEnergy, s.AddedEnergy), ShowSign = true, StatIndex = 3 });

            if (s.IsDarkLordFamily)
            {
                _rows.Add(new StatRow { Kind = RowKind.Header, Label = "Command", Value = StatText(s.BaseLeadership, s.AddedLeadership), ShowSign = true, StatIndex = 4 });
            }

            _rows.Add(new StatRow { Kind = RowKind.Sub, Label = "* Skill ATK", Value = magical ? $"{s.MagicalMin} ~ {s.MagicalMax}" : "-" });
        }

        // ═══════════════════════════ Input ═══════════════════════════

        public override void Update(GameTime gameTime)
        {
            if (!Visible) return;
            base.Update(gameTime);
            UpdateDisplayData();

            var mouse = MuGame.Instance.UiMouseState;
            var prev = MuGame.Instance.PrevUiMouseState;
            var p = mouse.Position;
            bool down = mouse.LeftButton == ButtonState.Pressed;
            bool click = down && prev.LeftButton == ButtonState.Released;

            _closeHovered = Layout.CloseEnabled && LR(Layout.CloseX, Layout.CloseY, Layout.CloseW, Layout.CloseH).Contains(p);

            // ── Prompt de pontos aberto: só ele recebe input ──
            if (_promptOpen)
            {
                if (click)
                {
                    var box = LR(PromptBoxLocal.X, PromptBoxLocal.Y, PromptBoxLocal.Width, PromptBoxLocal.Height);
                    if (GetPromptOkRect().Contains(p)) ConfirmPrompt();
                    else if (GetPromptCancelRect().Contains(p)) ClosePrompt();
                    else if (!box.Contains(p)) ClosePrompt();   // clique fora fecha
                    Scene?.SetMouseInputConsumed();
                }
                return;
            }

            // ── Arraste pela barra de título (fora do X) ──
            if (_draggingWin)
            {
                if (down)
                {
                    X = _dragBase.X + (p.X - _dragGrab.X);
                    Y = _dragBase.Y + (p.Y - _dragGrab.Y);
                    Scene?.SetMouseInputConsumed();
                    return;
                }
                _draggingWin = false;
            }
            if (click && !_closeHovered && LR(0, 0, Layout.PanelW, Layout.DragBarH).Contains(p))
            {
                _draggingWin = true;
                _dragGrab = p;
                _dragBase = new Point(X, Y);
                Scene?.SetMouseInputConsumed();
                return;
            }

            // Hover/click no sinal "+" das linhas de atributo (só com pontos disponíveis).
            _hoveredSignRow = -1;
            bool hasPoints = _cachedSnapshot.LevelUpPoints > 0;
            for (int i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                if (row.StatIndex < 0 || !row.ShowSign) continue;
                var signRect = GetSignRect(i);
                if (signRect.Contains(p))
                {
                    _hoveredSignRow = i;
                    if (click && hasPoints && !_allocBusy)
                    {
                        OpenPrompt(row.StatIndex);   // digita a quantidade em vez de 1-a-1
                        Scene?.SetMouseInputConsumed();
                        return;
                    }
                }
            }

            if (click && _closeHovered)
            {
                HideWindow();
                SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav");
                Scene?.SetMouseInputConsumed();
                return;
            }

            if (click && DisplayRectangle.Contains(p))
            {
                Scene?.SetMouseInputConsumed();   // não vaza clique pro mundo
            }
        }

        private Rectangle GetSignRect(int rowIndex)
        {
            float y = Layout.StatRowY + rowIndex * Layout.StatRowPitch;
            return LR(Layout.StatSignX - 11, y - 3, 22, 20);
        }

        // ── Prompt de alocação em lote ──────────────────────────────────────────────

        private Rectangle GetPromptOkRect()
            => LR(PromptBoxLocal.X + 24, PromptBoxLocal.Y + 92, 100, 28);

        private Rectangle GetPromptCancelRect()
            => LR(PromptBoxLocal.X + PromptBoxLocal.Width - 124, PromptBoxLocal.Y + 92, 100, 28);

        private void OpenPrompt(int statIndex)
        {
            SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav");
            _promptOpen = true;
            _promptStat = statIndex;
            _amountInput.X = PromptBoxLocal.X + (PromptBoxLocal.Width - _amountInput.ViewSize.X) / 2;
            _amountInput.Y = PromptBoxLocal.Y + 52;
            _amountInput.Value = "1";
            _amountInput.Visible = true;
            _amountInput.Focus();
        }

        private void ClosePrompt()
        {
            _promptOpen = false;
            _promptStat = -1;
            _amountInput.Visible = false;
            _amountInput.Blur();
        }

        private void ConfirmPrompt()
        {
            if (!_promptOpen || _promptStat < 0) return;
            int available = _cachedSnapshot.LevelUpPoints;
            if (!int.TryParse(_amountInput.Value, out int amount) || amount < 1)
            {
                ClosePrompt();
                return;
            }
            amount = Math.Min(amount, available);
            int stat = _promptStat;
            ClosePrompt();
            if (amount <= 0) return;

            var service = _networkManager?.GetCharacterService();
            if (service == null)
            {
                MessageWindow.Show("Internal error: Could not add points.");
                return;
            }

            CharacterStatAttribute attribute = stat switch
            {
                0 => CharacterStatAttribute.Strength,
                1 => CharacterStatAttribute.Agility,
                2 => CharacterStatAttribute.Vitality,
                3 => CharacterStatAttribute.Energy,
                4 => CharacterStatAttribute.Leadership,
                _ => CharacterStatAttribute.Strength
            };

            // O pacote adiciona 1 ponto por request: dispara N em sequência (com folga
            // pro servidor processar). UI atualiza sozinha pelos eventos de rede.
            _allocBusy = true;
            SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav");
            _ = Task.Run(async () =>
            {
                try
                {
                    for (int i = 0; i < amount; i++)
                    {
                        await service.SendIncreaseCharacterStatPointRequestAsync(attribute);
                        await Task.Delay(25);
                    }
                    _logger.LogInformation("Sent {Amount} stat points to {Attribute}.", amount, attribute);
                }
                finally
                {
                    _allocBusy = false;
                }
            });
        }

        private void OnStatButtonClicked(int statIndex)
        {
            SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav");

            if (_networkManager == null || !_networkManager.IsConnected || _networkManager.CurrentState < ClientConnectionState.InGame)
            {
                _logger.LogWarning("Cannot increase stat: Not connected to game server or invalid state.");
                MessageWindow.Show("Cannot add stat points: Not connected to server or not in game.");
                return;
            }

            CharacterStatAttribute attributeToSend = statIndex switch
            {
                0 => CharacterStatAttribute.Strength,
                1 => CharacterStatAttribute.Agility,
                2 => CharacterStatAttribute.Vitality,
                3 => CharacterStatAttribute.Energy,
                4 => CharacterStatAttribute.Leadership,
                _ => CharacterStatAttribute.Strength
            };

            if (_characterState == null) return;

            if (attributeToSend == CharacterStatAttribute.Leadership &&
                !(_characterState.Class == CharacterClassNumber.DarkLord || _characterState.Class == CharacterClassNumber.LordEmperor))
            {
                MessageWindow.Show("Only Dark Lords can add Leadership points.");
                return;
            }

            var service = _networkManager.GetCharacterService();
            if (service != null)
            {
                _ = service.SendIncreaseCharacterStatPointRequestAsync(attributeToSend);
                _logger.LogInformation("Sent request to add point to {Attribute}.", attributeToSend);
            }
            else
            {
                MessageWindow.Show("Internal error: Could not add points.");
            }
        }

        // ═══════════════════════════ Draw (ao vivo) ═══════════════════════════

        public override void Draw(GameTime gameTime)
        {
            if (!Visible) return;
            var font = GraphicsManager.Instance.Font;
            var sb = GraphicsManager.Instance.Sprite;
            var pixel = GraphicsManager.Instance.Pixel;
            if (font == null || sb == null || pixel == null) return;

            SpriteBatchScope? scope = null;
            if (!SpriteBatchScope.BatchIsBegun)
            {
                scope = new SpriteBatchScope(sb, SpriteSortMode.Deferred, BlendState.AlphaBlend,
                    GraphicsManager.GetQualityLinearSamplerState(), transform: UiScaler.SpriteTransform);
            }

            try
            {
                // Painel-template (barra de título assada).
                var panelRect = LR(0, 0, Layout.PanelW, Layout.PanelH);
                if (Tex(_texPanel)) sb.Draw(_texPanel, panelRect, Color.White);
                else sb.Draw(pixel, panelRect, new Color(24, 20, 18) * 0.97f);

                // Nome do personagem sobre a barra.
                if (Layout.TitleTextEnabled)
                    DrawCentered(sb, font, _cachedSnapshot.Name ?? string.Empty, Layout.TitleTextX, Layout.TitleTextY, Layout.TitleTextFont, TitleGold);

                // X de fechar.
                if (Layout.CloseEnabled)
                {
                    var closeTex = _closeHovered ? (_texCloseHover ?? _texClose) : _texClose;
                    if (Tex(closeTex)) sb.Draw(closeTex, LR(Layout.CloseX, Layout.CloseY, Layout.CloseW, Layout.CloseH), Color.White);
                }

                // Abas: Normal (ativa) | Element + 3rd/4th/5th (desabilitadas — sem conteúdo).
                if (Layout.TabsEnabled)
                {
                    DrawTab(sb, font, LR(Layout.TabNormalX, Layout.TabNormalY, Layout.TabNormalW, Layout.TabNormalH), "Normal", Layout.TabFont, active: true);
                    DrawTab(sb, font, LR(Layout.TabNormalX + Layout.TabPitch, Layout.TabNormalY, Layout.TabNormalW, Layout.TabNormalH), "Element", Layout.TabFont, active: false);
                    string[] small = { "3rd", "4th", "5th" };
                    for (int i = 0; i < small.Length; i++)
                    {
                        DrawTab(sb, font, LR(Layout.TabSmallX + i * Layout.TabSmallPitch, Layout.TabSmallY, Layout.TabSmallW, Layout.TabSmallH), small[i], Layout.TabSmallFont, active: false);
                    }
                }

                // Dark card POR TRÁS de cada container (referência oficial) + molduras.
                if (Tex(_texDarkCard))
                {
                    if (Layout.Card1Enabled) sb.Draw(_texDarkCard, LR(Layout.Card1X, Layout.Card1Y, Layout.Card1W, Layout.Card1H), Color.White);
                    if (Layout.Card2Enabled) sb.Draw(_texDarkCard, LR(Layout.Card2X, Layout.Card2Y, Layout.Card2W, Layout.Card2H), Color.White);
                    if (Layout.Card3Enabled) sb.Draw(_texDarkCard, LR(Layout.Card3X, Layout.Card3Y, Layout.Card3W, Layout.Card3H), Color.White);
                }
                if (Layout.Card1Enabled && Tex(_texRect)) Draw9Slice(sb, _texRect, LR(Layout.Card1X, Layout.Card1Y, Layout.Card1W, Layout.Card1H));
                if (Layout.Card2Enabled && Tex(_texRect)) Draw9Slice(sb, _texRect, LR(Layout.Card2X, Layout.Card2Y, Layout.Card2W, Layout.Card2H));
                if (Layout.Card3Enabled && Tex(_texRect)) Draw9Slice(sb, _texRect, LR(Layout.Card3X, Layout.Card3Y, Layout.Card3W, Layout.Card3H));

                DrawInfoCard(sb, font);
                DrawPointsCard(sb, font);
                DrawStatsCard(sb, font, pixel);
                DrawPrompt(sb, font, pixel);
            }
            finally
            {
                scope?.Dispose();
            }

            base.Draw(gameTime);   // filhos (campo de texto do prompt) desenham por cima
        }

        private void DrawInfoCard(SpriteBatch sb, SpriteFont font)
        {
            var s = _cachedSnapshot;
            string levelText = s.MasterLevel > 0 ? $"{s.Level}  ML.{s.MasterLevel}" : s.Level.ToString();

            (string Label, string Value)[] rows =
            {
                ("Class", CharacterClassDatabase.GetClassName(s.Class)),
                ("Level", levelText),
                ("Guild", "-"),
                ("Server", "-"),
            };

            for (int i = 0; i < rows.Length; i++)
            {
                float y = Layout.InfoRowY + i * Layout.InfoRowPitch;
                DrawLeft(sb, font, rows[i].Label, Layout.InfoLabelX, y, Layout.InfoFont, LabelWhite);
                DrawLeft(sb, font, rows[i].Value, Layout.InfoValueX, y, Layout.InfoFont, ValueYellow);
            }
        }

        private void DrawPointsCard(SpriteBatch sb, SpriteFont font)
        {
            var s = _cachedSnapshot;
            string pts = s.MasterLevel > 0
                ? (s.MasterLevelUpPoints > 0 ? s.MasterLevelUpPoints.ToString() : "-")
                : (s.LevelUpPoints > 0 ? s.LevelUpPoints.ToString() : "-");

            float y0 = Layout.PtsRowY;
            DrawLeft(sb, font, "Pts Remaining", Layout.InfoLabelX, y0, Layout.InfoFont, LabelWhite);
            DrawLeft(sb, font, pts, Layout.InfoValueX, y0, Layout.InfoFont, ValueYellow);

            float y1 = y0 + Layout.PtsRowPitch;
            DrawLeft(sb, font, "Fruit Create", Layout.InfoLabelX, y1, Layout.InfoFont, LabelWhite);
            DrawLeft(sb, font, "0 / 0", Layout.InfoValueX, y1, Layout.InfoFont, ValueYellow);
            DrawRight(sb, font, "(100%)", Layout.PctX, y1, Layout.InfoFont, PctBlue);

            float y2 = y0 + Layout.PtsRowPitch * 2;
            DrawLeft(sb, font, "Fruit Decrease", Layout.InfoLabelX, y2, Layout.InfoFont, LabelWhite);
            DrawLeft(sb, font, "0 / 0", Layout.InfoValueX, y2, Layout.InfoFont, ValueYellow);
            DrawRight(sb, font, "(100%)", Layout.PctX, y2, Layout.InfoFont, PctBlue);
        }

        private void DrawStatsCard(SpriteBatch sb, SpriteFont font, Texture2D pixel)
        {
            bool hasPoints = _cachedSnapshot.LevelUpPoints > 0;

            for (int i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                float y = Layout.StatRowY + i * Layout.StatRowPitch;
                bool header = row.Kind == RowKind.Header;
                float f = header ? Layout.StatHeaderFont : Layout.StatSubFont;

                // Faixa de destaque nas linhas de atributo (Strength/Agility/...).
                if (header)
                {
                    sb.Draw(pixel, LR(Layout.Card3X + 4, y - 2, Layout.Card3W - 8, Layout.StatRowPitch), HeaderStrip);
                }

                Color labelColor = row.Kind switch
                {
                    RowKind.Header => LabelWhite,
                    RowKind.SubPurple => LabelPurple,
                    _ => LabelGray
                };

                DrawLeft(sb, font, row.Label, Layout.StatLabelX, y, f, labelColor);
                DrawLeft(sb, font, row.Value, Layout.StatValueX, y, f, ValueYellow);

                // Coluna do sinal: "+" azul (clicável) quando há pontos E é linha de
                // atributo; senão "-" cinza (como no oficial).
                if (row.ShowSign)
                {
                    bool plus = hasPoints && row.StatIndex >= 0;
                    string sign = plus ? "+" : "-";
                    Color signColor = plus
                        ? (_hoveredSignRow == i ? Color.White : SignPlus)
                        : SignMinus;
                    DrawCentered(sb, font, sign, Layout.StatSignX, y + Layout.StatRowPitch * 0.4f, f + (plus ? 2f : 0f), signColor);
                }
            }
        }

        // Prompt "quantos pontos?": véu sobre a janela + caixa (dark card + moldura) com
        // título, campo (filho TextField) e botões OK/Cancel.
        private void DrawPrompt(SpriteBatch sb, SpriteFont font, Texture2D pixel)
        {
            if (!_promptOpen) return;

            sb.Draw(pixel, LR(0, 0, Layout.PanelW, Layout.PanelH), Color.Black * 0.55f);

            var box = LR(PromptBoxLocal.X, PromptBoxLocal.Y, PromptBoxLocal.Width, PromptBoxLocal.Height);
            if (Tex(_texDarkCard)) sb.Draw(_texDarkCard, box, Color.White);
            else sb.Draw(pixel, box, new Color(18, 16, 14) * 0.98f);
            if (Tex(_texRect)) Draw9Slice(sb, _texRect, box);

            string statName = _promptStat >= 0 && _promptStat < StatNames.Length ? StatNames[_promptStat] : string.Empty;
            int available = _cachedSnapshot.LevelUpPoints;
            DrawCentered(sb, font, $"{statName} — pontos (máx {available})",
                PromptBoxLocal.X + PromptBoxLocal.Width / 2f, PromptBoxLocal.Y + 26, 12f, TitleGold);

            // moldura simples do campo (o TextField filho desenha o texto)
            var fieldRect = LR(_amountInput.X - 4, _amountInput.Y - 4, _amountInput.ViewSize.X + 8, _amountInput.ViewSize.Y + 8);
            sb.Draw(pixel, fieldRect, Color.Black * 0.6f);
            sb.Draw(pixel, new Rectangle(fieldRect.X, fieldRect.Y, fieldRect.Width, 1), new Color(120, 104, 72));
            sb.Draw(pixel, new Rectangle(fieldRect.X, fieldRect.Bottom - 1, fieldRect.Width, 1), new Color(120, 104, 72));
            sb.Draw(pixel, new Rectangle(fieldRect.X, fieldRect.Y, 1, fieldRect.Height), new Color(120, 104, 72));
            sb.Draw(pixel, new Rectangle(fieldRect.Right - 1, fieldRect.Y, 1, fieldRect.Height), new Color(120, 104, 72));

            DrawPromptButton(sb, font, GetPromptOkRect(), "OK");
            DrawPromptButton(sb, font, GetPromptCancelRect(), "Cancel");
        }

        private void DrawPromptButton(SpriteBatch sb, SpriteFont font, Rectangle rect, string label)
        {
            var pixel = GraphicsManager.Instance.Pixel;
            bool hovered = rect.Contains(MuGame.Instance.UiMouseState.Position);
            sb.Draw(pixel, rect, (hovered ? new Color(66, 58, 40) : new Color(44, 38, 28)) * 0.95f);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), new Color(150, 128, 84));
            sb.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), new Color(60, 50, 34));
            float cx = rect.X - DisplayRectangle.X + rect.Width / 2f;
            float cy = rect.Y - DisplayRectangle.Y + rect.Height / 2f;
            DrawCentered(sb, font, label, cx, cy, 12f, hovered ? Color.White : new Color(225, 218, 200));
        }

        private void DrawTab(SpriteBatch sb, SpriteFont font, Rectangle rect, string label, float fontSize, bool active)
        {
            if (Tex(_texTab)) sb.Draw(_texTab, rect, active ? Color.White : Color.White * 0.45f);
            if (active)
            {
                // destaque dourado da aba ativa (mesmo efeito das telas Imprint)
                var pixel = GraphicsManager.Instance.Pixel;
                sb.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 2), new Color(212, 175, 85));
                sb.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2), new Color(212, 175, 85) * 0.6f);
            }
            Color textColor = active ? new Color(235, 210, 150) : new Color(150, 140, 120);
            float cx = rect.X - DisplayRectangle.X + rect.Width / 2f;
            float cy = rect.Y - DisplayRectangle.Y + rect.Height / 2f;
            DrawCentered(sb, font, label, cx, cy, fontSize, textColor);
        }

        // ── primitivas de texto (coords locais do painel) ──
        private void DrawLeft(SpriteBatch sb, SpriteFont font, string text, float x, float y, float fontSize, Color color)
        {
            if (string.IsNullOrEmpty(text)) return;
            float s = fontSize / 25f;
            var pos = new Vector2(DisplayRectangle.X + x, DisplayRectangle.Y + y);
            sb.DrawString(font, text, pos + Vector2.One, Color.Black * 0.6f, 0f, Vector2.Zero, s, SpriteEffects.None, 0f);
            sb.DrawString(font, text, pos, color * Alpha, 0f, Vector2.Zero, s, SpriteEffects.None, 0f);
        }

        private void DrawRight(SpriteBatch sb, SpriteFont font, string text, float rightX, float y, float fontSize, Color color)
        {
            if (string.IsNullOrEmpty(text)) return;
            float s = fontSize / 25f;
            Vector2 size = font.MeasureString(text) * s;
            var pos = new Vector2(DisplayRectangle.X + rightX - size.X, DisplayRectangle.Y + y);
            sb.DrawString(font, text, pos + Vector2.One, Color.Black * 0.6f, 0f, Vector2.Zero, s, SpriteEffects.None, 0f);
            sb.DrawString(font, text, pos, color * Alpha, 0f, Vector2.Zero, s, SpriteEffects.None, 0f);
        }

        private void DrawCentered(SpriteBatch sb, SpriteFont font, string text, float cx, float cy, float fontSize, Color color)
        {
            if (string.IsNullOrEmpty(text)) return;
            float s = fontSize / 25f;
            Vector2 size = font.MeasureString(text) * s;
            var pos = new Vector2(DisplayRectangle.X + cx - size.X / 2f, DisplayRectangle.Y + cy - size.Y / 2f);
            sb.DrawString(font, text, pos + Vector2.One, Color.Black * 0.6f, 0f, Vector2.Zero, s, SpriteEffects.None, 0f);
            sb.DrawString(font, text, pos, color * Alpha, 0f, Vector2.Zero, s, SpriteEffects.None, 0f);
        }

        // 9-slice da moldura inv_rect (borda 8px) — cantos fixos, bordas esticam.
        private static void Draw9Slice(SpriteBatch sb, Texture2D tex, Rectangle dst)
        {
            const int m = 8;
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

        // ═══════════════════════════ API pública (inalterada) ═══════════════════════════

        public void ShowWindow()
        {
            Visible = true;
            _hasCachedSnapshot = false;
            UpdateDisplayData();
            BringToFront();
            SoundController.Instance.PlayBuffer("Sound/iCreateWindow.wav");
            if (Scene != null) Scene.FocusControl = this;
        }

        public void HideWindow()
        {
            Visible = false;
            if (Scene != null && Scene.FocusControl == this) Scene.FocusControl = null;
        }

        public override void Dispose()
        {
            var characterState = _networkManager?.GetCharacterState();
            if (characterState != null)
            {
                characterState.AttackSpeedsChanged -= OnAttackSpeedsChanged;
            }
            base.Dispose();
        }

        // ═══════════════ Snapshot + fórmulas CANÔNICAS (inalteradas) ═══════════════

        private readonly struct CharacterInfoSnapshot : IEquatable<CharacterInfoSnapshot>
        {
            public CharacterInfoSnapshot(CharacterState state)
            {
                Name = state.Name ?? string.Empty;
                Class = state.Class;
                Level = state.Level;
                MasterLevel = state.MasterLevel;
                Experience = state.Experience;
                ExperienceForNextLevel = state.ExperienceForNextLevel;
                LevelUpPoints = state.LevelUpPoints;
                MasterLevelUpPoints = state.MasterLevelUpPoints;
                BaseStrength = state.Strength;
                AddedStrength = state.AddedStrength;
                BaseAgility = state.Agility;
                AddedAgility = state.AddedAgility;
                BaseVitality = state.Vitality;
                AddedVitality = state.AddedVitality;
                BaseEnergy = state.Energy;
                AddedEnergy = state.AddedEnergy;
                BaseLeadership = state.Leadership;
                AddedLeadership = state.AddedLeadership;
                CurrentHealth = state.CurrentHealth;
                MaxHealth = state.MaximumHealth;
                CurrentShield = state.CurrentShield;
                MaxShield = state.MaximumShield;
                CurrentMana = state.CurrentMana;
                MaxMana = state.MaximumMana;
                CurrentAbility = state.CurrentAbility;
                MaxAbility = state.MaximumAbility;

                var phys = GetPhysicalDamage(state);
                PhysicalMin = phys.min;
                PhysicalMax = phys.max;

                var magic = GetMagicalDamage(state);
                MagicalMin = magic.min;
                MagicalMax = magic.max;

                AttackSpeed = state.AttackSpeed;
                Defense = GetDefense(state);
                PvPAttackRate = GetPvPAttackRate(state);
                PvPDefenseRate = GetPvPDefenseRate(state);
                PvMAttackRate = GetPvMAttackRate(state);
                PvMDefenseRate = GetPvMDefenseRate(state);

                IsDarkLordFamily = state.Class == CharacterClassNumber.DarkLord || state.Class == CharacterClassNumber.LordEmperor;
                CanBeMaster = state.Class != CharacterClassNumber.DarkWizard;
            }

            public static CharacterInfoSnapshot Create(CharacterState state) => new(state);

            public readonly string Name;
            public readonly CharacterClassNumber Class;
            public readonly ushort Level;
            public readonly ushort MasterLevel;
            public readonly ulong Experience;
            public readonly ulong ExperienceForNextLevel;
            public readonly ushort LevelUpPoints;
            public readonly ushort MasterLevelUpPoints;
            public readonly ushort BaseStrength;
            public readonly ushort AddedStrength;
            public readonly ushort BaseAgility;
            public readonly ushort AddedAgility;
            public readonly ushort BaseVitality;
            public readonly ushort AddedVitality;
            public readonly ushort BaseEnergy;
            public readonly ushort AddedEnergy;
            public readonly ushort BaseLeadership;
            public readonly ushort AddedLeadership;
            public readonly uint CurrentHealth;
            public readonly uint MaxHealth;
            public readonly uint CurrentShield;
            public readonly uint MaxShield;
            public readonly uint CurrentMana;
            public readonly uint MaxMana;
            public readonly uint CurrentAbility;
            public readonly uint MaxAbility;
            public readonly int PhysicalMin;
            public readonly int PhysicalMax;
            public readonly int MagicalMin;
            public readonly int MagicalMax;
            public readonly int AttackSpeed;
            public readonly int Defense;
            public readonly int PvPAttackRate;
            public readonly int PvPDefenseRate;
            public readonly int PvMAttackRate;
            public readonly int PvMDefenseRate;
            public readonly bool IsDarkLordFamily;
            public readonly bool CanBeMaster;

            public bool HasPhysicalDamage => PhysicalMin > 0 || PhysicalMax > 0;
            public bool HasMagicalDamage => MagicalMin > 0 || MagicalMax > 0;

            public bool Equals(CharacterInfoSnapshot other)
            {
                return string.Equals(Name, other.Name, StringComparison.Ordinal) &&
                       Class == other.Class &&
                       Level == other.Level &&
                       MasterLevel == other.MasterLevel &&
                       Experience == other.Experience &&
                       ExperienceForNextLevel == other.ExperienceForNextLevel &&
                       LevelUpPoints == other.LevelUpPoints &&
                       MasterLevelUpPoints == other.MasterLevelUpPoints &&
                       BaseStrength == other.BaseStrength &&
                       AddedStrength == other.AddedStrength &&
                       BaseAgility == other.BaseAgility &&
                       AddedAgility == other.AddedAgility &&
                       BaseVitality == other.BaseVitality &&
                       AddedVitality == other.AddedVitality &&
                       BaseEnergy == other.BaseEnergy &&
                       AddedEnergy == other.AddedEnergy &&
                       BaseLeadership == other.BaseLeadership &&
                       AddedLeadership == other.AddedLeadership &&
                       CurrentHealth == other.CurrentHealth &&
                       MaxHealth == other.MaxHealth &&
                       CurrentShield == other.CurrentShield &&
                       MaxShield == other.MaxShield &&
                       CurrentMana == other.CurrentMana &&
                       MaxMana == other.MaxMana &&
                       CurrentAbility == other.CurrentAbility &&
                       MaxAbility == other.MaxAbility &&
                       PhysicalMin == other.PhysicalMin &&
                       PhysicalMax == other.PhysicalMax &&
                       MagicalMin == other.MagicalMin &&
                       MagicalMax == other.MagicalMax &&
                       AttackSpeed == other.AttackSpeed &&
                       Defense == other.Defense &&
                       PvPAttackRate == other.PvPAttackRate &&
                       PvPDefenseRate == other.PvPDefenseRate &&
                       PvMAttackRate == other.PvMAttackRate &&
                       PvMDefenseRate == other.PvMDefenseRate &&
                       IsDarkLordFamily == other.IsDarkLordFamily &&
                       CanBeMaster == other.CanBeMaster;
            }
        }

        private static (int min, int max) GetPhysicalDamage(CharacterState state)
        {
            if (state == null) return (0, 0);

            var str = state.TotalStrength;
            var agi = state.TotalAgility;
            var ene = state.TotalEnergy;

            return state.Class switch
            {
                CharacterClassNumber.DarkKnight or CharacterClassNumber.BladeKnight or CharacterClassNumber.BladeMaster =>
                    (str / 6, str / 4),
                CharacterClassNumber.FairyElf or CharacterClassNumber.MuseElf or CharacterClassNumber.HighElf =>
                    ((str + agi * 2) / 14, (str + agi * 2) / 8),
                CharacterClassNumber.MagicGladiator or CharacterClassNumber.DuelMaster =>
                    ((str * 2 + ene) / 12, (str * 2 + ene) / 8),
                CharacterClassNumber.DarkLord or CharacterClassNumber.LordEmperor =>
                    ((str * 2 + ene) / 14, (str * 2 + ene) / 10),
                CharacterClassNumber.RageFighter or CharacterClassNumber.FistMaster =>
                    (str / 6 + state.TotalVitality / 10, str / 4 + state.TotalVitality / 8),
                _ => (0, 0)
            };
        }

        private static (int min, int max) GetMagicalDamage(CharacterState state)
        {
            if (state == null) return (0, 0);

            var ene = state.TotalEnergy;

            return state.Class switch
            {
                CharacterClassNumber.DarkWizard or CharacterClassNumber.SoulMaster or CharacterClassNumber.GrandMaster =>
                    (ene / 9, ene / 4),
                CharacterClassNumber.MagicGladiator or CharacterClassNumber.DuelMaster =>
                    (ene / 9, ene / 4),
                CharacterClassNumber.Summoner or CharacterClassNumber.BloodySummoner or CharacterClassNumber.DimensionMaster =>
                    (ene / 5, ene / 2),
                _ => (0, 0)
            };
        }

        private static int GetDefense(CharacterState state)
        {
            if (state == null) return 0;

            var agi = state.TotalAgility;

            return state.Class switch
            {
                CharacterClassNumber.DarkKnight or CharacterClassNumber.BladeKnight or CharacterClassNumber.BladeMaster =>
                    agi / 3,
                CharacterClassNumber.DarkWizard or CharacterClassNumber.SoulMaster or CharacterClassNumber.GrandMaster =>
                    agi / 4,
                CharacterClassNumber.FairyElf or CharacterClassNumber.MuseElf or CharacterClassNumber.HighElf =>
                    agi / 10,
                CharacterClassNumber.MagicGladiator or CharacterClassNumber.DuelMaster =>
                    agi / 5,
                CharacterClassNumber.DarkLord or CharacterClassNumber.LordEmperor =>
                    agi / 7,
                CharacterClassNumber.Summoner or CharacterClassNumber.BloodySummoner or CharacterClassNumber.DimensionMaster =>
                    agi / 4,
                CharacterClassNumber.RageFighter or CharacterClassNumber.FistMaster =>
                    agi / 7,
                _ => 0
            };
        }

        private static int GetPvPAttackRate(CharacterState state)
        {
            if (state == null) return 0;

            var lvl = state.Level;
            var agi = state.TotalAgility;

            return state.Class switch
            {
                CharacterClassNumber.DarkKnight or CharacterClassNumber.BladeKnight or CharacterClassNumber.BladeMaster =>
                    lvl * 3 + (int)(agi * 4.5f),
                CharacterClassNumber.DarkWizard or CharacterClassNumber.SoulMaster or CharacterClassNumber.GrandMaster =>
                    lvl * 3 + agi * 4,
                CharacterClassNumber.FairyElf or CharacterClassNumber.MuseElf or CharacterClassNumber.HighElf =>
                    (int)(lvl * 3 + agi * 0.6f),
                CharacterClassNumber.MagicGladiator or CharacterClassNumber.DuelMaster =>
                    (int)(lvl * 3 + agi * 3.5f),
                CharacterClassNumber.DarkLord or CharacterClassNumber.LordEmperor =>
                    lvl * 3 + agi * 4,
                _ => 0
            };
        }

        private static int GetPvPDefenseRate(CharacterState state)
        {
            if (state == null) return 0;

            var lvl = state.Level;
            var agi = state.TotalAgility;

            return state.Class switch
            {
                CharacterClassNumber.DarkKnight or CharacterClassNumber.BladeKnight or CharacterClassNumber.BladeMaster =>
                    lvl * 2 + agi / 2,
                CharacterClassNumber.DarkWizard or CharacterClassNumber.SoulMaster or CharacterClassNumber.GrandMaster =>
                    lvl * 2 + agi / 4,
                CharacterClassNumber.FairyElf or CharacterClassNumber.MuseElf or CharacterClassNumber.HighElf =>
                    lvl * 2 + agi / 10,
                CharacterClassNumber.MagicGladiator or CharacterClassNumber.DuelMaster =>
                    lvl * 2 + agi / 4,
                CharacterClassNumber.DarkLord or CharacterClassNumber.LordEmperor =>
                    lvl * 2 + agi / 2,
                _ => 0
            };
        }

        private static int GetPvMAttackRate(CharacterState state)
        {
            if (state == null) return 0;

            var lvl = state.Level;
            var agi = state.TotalAgility;
            var str = state.TotalStrength;
            var cmd = state.TotalLeadership;

            return state.Class switch
            {
                CharacterClassNumber.DarkLord or CharacterClassNumber.LordEmperor =>
                    (int)((lvl * 2 + agi) * 2.5f + str / 6 + cmd / 10),
                _ => (int)(lvl * 5 + agi * 1.5f + str / 4)
            };
        }

        private static int GetPvMDefenseRate(CharacterState state)
        {
            if (state == null) return 0;

            var agi = state.TotalAgility;

            return state.Class switch
            {
                CharacterClassNumber.FairyElf or CharacterClassNumber.MuseElf or CharacterClassNumber.HighElf =>
                    agi / 4,
                CharacterClassNumber.DarkLord or CharacterClassNumber.LordEmperor =>
                    agi / 7,
                _ => agi / 3
            };
        }
    }
}
