namespace Client.Main.Controls.UI.Game.Hud
{
    /// <summary>
    /// Layout da janela do BAÚ / Storage (estilo MU oficial mobile) — ajustado pelo editor
    /// hud-edit-vault (:5191). Painel = panel-template (barra assada; título por cima).
    /// Dark card e Moldura são peças INDEPENDENTES (posição/tamanho próprios).
    /// </summary>
    public static class VaultLayout
    {
        public static int PanelW = 440;
        public static int PanelH = 716;

        public static int DragBarH = 78;

        public static bool TitleTextEnabled = true;
        public static float TitleTextX = 210f;   // centro
        public static float TitleTextY = 65f;
        public static float TitleTextFont = 13f;
        public static string TitleMessage = "Storage (open)";

        public static bool CloseEnabled = true;
        public static float CloseX = 353f;
        public static float CloseY = 51f;
        public static float CloseW = 26f;
        public static float CloseH = 26f;

        // Dark card (fundo escuro) — peça INDEPENDENTE da moldura.
        public static bool DarkCardEnabled = true;
        public static float DarkCardX = 92f;
        public static float DarkCardY = 151f;
        public static float DarkCardW = 254f;
        public static float DarkCardH = 355f;

        // Moldura vazada (moldura2) — peça INDEPENDENTE do dark card.
        public static bool CardEnabled = true;
        public static float CardX = 62f;
        public static float CardY = 136f;
        public static float CardW = 313f;
        public static float CardH = 387f;

        // Grade do baú, célula 32 (mesma arte inv_grid_row dos outros grids).
        // 8 colunas = stride do protocolo (slot = y*8 + x).
        // 8 colunas = RowSize do servidor (InventoryConstants.RowSize) — NUNCA mudar,
        // é o stride do protocolo (slot = linha*8 + coluna).
        public static float GridX = 90f;
        public static float GridY = 153f;
        public static int GridCols = 8;
        // Linhas VISÍVEIS por página. O baú tem 15 linhas no servidor
        // (WarehouseRows=15, WarehouseSize=120); o resto vai para as abas.
        public static int GridRows = 11;

        // Abas de página (como nas lojas de NPC): dividem as 15 linhas do baú.
        public static bool PageTabsEnabled = true;
        public static float PageTabX = 90f;
        public static float PageTabY = 114f;
        public static float PageTabW = 40f;
        public static float PageTabH = 24f;
        public static float PageTabGap = 8f;
        public static float PageTabFont = 11f;

        // Linha "Zen" (rótulo + campo de valor).
        public static bool ZenRowEnabled = true;
        public static float ZenLabelX = 90f;
        public static float ZenRowY = 565f;      // centro vertical da linha
        public static float ZenFieldX = 186f;
        public static float ZenFieldW = 159f;
        public static float ZenFieldH = 26f;
        public static float ZenFont = 11f;

        // Linha "Storage fee" (rótulo + campo).
        public static bool FeeRowEnabled = true;
        public static float FeeLabelX = 89f;
        public static float FeeRowY = 599f;
        public static float FeeFieldX = 185f;
        public static float FeeFieldW = 159f;
        public static float FeeFieldH = 26f;

        // Fileira de 4 botões (Deposit / Withdraw / Lock / Expanded), arte padrão do Save.
        public static bool ButtonsEnabled = true;
        public static float BtnRowX = 32f;
        public static float BtnRowY = 640f;
        public static float BtnW = 92f;
        public static float BtnH = 36f;
        public static float BtnGap = 3f;
        public static float BtnFont = 11f;
        public static string Btn1Label = "Deposit";
        public static string Btn2Label = "Withdraw";
        public static string Btn3Label = "Lock";
        public static string Btn4Label = "Expanded";
    }
}
