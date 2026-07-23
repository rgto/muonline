#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MUnique.OpenMU.Network.Packets;

namespace Client.Main.Controls.UI.SelectCharacter
{
    /// <summary>
    /// Lista de slots da tela de SELEÇÃO de personagem.
    ///
    /// Cada linha é um slot: com personagem mostra "Classe / Título" em cima e
    /// "Nome / Nível" embaixo; vazia mostra "Vacant Character Slot" — MESMA arte, só o
    /// texto muda. Clicar num slot com personagem carrega ele no pilar (um de cada vez).
    ///
    /// As setas NÃO são scroll: são o controle de REORDENAR e aparecem só no slot
    /// selecionado, movendo ele na lista. A ordem é do CLIENTE (salva no aparelho, por
    /// conta) — o servidor continua dono do slot real de cada personagem, então a ordem
    /// visual é um mapeamento por cima disso (ver CharacterSlotOrder).
    ///
    /// Posições vêm do editor (hud-edit-slots), em px do prefab 1334x750.
    /// </summary>
    public sealed class CharacterSlotListControl : UIControl
    {
        private const string ArtDir = "Interface/CharCreate/";

        private const float S = 720f / 750f;

        // ── Recortes MEDIDOS na folha char_slots.OZT (368x228, textura 512x256) ──────
        // 4 barras de 231x55; a 2ª (y=59) NÃO é usada.
        private const int BarW = 231, BarH = 55;
        private static readonly Rectangle SlotSelSrc = new(0, 2, BarW, BarH);      // selecionado
        private static readonly Rectangle SlotHoverSrc = new(0, 116, BarW, BarH);  // hover
        private static readonly Rectangle SlotNormalSrc = new(0, 173, BarW, BarH); // padrão (inclui Vacant)

        // ── Borda retangular (moldura por cima de TODO slot, sempre visível) ─────────
        // borda_retangular.OZT: 388x118 (tex 512x128). Centro transparente — o fundo do
        // slot (que muda de cor em hover/selected) aparece através dela. Posição/tamanho
        // são DELTAS sobre o rect do slot, ajustáveis no hud-edit-slots (SlotBorder*).
        private static readonly Rectangle BorderSrc = new(0, 0, 388, 118);


        // ── Layout (editor) ─────────────────────────────────────────────────────────
        private const float SlotCX = 476.6f, SlotTopY = 272.3f;
        private const float SlotW = 204.3f, SlotH = 58.3f;
        private const float SlotPitch = 60f;

        // ── Borda por-slot (ajustável no editor) ─────────────────────────────────────
        // A borda é desenhada centrada no slot, com estes deltas (px do prefab):
        //   BorderDX/DY move a moldura; BorderDW/DH aumenta/diminui a largura/altura dela.
        // O MESMO ajuste vale pra TODOS os slots.
        private const float BorderDX = 0f, BorderDY = 0f;
        private const float BorderDW = 0f, BorderDH = 0f;
        /// <summary>Nº de slots. É o limite de personagens do cliente — o servidor deve
        /// ter MaximumCharactersPerAccount igual ou maior.</summary>
        public const int SlotCount = 10;


        // Texto: mesma família do resto da tela.
        // ── Texto do slot (ajustável no EDITOR) ─────────────────────────────────────
        // Duas linhas: em cima "Classe | Título", embaixo "Nome | Nível". A linha de baixo
        // tem fonte própria porque o nome do personagem é o que mais importa ler.
        // São 5 textos INDEPENDENTES, cada um com fonte e deslocamento próprios:
        //   classe (esq/cima) | tipo de conta (dir/cima) | nome (esq/baixo) | nível (dir/baixo)
        //   e o "Vacant Character Slot" (centro) do slot vazio.
        // DX/DY deslocam cada um a partir da âncora dele (+DX direita, +DY baixo).
        // Bold* = usa a fonte NEGRITO (ArialBold). O MonoGame rasteriza a fonte num atlas,
        // então negrito é outra FONTE — não um peso que se ajusta em runtime.
        /// <summary>CLASSE — "Dark Wizard" (esquerda, linha de cima).</summary>
        private const float FontClass = 9f / 24f;
        private const float ClassDX = 0f, ClassDY = 0f;
        private const bool ClassBold = true;

