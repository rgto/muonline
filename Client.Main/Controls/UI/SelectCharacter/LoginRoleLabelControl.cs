#nullable enable
using System;
using System.Threading.Tasks;
using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Core.Utilities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MUnique.OpenMU.Network.Packets;

namespace Client.Main.Controls.UI.SelectCharacter
{
    /// <summary>
    /// Rótulo flutuante de um personagem na tela de seleção — o nó RoleInfo do prefab
    /// login_loginroleui, projetado sobre a cabeça do modelo 3D.
    ///
    /// Fiel à Lua (NameOnRefresh): duas linhas — nome e "Classe Nível" — e o botão Del
    /// à direita, que a Lua liga SÓ no personagem selecionado (ShowDeleteBtn). A posição
    /// vem de WorldToScreenPoint + RoleHeight; aqui quem projeta é o SelectWorld e
    /// escreve em SetAnchor().
    ///
    ///   RoleInfo            size=(185,57)
    ///     lab_Name          pos=(0,6)     size=(100,30)
    ///     lab_CareerAndLevel pos=(0,-18)  size=(100,30)
    ///     btn_Delete        pos=(97.8,0)  size=(38,49)   INACTIVE por padrão
    /// </summary>
    public sealed class LoginRoleLabelControl : UIControl
    {

        // Mesma escala prefab->virtual da LoginRoleControl.
        private const float S = 720f / 750f;

        private const float InfoW = 185f, InfoH = 57f;
        private const float NameY = 6f;
        private const float CareerY = -18f;

        private static readonly Color NameColor = new(255, 255, 255);
        // Verde do "Dark Knight 53" da referência.
        private static readonly Color CareerColor = new(126, 211, 33);


        /// <summary>Centro do rótulo na UI virtual (o SelectWorld projeta e escreve aqui).</summary>
        private Vector2 _anchor;

        public string CharacterName { get; set; } = string.Empty;
        public CharacterClassNumber CharacterClass { get; set; }
        public ushort Level { get; set; }

        /// <summary>Selecionado: só então o Del aparece (regra da Lua ShowDeleteBtn).</summary>
        public bool IsSelected { get; set; }


        public LoginRoleLabelControl()
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
            await base.Load();
        }

        /// <summary>Reposiciona o rótulo; (cx,cy) = centro do RoleInfo na UI virtual.</summary>
        public void SetAnchor(Vector2 center)
        {
            _anchor = center;
        }

        /// <summary>Rect w×h centrado em (x,y) do prefab, relativo ao rótulo.</summary>
        private Rectangle Rect(float x, float y, float w, float h) => new(
            (int)MathF.Round(_anchor.X + (x - w / 2f) * S),
            (int)MathF.Round(_anchor.Y - (y + h / 2f) * S),
            (int)MathF.Round(w * S),
            (int)MathF.Round(h * S));

        /// <summary>
        /// O Del saiu do rótulo: apagar agora é o botão de LIXEIRA da tela (LoginRoleControl).
        /// O ícone já diz o que faz — não precisa do texto "Del" em cima do personagem.
        /// </summary>
        public bool HitsDelete(Point p) => false;

        public override void Draw(GameTime gameTime)
        {
            if (!Visible) return;

            var sb = GraphicsManager.Instance.Sprite;
            var font = GraphicsManager.Instance.Font;
            if (sb == null || font == null) return;

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                     null, null, null, UiScaler.SpriteTransform);

            DrawCentered(font, CharacterName, NameY, NameColor, 0.62f);
            // "Dark Knight 53" — o mesmo string.format("%s %d") da Lua.
            string career = $"{CharacterClassDatabase.GetClassName(CharacterClass)} {Level}";
            DrawCentered(font, career, CareerY, CareerColor, 0.52f);


            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                     null, null, null, UiScaler.SpriteTransform);

            base.Draw(gameTime);
        }

        private void DrawCentered(SpriteFont font, string text, float prefabY, Color color, float scale)
        {
            if (string.IsNullOrEmpty(text)) return;

            var sb = GraphicsManager.Instance.Sprite;
            Vector2 size = font.MeasureString(text) * scale;
            var c = new Vector2(_anchor.X, _anchor.Y - prefabY * S);
            var pos = new Vector2(c.X - size.X / 2f, c.Y - size.Y / 2f);

            // Contorno preto 1px: o fundo é a cena 3D, texto puro some.
            sb.DrawString(font, text, pos + new Vector2(1, 1), Color.Black * 0.85f,
                          0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            sb.DrawString(font, text, pos, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }
    }
}
