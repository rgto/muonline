namespace Client.Main.Controls.UI.Game.Hud
{
    /// <summary>
    /// Layout da janela de NPC SHOP (estilo MU oficial mobile) — ajustado pelo editor
    /// hud-edit-npcshop (:5190). Painel = panel-template (barra assada; título por cima).
    /// </summary>
    public static class NpcShopLayout
    {
        public static int PanelW = 316;
        public static int PanelH = 716;

        public static int DragBarH = 78;

        public static bool TitleTextEnabled = true;
        public static float TitleTextX = 158f;   // centro
        public static float TitleTextY = 65f;
        public static float TitleTextFont = 13f;
        public static string TitleMessage = "Merchant";

        public static bool CloseEnabled = true;
        public static float CloseX = 260f;
        public static float CloseY = 52f;
        public static float CloseW = 26f;
        public static float CloseH = 26f;

        // Aba de página "1" (decorativa por enquanto — loja tem 1 página).
        public static bool PageTabEnabled = true;
        public static float PageTabX = 28f;
        public static float PageTabY = 113f;
        public static float PageTabW = 40f;
        public static float PageTabH = 26f;
        public static float PageTabFont = 11f;

        // Card (dark card + moldura) que envolve a grade.
        public static bool CardEnabled = true;
        public static float CardX = 16f;
        public static float CardY = 139f;
        public static float CardW = 283f;
        public static float CardH = 439f;

        // Grade da loja, célula 32 padrão (mesma arte inv_grid_row dos outros grids).
        // 8 colunas = stride do protocolo (slot = y*8 + x); cols≠8 desalinha itens vs servidor.
        public static float GridX = 30f;
        public static float GridY = 150f;
        public static int GridCols = 8;
        public static int GridRows = 13;

        // Linha de texto do rodapé (hint/taxa — conteúdo dinâmico do modo).
        public static bool FooterTextEnabled = false;
        public static float FooterTextY = 640f;
        public static float FooterTextFont = 11f;

        // Botão inferior ("Cancel Item Sale" — fecha a loja).
        // Tamanho EFETIVO do Save do Skill Imprint (104x41 @ escala 1.2 = ~125x49, fonte 12*1.2).
        public static bool BottomBtnEnabled = true;
        public static float BottomBtnX = 48f;
        public static float BottomBtnY = 640f;
        public static float BottomBtnW = 220f;
        public static float BottomBtnH = 49f;
        public static float BottomBtnFont = 14.4f;
        public static string BottomBtnLabel = "Cancel Item Sale";

        // Botões de conserto (só em loja de repair): lado a lado acima do rodapé.
        public static float RepairBtnX = 18f;
        public static float RepairBtnY = 588f;
        public static float RepairBtnW = 136f;
        public static float RepairBtnH = 44f;
        public static float RepairBtnGap = 8f;
        public static float RepairBtnFont = 13f;
    }
}
