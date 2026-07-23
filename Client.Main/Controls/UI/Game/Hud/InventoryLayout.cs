namespace Client.Main.Controls.UI.Game.Hud
{
    /// <summary>
    /// Layout da janela de INVENTÁRIO (estilo MU oficial mobile) — gerado/ajustado pelo
    /// editor hud-edit-inventory (:5188). Coordenadas locais da janela (origem no canto
    /// sup-esq do painel). Painel = panel-template (barra de título assada). Célula da
    /// grade = 34px (compartilhada com vault/trade).
    /// </summary>
    public static class InventoryLayout
    {
        public static int PanelW = 396;
        public static int PanelH = 716;
        public static int DragBarH = 82;

        public static bool PanelEnabled = true;
        public static float PanelX = 0f;
        public static float PanelY = 0f;

        public static bool TitleTextEnabled = true;
        public static float TitleTextX = 198f;
        public static float TitleTextY = 65f;
        public static float TitleTextFont = 14f;

        public static bool CloseEnabled = true;
        public static float CloseX = 332f;
        public static float CloseY = 50f;
        public static float CloseW = 28f;
        public static float CloseH = 28f;

        // 2 molduras LIVRES (inv_rect, 9-slice — cantos fixos) pra emoldurar áreas.
        public static bool Rect1Enabled = true;
        public static float Rect1X = 16f;
        public static float Rect1Y = 91f;
        public static float Rect1W = 364f;
        public static float Rect1H = 314f;

        public static bool Rect2Enabled = true;
        public static float Rect2X = 16f;
        public static float Rect2Y = 404f;
        public static float Rect2W = 364f;
        public static float Rect2H = 292f;

        public static bool CircleEnabled = true;
        public static float CircleX = 38f;
        public static float CircleY = 98f;
        public static float CircleW = 319f;
        public static float CircleH = 284f;

        public static bool PetEnabled = true;
        public static float PetX = 24f;
        public static float PetY = 99f;
        public static float PetW = 70f;
        public static float PetH = 70f;

        public static bool PendEnabled = true;
        public static float PendX = 104f;
        public static float PendY = 118f;
        public static float PendW = 50f;
        public static float PendH = 50f;

        public static bool HelmEnabled = true;
        public static float HelmX = 163f;
        public static float HelmY = 99f;
        public static float HelmW = 70f;
        public static float HelmH = 70f;

        public static bool WingsEnabled = true;
        public static float WingsX = 242f;
        public static float WingsY = 99f;
        public static float WingsW = 131f;
        public static float WingsH = 70f;

        public static bool WeaponEnabled = true;
        public static float WeaponX = 24f;
        public static float WeaponY = 170f;
        public static float WeaponW = 70f;
        public static float WeaponH = 95f;

        public static bool ArmorEnabled = true;
        public static float ArmorX = 163f;
        public static float ArmorY = 169f;
        public static float ArmorW = 70f;
        public static float ArmorH = 95f;

        public static bool ShieldEnabled = true;
        public static float ShieldX = 303f;
        public static float ShieldY = 170f;
        public static float ShieldW = 70f;
        public static float ShieldH = 95f;

        // Brincos (earrings): DECORATIVOS por enquanto — o servidor (OpenMU) só tem
        // equip 0..11; quando houver suporte, ganham slot id próprio.
        public static bool EarringLEnabled = true;
        public static float EarringLX = 104f;
        public static float EarringLY = 195f;
        public static float EarringLW = 50f;
        public static float EarringLH = 50f;

        public static bool EarringREnabled = true;
        public static float EarringRX = 244f;
        public static float EarringRY = 195f;
        public static float EarringRW = 50f;
        public static float EarringRH = 50f;

        public static bool Ring1Enabled = true;
        public static float Ring1X = 104f;
        public static float Ring1Y = 285f;
        public static float Ring1W = 50f;
        public static float Ring1H = 50f;

        public static bool Ring2Enabled = true;
        public static float Ring2X = 244f;
        public static float Ring2Y = 285f;
        public static float Ring2W = 50f;
        public static float Ring2H = 50f;

        public static bool GlovesEnabled = true;
        public static float GlovesX = 24f;
        public static float GlovesY = 265f;
        public static float GlovesW = 70f;
        public static float GlovesH = 70f;

        public static bool PantsEnabled = true;
        public static float PantsX = 163f;
        public static float PantsY = 266f;
        public static float PantsW = 70f;
        public static float PantsH = 70f;

        public static bool BootsEnabled = true;
        public static float BootsX = 303f;
        public static float BootsY = 266f;
        public static float BootsW = 70f;
        public static float BootsH = 70f;

        public static bool ArtifactEnabled = true;
        public static float ArtifactX = 22f;
        public static float ArtifactY = 332f;
        public static float ArtifactW = 74f;
        public static float ArtifactH = 74f;
        public static float ArtifactFont = 10f;

        public static bool StarBoxEnabled = true;
        public static float StarBoxX = 303f;
        public static float StarBoxY = 334f;
        public static float StarBoxW = 70f;
        public static float StarBoxH = 69f;

        public static bool GridEnabled = true;
        public static float GridX = 16f;
        public static float GridY = 408f;
        // ATENÇÃO: o protocolo do servidor endereça o inventário como 8 COLUNAS
        // (slot = 12 + linha*8 + coluna). Cols != 8 desalinha a posição visual dos
        // itens vs a validação do servidor; Rows só mostra os slots que o server tiver.
        // Design OFICIAL: 8 colunas x 7 linhas (56 slots). O protocolo endereça com
        // stride de 8 colunas; a "8ª linha" do server (slots 68..75) não é usada.
        public static int GridCols = 8;
        public static int GridRows = 7;

        // ── Janela EXPANDED (abre/fecha pelo botão "Expanded"): painel-template +
        //    moldura interna (inv_rect) + grade PREENCHIDA de slots (célula 34, visual
        //    por enquanto — os slots extras dependem de inventário estendido no server).
        //    Coordenadas relativas à origem do painel PRINCIPAL (X negativo = à esquerda).
        // Mesma ALTURA do inventário principal (716) — cabem 16 linhas.
        public static bool ExpPanelEnabled = true;
        public static float ExpPanelX = -320f;
        public static float ExpPanelY = 0f;
        public static float ExpPanelW = 310f;
        public static float ExpPanelH = 716f;

        public static bool ExpTitleEnabled = true;
        public static float ExpTitleTextX = -165f;   // centro do texto (sobre a barra assada)
        public static float ExpTitleTextY = 65f;
        public static float ExpTitleFont = 14f;
        public static string ExpTitleMessage = "Expanded";

        public static bool ExpCloseEnabled = true;
        public static float ExpCloseX = -68f;
        public static float ExpCloseY = 50f;
        public static float ExpCloseW = 28f;
        public static float ExpCloseH = 28f;

        public static bool ExpRectEnabled = true;
        public static float ExpRectX = -306f;
        public static float ExpRectY = 99f;
        public static float ExpRectW = 282f;
        public static float ExpRectH = 597f;

        // Grade da Expanded: ESTICA pra preencher exatamente a área W/H (sem lacunas) —
        // é visual, então a célula não precisa dos 34px do protocolo.
        public static bool ExpGridEnabled = true;
        public static float ExpGridX = -303f;
        public static float ExpGridY = 100f;
        public static float ExpGridW = 278f;
        public static float ExpGridH = 594f;
        public static int ExpGridCols = 8;
        public static int ExpGridRows = 18;

        public static bool SideBtnsEnabled = true;
        public static float SideBtnX = 294f;
        public static float SideBtnY = 408f;
        public static float SideBtnW = 82f;
        public static float SideBtnH = 33f;
        public static float SideBtnPitch = 42f;
        public static float SideBtnFont = 12f;

        public static bool MoneyEnabled = true;
        public static float CoinZenX = 202f; public static float CoinZenY = 660f; public static float CoinZenW = 26f; public static float CoinZenH = 26f;
        public static float FieldZenX = 57f; public static float FieldZenY = 659f; public static float FieldZenW = 143f; public static float FieldZenH = 30f;
        public static float CoinBundX = 32f; public static float CoinBundY = 660f; public static float CoinBundW = 26f; public static float CoinBundH = 26f;
        public static float FieldBundX = 227f; public static float FieldBundY = 659f; public static float FieldBundW = 143f; public static float FieldBundH = 30f;
        public static float MoneyFont = 12f;
    }
}
