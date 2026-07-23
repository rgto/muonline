namespace Client.Main.Controls.UI
{
    /// <summary>
    /// Layout da MOLDURA 9-slice do PopupFieldDialog (cantos + bordas), TODO editável
    /// pelo hud-edit-frame. Cada peça tem offset DX/DY (px) e espessura/tamanho próprios.
    /// Padrão = comportamento original (offsets 0, cantos 16px, bordas 16px).
    ///
    /// DX/DY são somados à posição da peça:
    ///   +DX empurra pra DIREITA, +DY empurra pra BAIXO.
    /// Cole aqui o bloco gerado pelo editor.
    /// </summary>
    public static class FrameLayout
    {
        // Tamanho dos CANTOS (todos os 4 usam o mesmo W×H).
        public static int CornerW = 16;
        public static int CornerH = 16;

        // Espessura das BORDAS.
        public static int TopThick = 16;
        public static int BottomThick = 16;
        public static int LeftThick = 16;
        public static int RightThick = 16;

        // Ajuste no COMPRIMENTO das bordas (delta somado ao comprimento base = painel-2*canto).
        // As horizontais (Top/Bottom) usam LenX; as verticais (Left/Right) usam LenY.
        // Ex.: BottomLen = -1 encurta a borda inferior em 1px (corrige o vazamento na quina).
        public static int TopLen = 0;
        public static int BottomLen = -1;
        public static int LeftLen = 0;
        public static int RightLen = 0;

        // Offsets das BORDAS.
        public static int TopDX = 0, TopDY = 0;
        public static int BottomDX = 0, BottomDY = 0;
        public static int LeftDX = 0, LeftDY = 0;
        public static int RightDX = 0, RightDY = 0;

        // Offsets dos CANTOS (TL=sup-esq, TR=sup-dir, BL=inf-esq, BR=inf-dir).
        public static int TLDX = 0, TLDY = 0;
        public static int TRDX = 0, TRDY = 0;
        public static int BLDX = 0, BLDY = 0;
        public static int BRDX = -1, BRDY = 0;
    }
}
