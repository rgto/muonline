using Microsoft.Xna.Framework;

namespace Client.Main.Controls.UI
{
    /// <summary>
    /// Botão "OK" padrão do jogo. Reusa a MESMA arte/renderização do OkCancelButton
    /// (Interface/CharCreate/ok_cancel.OZT, default+hover) — fonte única de verdade, pra
    /// todo "OK" da UI (MessageWindow, diálogos) ficar uniforme. Antes usava a arte antiga
    /// message_ok_b_all.tga (SpriteControl 54x30 TileY).
    /// </summary>
    public class OkButton : OkCancelButton
    {
        public OkButton()
        {
            Text = "OK";
            ViewSize = new Point(84, 34);   // tamanho padrão do OK nas janelas
            FontScale = 11f / 24f;
        }
    }
}
