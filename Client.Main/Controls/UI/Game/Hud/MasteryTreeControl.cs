using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Controls.UI.Game.Skills;
using Client.Main.Core.Client;
using Client.Main.Core.Utilities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Controls.UI.Game.Hud
{
    /// <summary>
    /// Janela de MASTERY (árvore de master skills), estilo tela oficial: janela única
    /// (arte mastery_bg composta dos masterskill_bg oficiais) com 3 colunas — comum /
    /// especialidade I / especialidade II — nós CIRCULARES (mesma pilha do Skill
    /// Imprint: soquete + ícone redondo HD + glow) e conexões de pré-requisito.
    ///
    /// DADOS 100% do servidor: ServerMasterSkills (dump do banco OpenMU) define a
    /// árvore; os níveis aprendidos vêm do CharacterState (MasterSkillList/Update).
    /// Clique num nó envia AddMasterSkillPoint — o SERVIDOR valida (pontos, rank,
    /// pré-requisito nível 10) e responde com o update.
    ///
    /// Abre pela tecla A ou pelo ícone de árvore (Allocate) do fold-out.
    /// Geometria dirigida pelo MasteryLayout (editor hud-edit-mastery :5193).
    /// </summary>
    public sealed class MasteryTreeControl : UIControl
    {
        public static MasteryTreeControl Instance { get; private set; }

        private const string BgPath = "Interface/Mastery/mastery_bg.OZP";
        private const string SlotPath = "Interface/Imprint/imprint_slot.OZP";
        private const string GlowPath = "Interface/SkillWin/hole_glow.OZP";
        private const string ClosePath = "Interface/Imprint/imprint_close.OZP";
        private const string CloseHoverPath = "Interface/Imprint/imprint_close_hover.OZP";

        private readonly CharacterState _state;

        private Texture2D _texBg, _texSlot, _texGlow, _texClose, _texCloseHover;

        // Escala responsiva (mesmo esquema do SkillImprint): arte 1024x1023 encaixada
        // em min(altura da tela, PanelMaxH), centrada.
        private float _scale = 1f;
        private int _ox, _oy;

        private sealed class Node
        {
            public ServerMasterSkills.Entry Entry;
            public int SubCol;
            public Rectangle Rect;      // rect do soquete em coords de TELA (recalculado no draw)
        }

        private readonly List<Node> _nodes = new();
        private byte _builtClass = 0xFF;
        private ushort _hoverSkill;
        private Rectangle _closeRect;
        private bool _closeHover;

        // Arraste da janela pela BARRA DE TÍTULO (igual às demais janelas):
        // deslocamento do usuário somado ao painel centrado.
        private bool _dragging;
        private Point _dragLast;
        private int _userOX, _userOY;

        public MasteryTreeControl(CharacterState state)
        {
            _state = state;
            Instance = this;
            Interactive = true;
            AutoViewSize = false;
            ControlSize = new Point(Constants.BASE_UI_WIDTH, Constants.BASE_UI_HEIGHT);
            ViewSize = ControlSize;
            X = 0; Y = 0;
            Visible = false;
        }

        public override async Task Load()
        {
            await base.Load();
            var tl = TextureLoader.Instance;
            async Task<Texture2D> L(string p) { try { return await tl.PrepareAndGetTexture(p); } catch { return null; } }
            _texBg = await L(BgPath);
            _texSlot = await L(SlotPath);
            _texGlow = await L(GlowPath);
            _texClose = await L(ClosePath);
            _texCloseHover = await L(CloseHoverPath);

            // Atlas dos ícones (normal + apagado) pré-decodificado, como no Imprint.
            foreach (var path in SkillIconAtlas.TexturePaths)
            {
                await L(path);
                await L(SkillIconRenderer.DisabledVariant(path));
            }
        }

        public void Toggle()
        {
            Visible = !Visible;
            if (Visible)
            {
                BringToFront();
                SoundController.Instance.PlayBuffer("Sound/iCreateWindow.wav");
            }
        }

        // ── Árvore da classe atual ───────────────────────────────────────
        // SubCol por nó: filho herda a coluna do pai (corrente vertical); dependência
        // no MESMO rank vai pra coluna ao lado (corrente horizontal); sem pai = primeira
        // coluna livre do rank. Determinístico (ordem por Number).
        private void EnsureTree()
        {
            byte masterClass = ServerMasterSkills.ToMasterClass((byte)_state.Class);
            if (masterClass == _builtClass) return;
            _builtClass = masterClass;
            _nodes.Clear();

            var byNumber = new Dictionary<ushort, Node>();
            var occupied = new HashSet<(byte Root, byte Rank, int Sub)>();

            foreach (var e in ServerMasterSkills.All.OrderBy(e => e.Number))
            {
                if (Array.IndexOf(e.Classes, masterClass) < 0) continue;

                int sub = 0;
                if (e.Required != 0 && byNumber.TryGetValue(e.Required, out var parent)
                    && parent.Entry.Root == e.Root)
                {
                    sub = e.Rank > parent.Entry.Rank ? parent.SubCol : parent.SubCol + 1;
                }
                while (occupied.Contains((e.Root, e.Rank, sub)))
                    sub++;
                sub = Math.Min(sub, MasteryLayout.SubCols - 1);
                while (occupied.Contains((e.Root, e.Rank, sub)))
                    sub++;   // último recurso: estoura à direita (não deve acontecer)

                occupied.Add((e.Root, e.Rank, sub));
                var node = new Node { Entry = e, SubCol = sub };
                _nodes.Add(node);
                byNumber[e.Number] = node;
            }
        }

        // ── Geometria ────────────────────────────────────────────────────
        private void ComputeScale()
        {
            int scrW = Constants.BASE_UI_WIDTH, scrH = Constants.BASE_UI_HEIGHT;
            // PADRÃO do projeto (igual ao Imprint.ComputeScale): o painel ocupa 100% da
            // ALTURA VIRTUAL (720) — o UiScaler estica pra tela física, então a janela
            // cresce junto com a tela como as demais.
            _scale = scrH / MasteryLayout.ArtH;
            int panelW = (int)(MasteryLayout.ArtW * _scale);
            int panelH = (int)(MasteryLayout.ArtH * _scale);
            // centrado + deslocamento do arraste, preso DENTRO da tela
            _ox = Math.Clamp((scrW - panelW) / 2 + _userOX, 0, Math.Max(0, scrW - panelW));
            _oy = Math.Clamp((scrH - panelH) / 2 + _userOY, 0, Math.Max(0, scrH - panelH));
            // re-deriva o offset do clamp (evita acumular "puxão" fora da tela)
            _userOX = _ox - (scrW - panelW) / 2;
            _userOY = _oy - (scrH - panelH) / 2;
        }

        private Rectangle R(float x, float y, float w, float h) =>
            new((int)(_ox + x * _scale), (int)(_oy + y * _scale),
                (int)(w * _scale), (int)(h * _scale));

        private float ColX(byte root) => root switch
        {
            0 => MasteryLayout.Col1X,
            1 => MasteryLayout.Col2X,
            _ => MasteryLayout.Col3X,
        };

        // Centro (em coords de ARTE) do nó.
        private Vector2 NodeCenterArt(Node n)
        {
            float colX = ColX(n.Entry.Root);
            float usable = MasteryLayout.ColW - MasteryLayout.SubColPad * 2f;
            float subW = usable / MasteryLayout.SubCols;
            float cx = colX + MasteryLayout.SubColPad + subW * (n.SubCol + 0.5f);
            float cy = MasteryLayout.FirstRowCY + (n.Entry.Rank - 1) * MasteryLayout.RowH;
            return new Vector2(cx, cy);
        }

        private Rectangle NodeRect(Node n)
        {
            var c = NodeCenterArt(n);
            float half = MasteryLayout.NodeSize / 2f;
            return R(c.X - half, c.Y - half, MasteryLayout.NodeSize, MasteryLayout.NodeSize);
        }

        // ── Input ────────────────────────────────────────────────────────
        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            if (!Visible) return;

            EnsureTree();
            ComputeScale();

            var mouse = MuGame.Instance.UiMouseState;
            var prev = MuGame.Instance.PrevUiMouseState;
            var p = mouse.Position;
            bool down = mouse.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed;
            bool click = down
                         && prev.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Released;

            _closeRect = R(MasteryLayout.CloseX, MasteryLayout.CloseY,
                           MasteryLayout.CloseSize, MasteryLayout.CloseSize);
            _closeHover = _closeRect.Contains(p);

            // Arraste pela barra de título (área acima das colunas, fora do X).
            var titleRect = R(0, 0, MasteryLayout.ArtW, MasteryLayout.DragBarH);
            if (_dragging)
            {
                if (!down) _dragging = false;
                else
                {
                    _userOX += p.X - _dragLast.X;
                    _userOY += p.Y - _dragLast.Y;
                    _dragLast = p;
                    ComputeScale();
                    Scene?.SetMouseInputConsumed();
                    return;   // arrastando: não processa nós/close
                }
            }
            else if (click && titleRect.Contains(p) && !_closeHover)
            {
                _dragging = true;
                _dragLast = p;
                Scene?.SetMouseInputConsumed();
                return;
            }

            _hoverSkill = 0;
            foreach (var n in _nodes)
            {
                n.Rect = NodeRect(n);
                if (n.Rect.Contains(p)) _hoverSkill = n.Entry.Number;
            }

            if (!click) return;

            if (_closeHover)
            {
                Visible = false;
                Scene?.SetMouseInputConsumed();
                return;
            }

            var panelRect = R(0, 0, MasteryLayout.ArtW, MasteryLayout.ArtH);
            if (!panelRect.Contains(p)) return;   // clique fora: deixa passar (mundo)

            Scene?.SetMouseInputConsumed();

            if (_hoverSkill != 0)
                TryLearn(_hoverSkill);
        }

        private void TryLearn(ushort skillId)
        {
            if (_state.MasterLevelUpPoints == 0)
            {
                AddChat("No master points available.");
                return;
            }

            var node = _nodes.FirstOrDefault(n => n.Entry.Number == skillId);
            if (node == null) return;

            int level = GetLevel(skillId);
            if (level >= node.Entry.MaxLevel)
            {
                AddChat("This skill is already at maximum level.");
                return;
            }
            if (node.Entry.Required != 0 && GetLevel(node.Entry.Required) < 10)
            {
                AddChat($"Requires {ServerMasterSkills.All.First(e => e.Number == node.Entry.Required).Name} at level 10.");
                return;
            }

            // O SERVIDOR valida de verdade; o gate local é só UX.
            var svc = MuGame.Network?.GetCharacterService();
            if (svc == null) return;
            _ = svc.SendAddMasterSkillPointRequestAsync(skillId);
        }

        private static void AddChat(string msg)
        {
            var scene = MuGame.Instance?.ActiveScene as Scenes.GameScene;
            scene?.ChatLog?.AddMessage("System", msg, Models.MessageType.System);
        }

        private int GetLevel(ushort skillId) =>
            _state.GetSkills()?.FirstOrDefault(s => s.SkillId == skillId)?.SkillLevel ?? 0;

        // ── Draw ─────────────────────────────────────────────────────────
        public override void Draw(GameTime gameTime)
        {
            if (!Visible) return;
            var sb = GraphicsManager.Instance?.Sprite;
            var pixel = GraphicsManager.Instance?.Pixel;
            var font = GraphicsManager.Instance?.Font;
            if (sb == null || pixel == null || font == null) return;

            EnsureTree();
            ComputeScale();

            // Batch próprio LinearClamp (UI reescalada; PointClamp serrilha) — padrão §4.
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                     null, null, null, UiScaler.SpriteTransform);

            var panel = R(0, 0, MasteryLayout.ArtW, MasteryLayout.ArtH);
            if (_texBg != null && !_texBg.IsDisposed)
                sb.Draw(_texBg, panel, Color.White);
            else
                sb.Draw(pixel, panel, new Color(18, 14, 12) * 0.96f);

            DrawHeader(sb, font);
            DrawLinks(sb, pixel);
            DrawNodes(sb, font);
            DrawClose(sb);
            DrawTooltip(sb, font, pixel);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                     null, null, null, UiScaler.SpriteTransform);

            base.Draw(gameTime);
        }

        private void DrawCentered(SpriteBatch sb, SpriteFont font, string text, float cxArt, float cyArt,
                                  float fontPt, Color color, float shadow = 0.7f)
        {
            float sc = fontPt / 25f * _scale;
            var size = font.MeasureString(text) * sc;
            var pos = new Vector2(_ox + cxArt * _scale - size.X / 2f, _oy + cyArt * _scale - size.Y / 2f);
            if (shadow > 0f)
                sb.DrawString(font, text, pos + new Vector2(1, 1), Color.Black * shadow, 0f, Vector2.Zero, sc, SpriteEffects.None, 0f);
            sb.DrawString(font, text, pos, color, 0f, Vector2.Zero, sc, SpriteEffects.None, 0f);
        }

        private void DrawHeader(SpriteBatch sb, SpriteFont font)
        {
            var gold = new Color(230, 200, 120);
            var white = new Color(235, 230, 220);

            string cls = CharacterClassDatabase.GetClassName(_state.Class);
            DrawCentered(sb, font, cls, MasteryLayout.ClassNameCX, MasteryLayout.TitleTextY,
                         MasteryLayout.TitleFont, gold);

            float expPct = _state.MasterExperienceForNextLevel > 0
                ? MathHelper.Clamp((float)(_state.MasterExperience / (double)_state.MasterExperienceForNextLevel) * 100f, 0f, 100f)
                : 0f;
            DrawCentered(sb, font, $"Level: {_state.MasterLevel}", MasteryLayout.Field1CX,
                         MasteryLayout.FieldTextY, MasteryLayout.FieldFont, white);
            DrawCentered(sb, font, $"Points: {_state.MasterLevelUpPoints}", MasteryLayout.Field2CX,
                         MasteryLayout.FieldTextY, MasteryLayout.FieldFont, white);
            DrawCentered(sb, font, $"EXP: {expPct:0.00}%", MasteryLayout.Field3CX,
                         MasteryLayout.FieldTextY, MasteryLayout.FieldFont, white);

            DrawCentered(sb, font, MasteryLayout.ColTitleLeft,
                         MasteryLayout.Col1X + MasteryLayout.ColW / 2f, MasteryLayout.ColHeaderY,
                         MasteryLayout.ColHeaderFont, white);
            DrawCentered(sb, font, MasteryLayout.ColTitleMiddle,
                         MasteryLayout.Col2X + MasteryLayout.ColW / 2f, MasteryLayout.ColHeaderY,
                         MasteryLayout.ColHeaderFont, white);
            DrawCentered(sb, font, MasteryLayout.ColTitleRight,
                         MasteryLayout.Col3X + MasteryLayout.ColW / 2f, MasteryLayout.ColHeaderY,
                         MasteryLayout.ColHeaderFont, white);
        }

        // Conexões de pré-requisito: linha dourada pai→filho (vertical na mesma
        // sub-coluna, horizontal no mesmo rank, senão em L).
        private void DrawLinks(SpriteBatch sb, Texture2D pixel)
        {
            var gold = new Color(198, 152, 86) * 0.9f;
            int w = Math.Max(1, (int)(MasteryLayout.LinkWidth * _scale));
            float half = MasteryLayout.NodeSize / 2f;

            foreach (var n in _nodes)
            {
                if (n.Entry.Required == 0) continue;
                var parent = _nodes.FirstOrDefault(x => x.Entry.Number == n.Entry.Required);
                if (parent == null || parent.Entry.Root != n.Entry.Root) continue;

                var a = NodeCenterArt(parent);
                var b = NodeCenterArt(n);

                if (n.SubCol == parent.SubCol)
                {
                    // vertical: do pé do pai ao topo do filho
                    var r = R(a.X - MasteryLayout.LinkWidth / 2f, a.Y + half,
                              MasteryLayout.LinkWidth, (b.Y - half) - (a.Y + half));
                    if (r.Height > 0) sb.Draw(pixel, r, gold);
                }
                else if (n.Entry.Rank == parent.Entry.Rank)
                {
                    // horizontal: borda a borda
                    float x0 = Math.Min(a.X, b.X) + half, x1 = Math.Max(a.X, b.X) - half;
                    var r = R(x0, a.Y - MasteryLayout.LinkWidth / 2f, x1 - x0, MasteryLayout.LinkWidth);
                    if (r.Width > 0) sb.Draw(pixel, r, gold);
                }
                else
                {
                    // L: desce do pai até a linha do filho, depois vai até a borda dele
                    var rv = R(a.X - MasteryLayout.LinkWidth / 2f, a.Y + half,
                               MasteryLayout.LinkWidth, (b.Y - a.Y) - half);
                    if (rv.Height > 0) sb.Draw(pixel, rv, gold);
                    float hx0 = Math.Min(a.X, b.X - half), hx1 = Math.Max(a.X, b.X - half);
                    if (b.X < a.X) { hx0 = b.X + half; hx1 = a.X; }
                    var rh = R(hx0, b.Y - MasteryLayout.LinkWidth / 2f, hx1 - hx0, MasteryLayout.LinkWidth);
                    if (rh.Width > 0) sb.Draw(pixel, rh, gold);
                }
            }
        }

        private void DrawNodes(SpriteBatch sb, SpriteFont font)
        {
            var gold = new Color(230, 200, 120);
            var grey = new Color(150, 150, 150);

            foreach (var n in _nodes)
            {
                var r = n.Rect = NodeRect(n);

                if (_texSlot != null && !_texSlot.IsDisposed)
                    sb.Draw(_texSlot, r, Color.White);

                int level = GetLevel(n.Entry.Number);
                bool learned = level > 0;
                bool unlocked = n.Entry.Required == 0 || GetLevel(n.Entry.Required) >= 10;

                // Ícone circular HD (mesma pilha do Imprint): pré-cortado; bloqueada = atlas apagado.
                var iconRect = ShrinkToSquare(r, 0.98f);
                var cut = SkillIconRenderer.GetCircleIconTexture(n.Entry.Number, disabled: !learned);
                var tint = learned ? Color.White : (unlocked ? Color.White : new Color(110, 110, 110));
                if (cut != null)
                    sb.Draw(cut, iconRect, tint);
                else
                    SkillIconRenderer.DrawSkillCircle(sb, n.Entry.Number, iconRect,
                        learned ? Color.White : Color.Gray, 2f);

                if (_texGlow != null && !_texGlow.IsDisposed)
                    sb.Draw(_texGlow, ShrinkToSquare(r, 0.90f), Color.White);

                // Destaque de hover: aro fino dourado.
                if (n.Entry.Number == _hoverSkill)
                    DrawBorder(sb, GraphicsManager.Instance.Pixel, r, gold);

                // Contagem "N" no canto inf-dir do nó (como a referência).
                string cnt = level.ToString();
                float sc = MasteryLayout.CountFont / 25f * _scale;
                var pos = new Vector2(r.Right + MasteryLayout.CountDX * _scale,
                                      r.Bottom - MasteryLayout.CountDY * _scale);
                sb.DrawString(font, cnt, pos + Vector2.One, Color.Black * 0.8f, 0f, Vector2.Zero, sc, SpriteEffects.None, 0f);
                sb.DrawString(font, cnt, pos, learned ? gold : grey, 0f, Vector2.Zero, sc, SpriteEffects.None, 0f);
            }
        }

        private void DrawClose(SpriteBatch sb)
        {
            var tex = _closeHover && _texCloseHover != null ? _texCloseHover : _texClose;
            if (tex != null && !tex.IsDisposed)
                sb.Draw(tex, _closeRect, Color.White);
        }

        private void DrawTooltip(SpriteBatch sb, SpriteFont font, Texture2D pixel)
        {
            if (_hoverSkill == 0) return;
            var node = _nodes.FirstOrDefault(n => n.Entry.Number == _hoverSkill);
            if (node == null) return;

            int level = GetLevel(node.Entry.Number);
            var lines = new List<(string Text, Color Color)>
            {
                (node.Entry.Name, new Color(230, 200, 120)),
                ($"Level: {level} / {node.Entry.MaxLevel}", Color.White),
            };
            if (node.Entry.Required != 0)
            {
                string reqName = ServerMasterSkills.All.First(e => e.Number == node.Entry.Required).Name;
                bool ok = GetLevel(node.Entry.Required) >= 10;
                lines.Add(($"Requires: {reqName} Lv.10", ok ? new Color(120, 220, 120) : new Color(230, 110, 110)));
            }
            if (_state.MasterLevelUpPoints > 0 && level < node.Entry.MaxLevel)
                lines.Add(("Click to learn (+1)", new Color(180, 180, 180)));

            // Fonte maior pro TÍTULO; corpo na fonte do layout. Tudo medido antes.
            float scBody = MasteryLayout.TooltipFont / 25f * _scale;
            float scTitle = (MasteryLayout.TooltipFont + 2f) / 25f * _scale;
            const float lineGap = 5f;

            float maxW = 0, totH = 0;
            for (int i = 0; i < lines.Count; i++)
            {
                float sc0 = i == 0 ? scTitle : scBody;
                var s = font.MeasureString(lines[i].Text) * sc0;
                maxW = MathF.Max(maxW, s.X);
                totH += s.Y + lineGap;
            }

            // Ancorado no NÓ, nunca no cursor: no touch o dedo (e no desktop o cursor)
            // ficava NA FRENTE do texto. Nasce à DIREITA do nó; sem espaço, vai pra
            // esquerda; clamp na tela nos dois eixos.
            var nodeRect = node.Rect;
            int pad = (int)(12 * _scale);
            var box = new Rectangle(0, 0, (int)maxW + pad * 2, (int)totH + pad * 2 - (int)lineGap);
            box.X = nodeRect.Right + (int)(10 * _scale);
            box.Y = nodeRect.Y - (int)(6 * _scale);
            if (box.Right > Constants.BASE_UI_WIDTH)
                box.X = nodeRect.X - box.Width - (int)(10 * _scale);
            box.X = Math.Clamp(box.X, 0, Math.Max(0, Constants.BASE_UI_WIDTH - box.Width));
            box.Y = Math.Clamp(box.Y, 0, Math.Max(0, Constants.BASE_UI_HEIGHT - box.Height));

            // Fundo OPACO (o painel escuro atrás deixava o texto ilegível) + borda dupla.
            sb.Draw(pixel, new Rectangle(box.X + 3, box.Y + 3, box.Width, box.Height), Color.Black * 0.55f); // sombra
            sb.Draw(pixel, box, new Color(16, 13, 10));
            DrawBorder(sb, pixel, box, new Color(60, 48, 32));
            var inner = new Rectangle(box.X + 1, box.Y + 1, box.Width - 2, box.Height - 2);
            DrawBorder(sb, pixel, inner, new Color(198, 152, 86));

            float y = box.Y + pad;
            for (int i = 0; i < lines.Count; i++)
            {
                float sc0 = i == 0 ? scTitle : scBody;
                var (t, c) = lines[i];
                var s = font.MeasureString(t) * sc0;
                var pos = new Vector2(box.X + pad, y);
                sb.DrawString(font, t, pos + Vector2.One, Color.Black, 0f, Vector2.Zero, sc0, SpriteEffects.None, 0f);
                sb.DrawString(font, t, pos, c, 0f, Vector2.Zero, sc0, SpriteEffects.None, 0f);
                y += s.Y + lineGap;
            }
        }

        private static Rectangle ShrinkToSquare(Rectangle r, float f)
        {
            int side = (int)(Math.Min(r.Width, r.Height) * f);
            return new Rectangle(r.X + (r.Width - side) / 2, r.Y + (r.Height - side) / 2, side, side);
        }

        private static void DrawBorder(SpriteBatch sb, Texture2D pixel, Rectangle r, Color c)
        {
            sb.Draw(pixel, new Rectangle(r.X, r.Y, r.Width, 1), c);
            sb.Draw(pixel, new Rectangle(r.X, r.Bottom - 1, r.Width, 1), c);
            sb.Draw(pixel, new Rectangle(r.X, r.Y, 1, r.Height), c);
            sb.Draw(pixel, new Rectangle(r.Right - 1, r.Y, 1, r.Height), c);
        }
    }
}