        /// <summary>TIPO DE CONTA — "Commoner" (direita, linha de cima).</summary>
        private const float FontAccountType = 9f / 24f;
        private const float AccountTypeDX = 0f, AccountTypeDY = 0f;
        private const bool AccountTypeBold = true;

        /// <summary>NOME do personagem (esquerda, linha de baixo).</summary>
        private const float FontCharName = 12f / 24f;
        private const float CharNameDX = 0f, CharNameDY = 0f;
        private const bool CharNameBold = true;

        /// <summary>NÍVEL (direita, linha de baixo).</summary>
        private const float FontLevel = 12f / 24f;
        private const float LevelDX = 0f, LevelDY = 0f;
        private const bool LevelBold = true;

        /// <summary>"Vacant Character Slot" — o texto do slot vazio (centro).</summary>
        private const float FontSlotVacant = 10.5f / 24f;
        private const float VacantDX = 0f, VacantDY = 0f;
        private const bool VacantBold = true;

        /// <summary>Recuo do texto nas bordas esquerda/direita do slot.</summary>
        private const float TextPadX = 15f;
        /// <summary>Meia-distância entre as duas linhas: ↑ afasta uma da outra.</summary>
        private const float LineDY = 11f;

        private static readonly Color TextWhite = new(230, 233, 237);
        private static readonly Color TextGold = new(255, 215, 106);
        private static readonly Color Pressed = new(180, 180, 180);

        private Texture2D? _tex;
        private Texture2D? _texBorder;
        private readonly List<Rectangle> _slotRects = new();
        private int _pressedSlot = -1;

        /// <summary>Personagens na ORDEM VISUAL (já reordenada pelo usuário).</summary>
        private List<CharacterEntry> _entries = new();

        private int _selected;

        public sealed record CharacterEntry(string Name, CharacterClassNumber Class, ushort Level);

        /// <summary>Slot clicado: a cena carrega este personagem no pilar.</summary>
        public event EventHandler<string>? CharacterSelected;

        /// <summary>Slot vazio clicado: a cena abre a criação.</summary>
        public event EventHandler? EmptySlotClicked;


        public CharacterSlotListControl()
        {
            Interactive = true;
            AutoViewSize = false;
            ControlSize = new Point(Constants.BASE_UI_WIDTH, Constants.BASE_UI_HEIGHT);
            ViewSize = ControlSize;
            X = 0;
            Y = 0;
        }

        /// <summary>Nome do personagem selecionado, ou null se o slot está vazio.</summary>
        public string? SelectedName =>
            _selected >= 0 && _selected < _entries.Count ? _entries[_selected].Name : null;

        /// <summary>Índice do slot selecionado (-1 se nenhum).</summary>
        public int SelectedIndex => _selected;

        /// <summary>
        /// Y (em px do prefab) do CENTRO do slot selecionado — a lixeira usa isto pra ficar
        /// do lado dele. É a mesma fórmula que posiciona cada slot.
        /// </summary>
        public float SelectedSlotPrefabY => SlotTopY - SlotPitch * _selected;

        /// <summary>Avisa que a seleção mudou, pra quem precisa acompanhar (a lixeira).</summary>
        public event EventHandler? SelectionChanged;

        public override async Task Load()
        {
            _tex = await TextureLoader.Instance.PrepareAndGetTexture(ArtDir + "char_slots.OZT")!;
            _texBorder = await TextureLoader.Instance.PrepareAndGetTexture(ArtDir + "borda_retangular.OZT")!;
            LayoutRects();
            await base.Load();
        }

        /// <summary>Popula a lista na ordem que a cena mandar (já a ordem visual salva).</summary>
        public void SetCharacters(IEnumerable<CharacterEntry> entries)
        {
            _entries = entries.Take(SlotCount).ToList();
            _selected = _entries.Count > 0 ? 0 : -1;
            SelectionChanged?.Invoke(this, EventArgs.Empty);

            if (_selected >= 0)
                CharacterSelected?.Invoke(this, _entries[_selected].Name);
        }

        private static Vector2 P(float x, float y) => new(
            Constants.BASE_UI_WIDTH / 2f + x * S,
            Constants.BASE_UI_HEIGHT / 2f - y * S);

