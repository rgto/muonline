using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Client.Main.Controls.UI
{
    public abstract class PopupFieldDialog : DialogControl, IUiTexturePreloadable
    {
        private Texture2D _cornerTopLeftTexture;
        private Texture2D _topLineTexture;
        private Texture2D _cornerTopRightTexture;
        private Texture2D _leftLineTexture;
        private Texture2D _backgroundTexture;
        private Texture2D _rightLineTexture;
        private Texture2D _cornerBottomLeftTexture;
        private Texture2D _bottomLineTexture;
        private Texture2D _cornerBottomRightTexture;
        private bool _useFallbackFrame;
        private readonly ILogger _logger = MuGame.AppLoggerFactory?.CreateLogger<PopupFieldDialog>();
        private static readonly string[] s_popupTextureSuffixes =
        {
            "01","02","03","04","05","06","07","08","09"
        };

        public override async Task Load()
        {
            await base.Load();

            var windowName = "popupfield";
            
            _cornerTopLeftTexture = await TextureLoader.Instance.PrepareAndGetTexture($"Interface/GFx/{windowName}01.ozd");
            _topLineTexture = await TextureLoader.Instance.PrepareAndGetTexture($"Interface/GFx/{windowName}02.ozd");
            _cornerTopRightTexture = await TextureLoader.Instance.PrepareAndGetTexture($"Interface/GFx/{windowName}03.ozd");
            _leftLineTexture = await TextureLoader.Instance.PrepareAndGetTexture($"Interface/GFx/{windowName}04.ozd");
            _backgroundTexture = await TextureLoader.Instance.PrepareAndGetTexture($"Interface/GFx/{windowName}05.ozd");
            _rightLineTexture = await TextureLoader.Instance.PrepareAndGetTexture($"Interface/GFx/{windowName}06.ozd");
            _cornerBottomLeftTexture = await TextureLoader.Instance.PrepareAndGetTexture($"Interface/GFx/{windowName}07.ozd");
            _bottomLineTexture = await TextureLoader.Instance.PrepareAndGetTexture($"Interface/GFx/{windowName}08.ozd");
            _cornerBottomRightTexture = await TextureLoader.Instance.PrepareAndGetTexture($"Interface/GFx/{windowName}09.ozd");

            _useFallbackFrame = _cornerTopLeftTexture == null || _topLineTexture == null ||
                                _cornerTopRightTexture == null || _leftLineTexture == null ||
                                _backgroundTexture == null || _rightLineTexture == null ||
                                _cornerBottomLeftTexture == null || _bottomLineTexture == null ||
                                _cornerBottomRightTexture == null;
            if (_useFallbackFrame)
            {
                _logger?.LogWarning("PopupFieldDialog frame textures missing. Using fallback flat background.");
            }
        }

        public IEnumerable<string> GetPreloadTexturePaths()
        {
            const string basePath = "Interface/GFx/popupfield";
            for (int i = 0; i < s_popupTextureSuffixes.Length; i++)
            {
                yield return basePath + s_popupTextureSuffixes[i] + ".ozd";
            }
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
        }

        public override void Draw(GameTime gameTime)
        {
            if (Status != GameControlStatus.Ready || !Visible)
                return;

            var sprite = GraphicsManager.Instance.Sprite;
            var rect = DisplayRectangle;

            if (_useFallbackFrame)
            {
                var bgColor = new Color(0, 0, 0, 200);
                sprite.Draw(GraphicsManager.Instance.Pixel, rect, bgColor);

                var borderColor = new Color(255, 255, 255, 160);
                sprite.Draw(GraphicsManager.Instance.Pixel, new Rectangle(rect.X, rect.Y, rect.Width, 2), borderColor);
                sprite.Draw(GraphicsManager.Instance.Pixel, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2), borderColor);
                sprite.Draw(GraphicsManager.Instance.Pixel, new Rectangle(rect.X, rect.Y, 2, rect.Height), borderColor);
                sprite.Draw(GraphicsManager.Instance.Pixel, new Rectangle(rect.Right - 2, rect.Y, 2, rect.Height), borderColor);
            }
            else
            {
                // Espessuras base das peças do frame (16px cada no popupfield0x).
                int cw = _cornerTopLeftTexture.Width, ch = _cornerTopLeftTexture.Height;
                int F = FrameLayout.CornerW, Fh = FrameLayout.CornerH; // tamanho editável dos cantos

                // Fundo (recuado pela espessura da moldura).
                sprite.Draw(_backgroundTexture,
                            new Rectangle(
                                rect.X + _leftLineTexture.Width,
                                rect.Y + _topLineTexture.Height,
                                rect.Width - _leftLineTexture.Width - _rightLineTexture.Width,
                                rect.Height - _topLineTexture.Height - _bottomLineTexture.Height),
                            Color.White);

                // Arestas — offset (DX/DY), espessura e COMPRIMENTO (…Len) editáveis.
                sprite.Draw(_topLineTexture, new Rectangle(
                    rect.X + cw + FrameLayout.TopDX, rect.Y + FrameLayout.TopDY,
                    rect.Width - cw * 2 + FrameLayout.TopLen, FrameLayout.TopThick), Color.White);
                sprite.Draw(_bottomLineTexture, new Rectangle(
                    rect.X + cw + FrameLayout.BottomDX, rect.Bottom - FrameLayout.BottomThick + FrameLayout.BottomDY,
                    rect.Width - cw * 2 + FrameLayout.BottomLen, FrameLayout.BottomThick), Color.White);
                sprite.Draw(_leftLineTexture, new Rectangle(
                    rect.X + FrameLayout.LeftDX, rect.Y + ch + FrameLayout.LeftDY,
                    FrameLayout.LeftThick, rect.Height - ch * 2 + FrameLayout.LeftLen), Color.White);
                sprite.Draw(_rightLineTexture, new Rectangle(
                    rect.Right - FrameLayout.RightThick + FrameLayout.RightDX, rect.Y + ch + FrameLayout.RightDY,
                    FrameLayout.RightThick, rect.Height - ch * 2 + FrameLayout.RightLen), Color.White);

                // Cantos — cada um com offset (DX/DY) e tamanho (F×Fh) editáveis.
                sprite.Draw(_cornerTopLeftTexture, new Rectangle(
                    rect.X + FrameLayout.TLDX, rect.Y + FrameLayout.TLDY, F, Fh), Color.White);
                sprite.Draw(_cornerTopRightTexture, new Rectangle(
                    rect.Right - F + FrameLayout.TRDX, rect.Y + FrameLayout.TRDY, F, Fh), Color.White);
                sprite.Draw(_cornerBottomLeftTexture, new Rectangle(
                    rect.X + FrameLayout.BLDX, rect.Bottom - Fh + FrameLayout.BLDY, F, Fh), Color.White);
                sprite.Draw(_cornerBottomRightTexture, new Rectangle(
                    rect.Right - F + FrameLayout.BRDX, rect.Bottom - Fh + FrameLayout.BRDY, F, Fh), Color.White);
            }

            base.Draw(gameTime);
        }
    }
}
