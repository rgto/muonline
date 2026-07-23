#nullable enable
using System;
using System.Threading.Tasks;
using Client.Main.Content;
using Client.Main.Controllers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Client.Main.Controls.UI.SelectCharacter
{
    /// <summary>
    /// Tela de SELEÇÃO de personagem — recriação fiel do prefab mobile
    /// login_loginroleui (design 1334x750), com a arte real do Atlas_Login
    /// (Interface/LoginRole, OZP premultiplicado).
    ///
    /// Fiel à Lua Login_LoginRoleUI: os personagens são modelos 3D na CENA (não cards
    /// de UI); esta classe desenha só o chrome — a placa "Create Character" pendurada
    /// nas correntes, o botão vermelho de entrar e o botão voltar. O rótulo
    /// nome/classe+nível flutua sobre a cabeça de cada personagem e o Del aparece
    /// só no selecionado (LoginRoleLabelControl).
    /// </summary>
    public sealed class LoginRoleControl : UIControl
    {
        private const string TexDir = "Interface/LoginRole/";

        // Prefab 1334x750 -> UI virtual 1280x720. Mesma proporção (16:9): uma escala
        // única mapeia os dois eixos sem deformar (1334*0.96 = 1280.6, 0.6px de sobra).
        private const float S = 720f / 750f;
        private const float PrefabW = 1334f;
        private const float PrefabH = 750f;

        // Widgets do prefab (dump_prefab_layout.py). Cada rect confere com o m_Rect do
        // sprite que o consome — tudo em tamanho NATIVO, nada esticado.
        //   btn_CreateRole  pos=(0,-185) size=(227,60)  anchor=(0.5,1|0.5,1)  [topo]
        //     BtnText         pos=(0,-1.8)   size=(166,41)
        //     Img_connect_1   pos=(111,123)  size=(158,196)
        //     Img_connect_2   pos=(-111,123) size=(158,196) scale=-1 (espelhado)
        //   btn_Connect     pos=(0,58)   size=(193,50)  anchor=(0.5,0|0.5,0)  [base]
        //   btn_Back        pos=(42,-52) size=(50,50)   anchor=(0,1|0,1)      [sup-esq]
        // Botão "Start Game": posicionado no EDITOR (hud-edit-slots). Era ancorado no
        // rodapé (ConnectBottomY); agora é ponto absoluto do prefab, como o resto.
        // Start Game alinhado aos "Vacant Character Slot": MESMA linha do último slot
        // (SlotTopY 272.3 - SlotPitch 60 * 9 = -267.7). A arte start_game tem ~9% de padding
        // transparente (conteúdo 124 de 136px), então ConnectH compensa (58.3/0.91≈64) pra a
        // MOLDURA VISÍVEL ficar da mesma altura do slot, sem o gap.
        private const float ConnectCX = -88.9f, ConnectCY = -267.7f;
        private const float ConnectW = 237.7f, ConnectH = 74f;

        // Botões VOLTAR e APAGAR: arte nova (ícone, sem texto), posicionados no EDITOR.
        // O antigo btn_back do canto sup-esq sai; estes assumem a função e mudam de lugar.
        private const float BackCX = -608.8f, BackCY = 272.2f;
        private const float BackW = 57.5f, BackH = 58.3f;

        // A lixeira ACOMPANHA o slot selecionado (apaga o char dele). DeleteCX/W/H são
        // fixos; o Y vem da lista (SetDeleteY), pra ela ficar sempre do lado do selecionado.
        private const float DeleteCX = 609.5f;
        private const float DeleteW = 57.5f, DeleteH = 58.3f;
        private float _deleteCY = 272.1f;

        /// <summary>Põe a lixeira na altura do slot selecionado (px do prefab).</summary>
        public void SetDeleteY(float prefabY)
        {
            _deleteCY = prefabY;
            _deleteRect = R(DeleteCX, _deleteCY, DeleteW, DeleteH);
        }

        // ── Recortes: o OZT é POTÊNCIA DE 2 e o conteúdo fica no canto ──────────────
        // start_game.OZT NOVO: arte 471x136 SEM padding (textura = tamanho da arte).
        // btn_back_new:   arte  93x81 numa textura 128x128
        // btn_delete_new: arte  92x81 numa textura 128x128
        // Sem source rect o Draw manda a textura INTEIRA (arte + padding vazio) pro rect,
        // e a arte sai ~18% menor e desalinhada. Foi exatamente o bug do Start Game.
        // start_game.OZT v3 (2026-07): placa nova 471x146 com o texto dourado ASSADO
        // (mesmo texto original, já na escala 84% aprovada). Sem rótulo em runtime.
        private static readonly Rectangle StartGameSrc = new(0, 0, 471, 146);
        // Artes novas (imagem única, sem hover): back 249x213, delete 246x213.
        private static readonly Rectangle BackSrc = new(0, 0, 249, 213);
        private static readonly Rectangle DeleteSrc = new(0, 0, 246, 213);

        private Texture2D? _texStartGame, _texBack, _texDelete;

        private Rectangle _connectRect, _backRect, _deleteRect;
        private bool _pressedConnect, _pressedBack, _pressedDelete;

        /// <summary>Apagar o personagem selecionado. Só aparece com um selecionado.</summary>
        public event EventHandler? DeleteClicked;

        /// <summary>Um personagem está selecionado e não está em exclusão.</summary>
        public bool CanEnter { get; set; }

        // Conta cheia NÃO tem knob aqui de propósito: na Lua (Button_CreateRoleOnClick)
        // a placa continua na tela e o aviso "roles cheios" sai só NO CLIQUE — quem
        // decide isso é a cena, não este controle.

        public event EventHandler? EnterClicked;
        public event EventHandler? BackClicked;

        public LoginRoleControl()
        {
            Interactive = true;
            AutoViewSize = false;
            ControlSize = new Point(Constants.BASE_UI_WIDTH, Constants.BASE_UI_HEIGHT);
            ViewSize = ControlSize;
            X = 0;
            Y = 0;
        }

        public override async Task Load()
        {
            _texStartGame = await Load("Interface/CharCreate/start_game.OZT");
            _texBack = await Load("Interface/CharCreate/btn_back_new.OZT");
            _texDelete = await Load("Interface/CharCreate/btn_delete_new.OZT");
            LayoutRects();
            await base.Load();

            static Task<Texture2D?> Load(string path) =>
                TextureLoader.Instance.PrepareAndGetTexture(path)!;
        }

        // ----- conversão prefab -> tela --------------------------------------------

        /// <summary>
        /// Ponto do prefab (origem no CENTRO da tela, Y pra CIMA) -> UI virtual.
        /// </summary>
        private static Vector2 P(float x, float y) => new(
            Constants.BASE_UI_WIDTH / 2f + x * S,
            Constants.BASE_UI_HEIGHT / 2f - y * S);

        /// <summary>Rect w×h centrado em (x,y) do prefab.</summary>
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
            _connectRect = R(ConnectCX, ConnectCY, ConnectW, ConnectH);

            _backRect = R(BackCX, BackCY, BackW, BackH);
            _deleteRect = R(DeleteCX, _deleteCY, DeleteW, DeleteH);
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
                _pressedConnect = ShowConnect && _connectRect.Contains(p);
                _pressedBack = _backRect.Contains(p);
                _pressedDelete = ShowDelete && _deleteRect.Contains(p);
            }
            else if (!down && wasDown)
            {
                if (_pressedConnect && ShowConnect && _connectRect.Contains(p))
                    EnterClicked?.Invoke(this, EventArgs.Empty);
                else if (_pressedBack && _backRect.Contains(p))
                    BackClicked?.Invoke(this, EventArgs.Empty);
                else if (_pressedDelete && ShowDelete && _deleteRect.Contains(p))
                    DeleteClicked?.Invoke(this, EventArgs.Empty);

                _pressedConnect = _pressedBack = _pressedDelete = false;
            }
        }

        // O botão vermelho some enquanto não há seleção válida (ShowBtn_Connect).
        // A placa "Create Character" foi REMOVIDA: criar personagem agora é pelo slot
        // "Vacant Character Slot" da lista.
        private bool ShowConnect => CanEnter;
        private bool ShowDelete => CanEnter;

        public override void Draw(GameTime gameTime)
        {
            if (!Visible) return;

            var sb = GraphicsManager.Instance.Sprite;
            if (sb == null) return;

            // Escopo LinearClamp próprio: PointClamp + upscale serrilha a arte.
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                     null, null, null, UiScaler.SpriteTransform);


            if (ShowConnect)
            {
                // Botão VERMELHO "Start Game": entra no jogo com o selecionado.
                // Na Lua desta tela o btn_Connect NÃO tem SetSprite — o sprite é fixo no
                // prefab, só liga/desliga (ShowBtn_Connect). Quem troca entre
                // btn_startGame/btn_create é o Button_Ok da tela de CRIAÇÃO, outro botão.
                Draw(_texStartGame, _connectRect, StartGameSrc,
                     tint: _pressedConnect ? Pressed : Color.White);

            }

            Draw(_texBack, _backRect, BackSrc, tint: _pressedBack ? Pressed : Color.White);

            // Apagar: sem personagem selecionado não há o que apagar.
            if (ShowDelete)
                Draw(_texDelete, _deleteRect, DeleteSrc, tint: _pressedDelete ? Pressed : Color.White);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                     null, null, null, UiScaler.SpriteTransform);

            base.Draw(gameTime);
        }

        private static readonly Color Pressed = new(180, 180, 180);

        /// <param name="src">
        /// Recorte da arte DENTRO da textura. Obrigatório pros OZT: a textura vem
        /// arredondada pra potência de 2 e o resto é padding vazio — passar null aqui
        /// desenha o vazio junto e encolhe a arte.
        /// </param>
        private static void Draw(Texture2D? tex, Rectangle rect, Rectangle? src = null,
                                 SpriteEffects fx = SpriteEffects.None, Color? tint = null)
        {
            if (tex == null) return;
            GraphicsManager.Instance.Sprite.Draw(
                tex, rect, src, tint ?? Color.White, 0f, Vector2.Zero, fx, 0f);
        }
    }
}