        private static Rectangle R(float x, float y, float w, float h)
        {
            var c = P(x, y);
            return new Rectangle(
                (int)MathF.Round(c.X - w * S / 2f),
                (int)MathF.Round(c.Y - h * S / 2f),
                (int)MathF.Round(w * S),
                (int)MathF.Round(h * S));
        }

        private void LayoutRects()
        {
            _slotRects.Clear();
            for (int i = 0; i < SlotCount; i++)
                _slotRects.Add(R(SlotCX, SlotTopY - SlotPitch * i, SlotW, SlotH));

        }


        protected override void OnScreenSizeChanged()
        {
            base.OnScreenSizeChanged();
            LayoutRects();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            if (!Visible) return;

            var mouse = MuGame.Instance.Mouse;
            var prev = MuGame.Instance.PrevMouseState;
            var p = MuGame.Instance.UiMouseState.Position;

            bool down = mouse.LeftButton == ButtonState.Pressed;
            bool wasDown = prev.LeftButton == ButtonState.Pressed;

            if (down && !wasDown)
            {
                _pressedSlot = _slotRects.FindIndex(r => r.Contains(p));
                System.Console.WriteLine($"[CREATEPROBE] mousedown pos={p} pressedSlot={_pressedSlot} visible={Visible} interactive={Interactive}");
            }
            else if (!down && wasDown)
            {
                if (_pressedSlot >= 0 && _slotRects[_pressedSlot].Contains(p))
                    Select(_pressedSlot);
                else
                    System.Console.WriteLine($"[CREATEPROBE] mouseup SEM select: pressedSlot={_pressedSlot} pos={p} contains={(_pressedSlot >= 0 && _pressedSlot < _slotRects.Count && _slotRects[_pressedSlot].Contains(p))}");

                _pressedSlot = -1;
            }
        }


        /// <summary>
        /// Troca o slot selecionado. Reavisa MESMO se for o slot que já estava selecionado:
        /// o clique tem que reagir sempre. (Com um `if (i == _selected) return;` aqui, clicar
        /// no 1º slot — o pré-selecionado — não fazia nada, e o personagem dele só aparecia
        /// depois de clicar noutro e voltar.)
        /// </summary>
        private void Select(int i)
        {
            _selected = i;
            System.Console.WriteLine($"[CREATEPROBE] Slot.Select({i}) entries={_entries.Count} vazio={(i >= _entries.Count)} temEmptyHandler={(EmptySlotClicked != null)}");
            SelectionChanged?.Invoke(this, EventArgs.Empty);

            if (i < _entries.Count)
                CharacterSelected?.Invoke(this, _entries[i].Name);   // carrega no pilar
            else
                EmptySlotClicked?.Invoke(this, EventArgs.Empty);     // slot vazio
        }


        public override void Draw(GameTime gameTime)
        {
            if (!Visible || _tex == null) return;

            var sb = GraphicsManager.Instance.Sprite;
            var font = GraphicsManager.Instance.Font;
            if (sb == null || font == null) return;

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                     null, null, null, UiScaler.SpriteTransform);

            var mouse = MuGame.Instance.UiMouseState.Position;

            for (int i = 0; i < _slotRects.Count; i++)
            {
                var rect = _slotRects[i];
                bool hover = rect.Contains(mouse);

                // Vazio usa a MESMA arte do que tem personagem — só o texto muda.
                var src = i == _selected ? SlotSelSrc
                        : hover || _pressedSlot == i ? SlotHoverSrc
                        : SlotNormalSrc;
                sb.Draw(_tex, rect, src, Color.White);

                // Borda por cima do fundo (centro transparente → o fundo colorido aparece).
                // Sempre visível, independente de hover/selected. Mesmo ajuste em todo slot.
                if (_texBorder != null)
                    sb.Draw(_texBorder, BorderRect(rect), BorderSrc, Color.White);

                if (i < _entries.Count) DrawCharacterRow(font, rect, _entries[i]);
                else DrawString(font, "Vacant Character Slot", Off(rect, VacantDX, VacantDY),
                                TextWhite, FontSlotVacant, center: true, bold: VacantBold);
            }



            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                     null, null, null, UiScaler.SpriteTransform);

