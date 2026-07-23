#nullable enable
using System;
using System.Threading.Tasks;
using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Helpers;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Controls.UI
{
    /// <summary>
    /// Botão "Login with Google" — arte no estilo MU (moldura ornamentada), texto embutido.
    /// google_login_normal.OZT / google_login_hover.OZT (200x63, OZT guarda dims exatas).
    /// Só há uma arte: normal e hover apontam pra mesma; o hover é um clareamento no código.
    /// </summary>
    public sealed class GoogleLoginButton : UIControl
    {
        private const string NormalPath = "Interface/CharCreate/google_login_normal.OZT";
        private const string HoverPath = "Interface/CharCreate/google_login_hover.OZT";

        // OZT guarda nx×ny exatos (sem padding pra potência de 2): recorta a arte inteira.
        private static readonly Rectangle Src = new(0, 0, 200, 63);
        private static readonly Color HoverTint = new(255, 245, 220);   // clareamento leve no hover
        private static readonly Color Pressed = new(200, 200, 200);

        private Texture2D? _texNormal, _texHover;
        private bool _pressed;

        public event EventHandler? Click2;

        public GoogleLoginButton()
        {
            Interactive = true;
            AutoViewSize = false;
            ViewSize = new Point(200, 50);
        }

        public override async Task Load()
        {
            _texNormal = await TextureLoader.Instance.PrepareAndGetTexture(NormalPath);
            _texHover = await TextureLoader.Instance.PrepareAndGetTexture(HoverPath);
            await base.Load();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            if (!Visible) return;
            _pressed = IsMouseOver && IsMousePressed;
        }

        public override void Draw(GameTime gameTime)
        {
            if (Status != GameControlStatus.Ready || !Visible) return;

            var tex = (IsMouseOver ? _texHover : _texNormal) ?? _texNormal;
            if (tex == null) return;

            // Arte única: hover = clareamento leve (mesma arte), pressed = escurecer.
            var tint = _pressed ? Pressed : (IsMouseOver ? HoverTint : Color.White);

            using (new SpriteBatchScope(
                GraphicsManager.Instance.Sprite,
                SpriteSortMode.Deferred,
                BlendState.AlphaBlend,
                transform: UiScaler.SpriteTransform))
            {
                GraphicsManager.Instance.Sprite.Draw(
                    tex, DisplayRectangle, Src, tint);
            }

            base.Draw(gameTime);
        }
    }
}
