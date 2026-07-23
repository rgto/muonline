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
    /// Botão OK/Cancel com a MESMA arte da tela de criação de personagem
    /// (Interface/CharCreate/ok_cancel.OZT). A folha traz só duas molduras VAZIAS
    /// (default à esquerda, hover à direita); o rótulo é desenhado por cima.
    /// Usado no LoginDialog para OK (loga) e Cancel (volta).
    /// </summary>
    public class OkCancelButton : UIControl
    {
        private const string TexPath = "Interface/CharCreate/ok_cancel.OZT";

        // Folha ok_cancel-3 (467x80): 2 CORES lado a lado — Save/OK (verde, esq 0..232) e
        // Cancel (vermelho, dir 233..466). O botão escolhe a metade por IsCancel. O hover NÃO
        // é uma arte separada: é um leve CLAREAMENTO aplicado via código.
        private static readonly Rectangle SaveSrc = new(0, 0, 233, 80);
        private static readonly Rectangle CancelSrc = new(233, 0, 234, 80);

        private static readonly Color TextNormal = new(220, 225, 229);
        private static readonly Color HoverTint = new(255, 245, 220);   // clareamento leve no hover
        private static readonly Color Pressed = new(180, 180, 180);

        private Texture2D? _tex;
        private bool _pressed;

        /// <summary>Cancel = metade vermelha (direita). Save/OK = metade verde (esquerda).</summary>
        public bool IsCancel { get; set; }

        /// <summary>Texto centrado no botão (ex.: "OK", "Cancel").</summary>
        public string Text { get; set; } = "OK";

        /// <summary>Escala da fonte do rótulo (mesmo divisor 24 da tela de criação).</summary>
        public float FontScale { get; set; } = 12f / 24f;

        public OkCancelButton()
        {
            Interactive = true;
            AutoViewSize = false;
            ViewSize = new Point(104, 41);
        }

        public override async Task Load()
        {
            _tex = await TextureLoader.Instance.PrepareAndGetTexture(TexPath);
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
            if (Status != GameControlStatus.Ready || !Visible || _tex == null)
                return;

            var rect = DisplayRectangle;
            bool hover = IsMouseOver || _pressed;

            using (new SpriteBatchScope(
                GraphicsManager.Instance.Sprite,
                SpriteSortMode.Deferred,
                BlendState.AlphaBlend,
                transform: UiScaler.SpriteTransform))
            {
                var sb = GraphicsManager.Instance.Sprite;
                // Metade da folha conforme o tipo do botão; hover = clareamento leve (tint).
                var src = IsCancel ? CancelSrc : SaveSrc;
                var tint = _pressed ? Pressed : (hover ? HoverTint : Color.White);
                sb.Draw(_tex, rect, src, tint);

                var font = GraphicsManager.Instance.Font;
                if (font != null && !string.IsNullOrEmpty(Text))
                {
                    Vector2 size = font.MeasureString(Text) * FontScale;
                    var pos = new Vector2(
                        rect.X + (rect.Width - size.X) / 2f,
                        rect.Y + (rect.Height - size.Y) / 2f);

                    // Outline do prefab: preto 50%, offset (1,-1).
                    sb.DrawString(font, Text, pos + new Vector2(1, -1), Color.Black * 0.5f,
                                  0f, Vector2.Zero, FontScale, SpriteEffects.None, 0f);
                    sb.DrawString(font, Text, pos, TextNormal,
                                  0f, Vector2.Zero, FontScale, SpriteEffects.None, 0f);
                }
            }

            base.Draw(gameTime);
        }
    }
}