            base.Draw(gameTime);
        }

        /// <summary>
        /// Os 4 textos do slot com personagem. Cada um tem âncora, fonte e deslocamento
        /// próprios — é o que permite ajustar um sem mexer nos outros.
        /// </summary>
        private void DrawCharacterRow(SpriteFont font, Rectangle rect, CharacterEntry e)
        {
            int pad = (int)MathF.Round(TextPadX * S);
            int dy = (int)MathF.Round(LineDY * S);

            var top = new Rectangle(rect.X + pad, rect.Y - dy, rect.Width - pad * 2, rect.Height);
            var bot = new Rectangle(rect.X + pad, rect.Y + dy, rect.Width - pad * 2, rect.Height);

            DrawString(font, ClassName(e.Class), Off(top, ClassDX, ClassDY), TextWhite, FontClass,
                       bold: ClassBold);
            DrawString(font, "Commoner", Off(top, AccountTypeDX, AccountTypeDY), TextWhite,
                       FontAccountType, right: true, bold: AccountTypeBold);
            DrawString(font, e.Name, Off(bot, CharNameDX, CharNameDY), TextGold, FontCharName,
                       bold: CharNameBold);
            DrawString(font, e.Level.ToString(), Off(bot, LevelDX, LevelDY), TextGold,
                       FontLevel, right: true, bold: LevelBold);
        }

        /// <summary>
        /// Rect da BORDA: parte do rect do slot e aplica os deltas (BorderDX/DY/DW/DH), tudo
        /// na escala da tela. A borda cresce/encolhe a partir do CENTRO do slot.
        /// </summary>
        private static Rectangle BorderRect(Rectangle slot)
        {
            int dx = (int)MathF.Round(BorderDX * S);
            int dy = (int)MathF.Round(BorderDY * S);
            int dw = (int)MathF.Round(BorderDW * S);
            int dh = (int)MathF.Round(BorderDH * S);
            return new Rectangle(
                slot.X + dx - dw / 2,
                slot.Y + dy - dh / 2,
                slot.Width + dw,
                slot.Height + dh);
        }

        /// <summary>Desloca o rect do texto (+dx direita, +dy baixo), na escala da tela.</summary>
        private static Rectangle Off(Rectangle r, float dx, float dy) => new(
            r.X + (int)MathF.Round(dx * S), r.Y + (int)MathF.Round(dy * S), r.Width, r.Height);

        private static string ClassName(CharacterClassNumber c) => c switch
        {
            CharacterClassNumber.DarkWizard or CharacterClassNumber.SoulMaster
                or CharacterClassNumber.GrandMaster => "Dark Wizard",
            CharacterClassNumber.DarkKnight or CharacterClassNumber.BladeKnight
                or CharacterClassNumber.BladeMaster => "Dark Knight",
            CharacterClassNumber.FairyElf or CharacterClassNumber.MuseElf
                or CharacterClassNumber.HighElf => "Elf",
            CharacterClassNumber.MagicGladiator or CharacterClassNumber.DuelMaster => "Magic Gladiator",
            CharacterClassNumber.DarkLord or CharacterClassNumber.LordEmperor => "Dark Lord",
            CharacterClassNumber.Summoner or CharacterClassNumber.BloodySummoner
                or CharacterClassNumber.DimensionMaster => "Summoner",
            CharacterClassNumber.RageFighter or CharacterClassNumber.FistMaster => "Rage Fighter",
            _ => c.ToString(),
        };

        private static void DrawString(SpriteFont font, string text, Rectangle rect, Color color,
                                       float scale, bool center = false, bool right = false,
                                       bool bold = false)
        {
            if (string.IsNullOrEmpty(text)) return;

            // Negrito = OUTRA fonte (atlas próprio), não um peso. Se a ArialBold não tiver
            // carregado por algum motivo, cai na regular em vez de sumir com o texto.
            if (bold) font = GraphicsManager.Instance.FontBold ?? font;

            var sb = GraphicsManager.Instance.Sprite;
            Vector2 size = font.MeasureString(text) * scale;
            float x = center ? rect.X + (rect.Width - size.X) / 2f
                    : right ? rect.Right - size.X
                    : rect.X;
            float y = rect.Y + (rect.Height - size.Y) / 2f;
            var pos = new Vector2(x, y);

            sb.DrawString(font, text, pos + new Vector2(1, -1), Color.Black * 0.5f,
                          0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            sb.DrawString(font, text, pos, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }
    }
}
