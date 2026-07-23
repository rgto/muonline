using System;
using System.Threading.Tasks;
using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Scenes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Client.Main.Controls.UI.Game.Hud
{
    /// <summary>
    /// Botão de menu (engrenagem btn_func do MU Immortal) no canto inferior direito +
    /// fold-out em grade de 2 colunas com os ícones Forge/Guide/Allocate/Friend/Skill/
    /// Mail/Settings (sprites reais do MU Immortal, grid_funcBtn do Main_MainMenuUI).
    /// Por ora só a entrada Skill é funcional (abre o painel de skills).
    /// </summary>
    public sealed class TouchMenuControl : UIControl
    {
        // Ícones bronze do canto superior direito (arte do usuário, estilo DI-Immortal),
        // desenhados SEM aro/chrome — só o ícone. notification (sino) e menu (grade 2x2)
        // na 1ª linha; bag (bolsa) na 2ª linha, tudo ancorado no topo direito.
        private const string NotifTexPath = "Interface/DH/mi_btn_notification.OZP";
        private const string GearTexPath = "Interface/DH/mi_btn_menu.OZP";
        private const string SqBagTexPath = "Interface/DH/mi_btn_bag.OZP";

        private sealed record MenuEntry(string Label, string TexPath, Action OnTap);

        // Layout (UI virtual 1280x720). CANTO SUPERIOR DIREITO (mock do usuário):
        //   linha 1: [notification] [menu]   (lado a lado, colados no topo direito)
        //   linha 2:            [bag]         (abaixo do menu, alinhado à direita)
        private const int GearSize = 48;          // tamanho de TODOS os botões (fixos e fold-out)
        private const int BtnGap = 20;            // vão entre botões (afastado: evita clique errado)
        private const int MarginRight = 16;       // margem da borda direita
        private const int MarginTop = 14;         // margem do topo
        // Centros X: menu na coluna direita, notification à esquerda dele.
        private const int MenuBtnCX = 1280 - MarginRight - GearSize / 2;               // ~1233
        private const int NotifBtnCX = MenuBtnCX - GearSize - BtnGap;                  // ~1160
        private const int Row1CY = MarginTop + GearSize / 2;                           // ~44
        private const int Row2CY = Row1CY + GearSize + BtnGap;                         // ~117
        // Menu e notification na linha 1; bag na linha 2 (sob o menu).
        private const int SideBtnX = MenuBtnCX;   // coluna do menu/bag (fold-out ancora aqui)
        private const int MenuBtnCY = Row1CY;     // centro Y do botão de menu
        private const int BagBtnCY = Row2CY;      // centro Y do botão do baú
        // Ícones do fold-out IDÊNTICOS aos 3 botões fixos (notif/menu/bag): MESMO tamanho
        // (GearSize) e MESMO passo entre centros (GearSize+BtnGap) na horizontal E vertical.
        // Sem isso os do fold-out saíam maiores e com vão diferente → vácuos (feedback).
        // Ícones do fold-out 10% MAIORES que os fixos: com o mesmo box de 40 eles pareciam
        // menores que os 3 visíveis (feedback do usuário). O PASSO da grade continua vindo
        // de GearSize+BtnGap, então só o desenho cresce — o alinhamento não muda.
        private const int ItemSize = GearSize;               // TODOS do mesmo tamanho (48)
        private const int ItemGapX = BtnGap;                // = 12, mesmo vão horizontal
        private const int RowStep = GearSize + BtnGap;      // = 52, MESMO passo vertical dos fixos
        private const int LabelH = 22;

        // Posição (coluna, linha) de cada entrada do fold-out. LINHA 0 = TOPO.
        // Botões fixos ocupam: notification (col0,row0), menu (col1,row0), bag (col1,row1).
        // O fold-out desce a partir daí sem sobrepor esses três: começa em (col0,row1)
        // e preenche as linhas abaixo em 2 colunas.
        // Layout da imagem 2 do usuário — preenchimento CONTÍNUO 2 colunas, SEM vácuo:
        //   row0: notif  | menu        (fixos)
        //   row1: Forge  | bag         (bag é fixo; Forge preenche a col0)
        //   row2: Guide  | Allocate
        //   row3: Friend | Skill
        //   row4: Settings | Mail
        //   row5: Exit   | —
        // A ordem AQUI segue a ordem do array _entries:
        // [0]Forge [1]Guide [2]Allocate [3]Friend [4]Skill [5]Mail [6]Potions [7]Settings [8]Exit
        //   row0: notif  | menu        (fixos)
        //   row1: Forge  | bag         (bag fixo)
        //   row2: Guide  | Allocate
        //   row3: Friend | Skill
        //   row4: Potions| Mail        (potion-tab entra no lugar da engrenagem)
        //   row5: Settings | Exit      (Settings desceu uma linha)
        private static readonly (int Col, int Row)[] GridMap =
        {
            (0, 1), // Forge     (ao lado do bag → tira o vácuo sob a bag)
            (0, 2), // Guide
            (1, 2), // Allocate
            (0, 3), // Friend
            (1, 3), // Skill
            (0, 4), // Mail       (trocado com Potions)
            (1, 4), // Potions    (potion-tab — trocado com Mail)
            (0, 5), // Settings
            (1, 5), // Exit
        };

        /// <summary>Tela Skill Imprint (estilo novo) — a entrada "Skill" (livro) do fold-out
        /// faz toggle nela. O sino não abre mais nada.</summary>
        public SkillImprintControl ImprintPanel { get; set; }

        /// <summary>Tela Potion Imprint (config das 5 poções) — entrada "Potions" do fold-out.</summary>
        public PotionImprintControl PotionPanel { get; set; }

        /// <summary>
        /// Cluster de ataque/skills do touch HUD — some com CROSSFADE suave enquanto o
        /// fold-out ou o painel de skills estão abertos (pedido do usuário).
        /// </summary>
        public TouchActionButtonsControl HotbarToHide { get; set; }

        private Texture2D _texGear;
        private Texture2D _texNotif;
        private Texture2D _texSqBag;
        private Rectangle _bagRect;
        private Rectangle _notifRect;
        private readonly MenuEntry[] _entries;
        private readonly Texture2D[] _entryTex;
        private readonly Rectangle[] _entryRects;
        private Rectangle _gearRect;
        private bool _open;
        private float _gearSpin; // rotaçãozinha ao abrir, como o mobile (DOLocalRotate 45°)
        private float _foldAnim; // 0..1 — fade/slide do fold-out (transição suave)

        public TouchMenuControl()
        {
            Interactive = true;
            AutoViewSize = false;
            _entries = new[]
            {
                new MenuEntry("Forge", "Interface/DH/mi_menu_forge.OZP", null),
                new MenuEntry("Guide", "Interface/DH/mi_menu_guide.OZP", null),
                new MenuEntry("Allocate", "Interface/DH/mi_menu_allocate.OZP", OpenMastery),
                new MenuEntry("Friend", "Interface/DH/mi_menu_friend.OZP", null),
                // "Skill" (livro) agora abre a tela nova Skill Imprint (antes abria a antiga).
                new MenuEntry("Skill", "Interface/DH/mi_menu_skill.OZP", OpenImprint),
                new MenuEntry("Mail", "Interface/DH/mi_menu_mail.OZP", null),
                // potion-tab entra no lugar da engrenagem (Settings), que desce uma linha.
                new MenuEntry("Potions", "Interface/DH/mi_menu_potion_tab.OZP", OpenPotionImprint),
                new MenuEntry("Settings", "Interface/DH/mi_menu_settings.OZP", OpenExitMenu),
                new MenuEntry("Exit", "Interface/DH/mi_menu_exit.OZP", ConfirmAndQuit),
            };
            _entryTex = new Texture2D[_entries.Length];
            _entryRects = new Rectangle[_entries.Length];

            // CRÍTICO: os bounds do controle DEVEM ficar restritos à área realmente usada —
            // um controle interativo fullscreen captura o ponteiro e MATA o point-to-click
            // do mundo. Fechado = só a engrenagem; aberto = engrenagem + grade.
            // TEM que rodar DEPOIS de _entries/_entryRects existirem (LayoutRects itera
            // ambos; chamar antes = NRE no construtor → GameScene nem monta → login
            // travado em 0% pra QUALQUER char).
            UpdateBounds();
        }

        // "Skill" (livro) do fold-out: abre/fecha a tela Skill Imprint (estilo novo).
        private void OpenImprint()
        {
            _open = false;
            UpdateBounds();
            ImprintPanel?.Toggle();
            if (ImprintPanel != null && ImprintPanel.Visible)
                ImprintPanel.BringToFront();
        }

        // "Potions" do fold-out: abre/fecha a tela Potion Imprint (config das 5 poções).
        private void OpenPotionImprint()
        {
            _open = false;
            UpdateBounds();
            PotionPanel?.Toggle();
            if (PotionPanel != null && PotionPanel.Visible)
                PotionPanel.BringToFront();
        }

        // Ícone de ÁRVORE (Allocate): abre a janela de Mastery (master skill tree).
        private void OpenMastery()
        {
            _open = false;
            UpdateBounds();
            MasteryTreeControl.Instance?.Toggle();
        }

        // Engrenagem (Settings): abre o menu de pausa/logout (o mesmo do ESC — tem
        // sair/voltar à seleção/opções). Ação movida do Exit pra cá (pedido do usuário).
        private void OpenExitMenu()
        {
            _open = false;
            UpdateBounds();
            var pauseMenu = (Scene as GameScene)?.PauseMenu;
            if (pauseMenu == null)
                return;
            pauseMenu.Visible = true;
            pauseMenu.BringToFront();
            if (Scene != null)
                Scene.FocusControl = pauseMenu;
        }

        // Exit: popup padrão OK/Cancel; ao confirmar, SAI do jogo (fecha o app).
        private void ConfirmAndQuit()
        {
            _open = false;
            UpdateBounds();
            ConfirmDialog.Show("Do you want to exit the game?", () =>
            {
#if !IOS
                MuGame.ScheduleOnMainThread(() => MuGame.Instance.Exit());
#endif
            });
        }

        public override async Task Load()
        {
            await base.Load();
            var tl = TextureLoader.Instance;
            async Task<Texture2D> L(string p) { try { return await tl.PrepareAndGetTexture(p); } catch { return null; } }
            _texGear = await L(GearTexPath);
            _texNotif = await L(NotifTexPath);
            _texSqBag = await L(SqBagTexPath);
            for (int i = 0; i < _entries.Length; i++)
                _entryTex[i] = await L(_entries[i].TexPath);
        }

        private void UpdateBounds()
        {
            LayoutRects();
            var bounds = Rectangle.Union(_gearRect, _bagRect);
            bounds = Rectangle.Union(bounds, _notifRect);
            if (_open)
            {
                for (int i = 0; i < _entryRects.Length; i++)
                {
                    var r = _entryRects[i];
                    r.Height += LabelH; // inclui o label abaixo do ícone
                    bounds = Rectangle.Union(bounds, r);
                }
            }
            // Margem de segurança: sem ela o disco do topo era CLIPADO na borda do controle.
            bounds = Rectangle.Union(bounds, new Rectangle(bounds.X - 12, bounds.Y - 12, bounds.Width + 24, bounds.Height + 24));
            X = bounds.X;
            Y = bounds.Y;
            ControlSize = new Point(bounds.Width, bounds.Height);
            ViewSize = ControlSize;
        }

        private void LayoutRects()
        {
            // Canto superior direito: notification + menu na linha 1, bag na linha 2.
            _notifRect = new Rectangle(NotifBtnCX - GearSize / 2, Row1CY - GearSize / 2, GearSize, GearSize);
            _gearRect = new Rectangle(MenuBtnCX - GearSize / 2, MenuBtnCY - GearSize / 2, GearSize, GearSize);
            _bagRect = new Rectangle(SideBtnX - GearSize / 2, BagBtnCY - GearSize / 2, GearSize, GearSize);

            // Grade 2 colunas alinhada aos botões fixos. Os CENTROS vêm de GearSize (mesma
            // coluna/linha dos fixos); o box desenhado é ItemSize (10% maior), centrado nesse
            // centro — assim os ícones ficam maiores SEM desalinhar da grade.
            int col1CX = SideBtnX;                              // centro X da coluna direita
            int col0CX = col1CX - GearSize - ItemGapX;          // centro X da coluna esquerda
            int topCY = MenuBtnCY;                              // centro Y da linha 0
            for (int i = 0; i < _entries.Length; i++)
            {
                var (col, row) = GridMap[i];
                int cx = (col == 0) ? col0CX : col1CX;
                int cy = topCY + row * RowStep;
                _entryRects[i] = new Rectangle(cx - ItemSize / 2, cy - ItemSize / 2, ItemSize, ItemSize);
            }
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            if (!Visible)
                return;

            UpdateBounds();

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            float k = MathHelper.Clamp(dt * 12f, 0f, 1f);

            // Transições suaves: fold-out (fade+slide) e crossfade da hotbar.
            float foldTarget = _open ? 1f : 0f;
            _foldAnim = MathHelper.Lerp(_foldAnim, foldTarget, k);
            if (MathF.Abs(_foldAnim - foldTarget) < 0.01f) _foldAnim = foldTarget;

            if (HotbarToHide != null)
            {
                bool hideBar = _open || ImprintPanel?.Visible == true || PotionPanel?.Visible == true;
                float aTarget = hideBar ? 0f : 1f;
                HotbarToHide.MasterAlpha = MathHelper.Lerp(HotbarToHide.MasterAlpha, aTarget, k);
                if (MathF.Abs(HotbarToHide.MasterAlpha - aTarget) < 0.01f)
                    HotbarToHide.MasterAlpha = aTarget;
            }

            float target = _open ? MathHelper.ToRadians(45f) : 0f;
            _gearSpin = MathHelper.Lerp(_gearSpin, target, k);

            var mouse = MuGame.Instance.UiMouseState;
            var prev = MuGame.Instance.PrevUiMouseState;
            bool justPressed = mouse.LeftButton == ButtonState.Pressed && prev.LeftButton == ButtonState.Released;
            if (!justPressed)
                return;
            var p = new Point(mouse.Position.X, mouse.Position.Y);

            if (_notifRect.Contains(p))
            {
                // Sino: não abre mais nada (era a tela Skill Imprint; movida pro "livro" do
                // fold-out). Só fecha o fold-out se estiver aberto e consome o clique.
                Scene?.SetMouseInputConsumed();
                _open = false;
                UpdateBounds();
                return;
            }

            if (_gearRect.Contains(p))
            {
                Scene?.SetMouseInputConsumed();
                _open = !_open;
                UpdateBounds();
                return;
            }

            if (_bagRect.Contains(p))
            {
                // Baú = o MESMO inventário da tecla I (InventoryControl), não a bolsa
                // do painel de skills (pedido do usuário).
                Scene?.SetMouseInputConsumed();
                _open = false;
                UpdateBounds();
                var inv = Inventory.InventoryControl.Instance;
                if (inv != null)
                {
                    if (inv.Visible)
                    {
                        inv.Hide();
                    }
                    else
                    {
                        // ADIA o Show pro FIM do frame: Show() faz BringToFront()+
                        // FocusControl=inv, reordenando a árvore de UI SOB o dedo ainda
                        // pressionado. Feito aqui (dentro do ciclo de input), o release do
                        // MESMO toque via MouseControl != currentMouseControl e a bag
                        // "comia" o 1º clique (precisava clicar 2x). A tecla I não sofre
                        // disso porque roda em HandleGlobal, fora do ciclo de UI — este
                        // ScheduleOnMainThread reproduz esse timing.
                        MuGame.ScheduleOnMainThread(() =>
                        {
                            inv.Show();
                            Controllers.SoundController.Instance.PlayBuffer("Sound/iCreateWindow.wav");
                        });
                    }
                }
                return;
            }

            if (!_open)
                return;

            for (int i = 0; i < _entries.Length; i++)
            {
                if (_entryRects[i].Contains(p))
                {
                    Scene?.SetMouseInputConsumed();
                    _entries[i].OnTap?.Invoke();
                    return;
                }
            }

            // Toque fora fecha o fold-out (não consome — deixa o clique agir no mundo).
            _open = false;
            UpdateBounds();
        }

        public override void Draw(GameTime gameTime)
        {
            if (!Visible)
                return;
            var sb = GraphicsManager.Instance?.Sprite;
            var pixel = GraphicsManager.Instance?.Pixel;
            var font = GraphicsManager.Instance?.Font;
            if (sb == null || pixel == null)
                return;

            LayoutRects();

            // Escopo LinearClamp (mesmo padrão do arco/painel): o chrome oval reescalonado
            // no batch PointClamp da cena sai serrilhado.
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, null, null, null, Controllers.UiScaler.SpriteTransform);

            // Notification (sino): ícone bronze cru, sem aro.
            if (_texNotif != null && !_texNotif.IsDisposed)
                sb.Draw(_texNotif, _notifRect, Color.White);
            else
                DrawDisc(sb, pixel, new Vector2(_notifRect.X + GearSize / 2f, _notifRect.Y + GearSize / 2f), GearSize / 2f - 2, new Color(30, 26, 20));

            // Menu (grade 2x2): ícone bronze cru; gira um tico ao abrir o fold-out.
            if (_texGear != null && !_texGear.IsDisposed)
            {
                var c = new Vector2(_gearRect.X + _gearRect.Width / 2f, _gearRect.Y + _gearRect.Height / 2f);
                var origin = new Vector2(_texGear.Width / 2f, _texGear.Height / 2f);
                float scale = _gearRect.Width / (float)_texGear.Width;
                sb.Draw(_texGear, c, null, Color.White, _gearSpin, origin, scale, SpriteEffects.None, 0f);
            }
            else
            {
                DrawDisc(sb, pixel, new Vector2(_gearRect.X + GearSize / 2f, _gearRect.Y + GearSize / 2f), GearSize / 2f - 2, new Color(30, 26, 20));
            }

            // Bag (bolsa): ícone bronze cru; abre o inventário.
            if (_texSqBag != null && !_texSqBag.IsDisposed)
                sb.Draw(_texSqBag, _bagRect, Color.White);
            else
                DrawDisc(sb, pixel, new Vector2(_bagRect.X + GearSize / 2f, _bagRect.Y + GearSize / 2f), GearSize / 2f - 2, new Color(30, 26, 20));

            // Fold-out com fade+slide suave (sobe ~14px enquanto aparece).
            if (_foldAnim > 0.02f)
            {
                float fa = _foldAnim;
                int rise = (int)((1f - fa) * 14f);
                for (int i = 0; i < _entries.Length; i++)
                {
                    var rect = _entryRects[i];
                    rect.Y += rise;
                    var tex = _entryTex[i];
                    // A arte do potion-tab tem menos padding transparente que os demais, então
                    // aparece MAIOR no mesmo box. Encolhe ~14% só ela pra igualar o tamanho visual.
                    if (_entries[i].Label == "Potions")
                    {
                        int shrink = (int)(rect.Width * 0.14f);
                        rect = new Rectangle(rect.X + shrink / 2, rect.Y + shrink / 2, rect.Width - shrink, rect.Height - shrink);
                    }
                    if (tex != null && !tex.IsDisposed)
                    {
                        sb.Draw(tex, rect, Color.White * fa);
                    }
                    else
                    {
                        DrawDisc(sb, pixel, new Vector2(rect.X + rect.Width / 2f, rect.Y + rect.Height / 2f), rect.Width / 2f - 1, new Color(24, 22, 18) * (0.95f * fa));
                    }

                    // Legendas removidas ("vamos ver se fica bom sem elas" — padronização);
                    // os ícones do mobile são autoexplicativos.
                }
            }

            // Restaura o batch padrão da cena (PointClamp).
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, Controllers.UiScaler.SpriteTransform);

            base.Draw(gameTime);
        }

        private static void DrawDisc(SpriteBatch sb, Texture2D pixel, Vector2 c, float r, Color color)
        {
            int ir = (int)r;
            for (int y = -ir; y <= ir; y++)
            {
                float t = 1f - (y * y) / (r * r);
                if (t <= 0f) continue;
                float hw = r * MathF.Sqrt(t);
                sb.Draw(pixel, new Rectangle((int)(c.X - hw), (int)(c.Y + y), (int)(hw * 2), 1), color);
            }
        }

        private static void DrawOutlined(SpriteBatch sb, SpriteFont font, string text, Vector2 pos, float scale, Color color, int thickness)
        {
            // Contorno acompanha o alpha do texto (senão o outline "fantasma" fica na frente no fade).
            var outline = Color.Black * (color.A / 255f);
            for (int dx = -thickness; dx <= thickness; dx++)
                for (int dy = -thickness; dy <= thickness; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    sb.DrawString(font, text, pos + new Vector2(dx, dy), outline, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                }
            sb.DrawString(font, text, pos, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }
    }
}
