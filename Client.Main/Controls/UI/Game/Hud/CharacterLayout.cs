namespace Client.Main.Controls.UI.Game.Hud
{
    /// <summary>
    /// Layout da tela de PERSONAGEM (tecla C — estilo MU oficial mobile) — ajustado pelo
    /// editor hud-edit-character (:5189). Painel = panel-template (barra de título assada,
    /// o NOME do personagem é desenhado sobre ela). Linhas de stats são data-driven; aqui
    /// ficam as colunas/pitches/fontes.
    /// </summary>
    public static class CharacterLayout
    {
        public static int PanelW = 346;
        public static int PanelH = 640;

        // Faixa de arraste (topo, barra de título assada).
        public static int DragBarH = 86;

        public static bool TitleTextEnabled = true;
        public static float TitleTextX = 173f;   // centro (nome do personagem)
        public static float TitleTextY = 58f;
        public static float TitleTextFont = 13f;

        public static bool CloseEnabled = true;
        public static float CloseX = 288f;
        public static float CloseY = 44f;
        public static float CloseW = 26f;
        public static float CloseH = 26f;

        // Abas: Normal (ativa) + Element (desabilitada) — placas grandes; 3rd/4th/5th pequenas.
        public static bool TabsEnabled = true;
        public static float TabNormalX = 22f;
        public static float TabNormalY = 99f;
        public static float TabNormalW = 86f;
        public static float TabNormalH = 30f;
        public static float TabPitch = 90f;      // Element = Normal.X + Pitch
        public static float TabFont = 11f;

        public static float TabSmallX = 206f;
        public static float TabSmallY = 94f;
        public static float TabSmallW = 38f;
        public static float TabSmallH = 26f;
        public static float TabSmallPitch = 42f; // 3rd, 4th, 5th
        public static float TabSmallFont = 10f;

        // Card 1: Class / Level / Guild / Server.
        public static bool Card1Enabled = true;
        public static float Card1X = 18f;
        public static float Card1Y = 128f;
        public static float Card1W = 310f;
        public static float Card1H = 104f;
        public static float InfoRowY = 138f;     // 1ª linha
        public static float InfoRowPitch = 24f;
        public static float InfoLabelX = 32f;
        public static float InfoValueX = 140f;
        public static float InfoFont = 11f;

        // Card 2: Pts Remaining / Fruit Create / Fruit Decrease (+ % à direita).
        public static bool Card2Enabled = true;
        public static float Card2X = 18f;
        public static float Card2Y = 236f;
        public static float Card2W = 310f;
        public static float Card2H = 82f;
        public static float PtsRowY = 246f;
        public static float PtsRowPitch = 24f;
        public static float PctX = 316f;         // borda direita dos percentuais (right-align)

        // Card 3: lista de stats (linhas data-driven).
        public static bool Card3Enabled = true;
        public static float Card3X = 18f;
        public static float Card3Y = 322f;
        public static float Card3W = 310f;
        public static float Card3H = 300f;
        public static float StatRowY = 330f;
        public static float StatRowPitch = 17.5f;
        public static float StatLabelX = 30f;
        public static float StatValueX = 150f;
        public static float StatSignX = 306f;    // centro do sinal +/-
        public static float StatHeaderFont = 11f;
        public static float StatSubFont = 10f;
    }
}
