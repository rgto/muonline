namespace Client.Main.Controls.UI.SelectCharacter
{
    /// <summary>
    /// Layout do diálogo de exclusão de personagem (CharacterDeletionDialog), TODO editável
    /// pelo hud-edit-delete. Coordenadas em px, origem no canto SUP-ESQ do painel, Y pra baixo.
    /// Cole aqui o bloco gerado pelo editor.
    /// </summary>
    public static class DeleteLayout
    {
        public static int PanelW = 380;
        public static int PanelH = 250;

        // Título "DELETE CHARACTER" (centrado no X do painel)
        public static float TitleY = 16f;
        public static float TitleFont = 15f;

        // Mensagem (centrada, multi-linha)
        public static float MessageY = 50f;
        public static float MessageW = 340f;
        public static float MessageFont = 10f;

        // Rótulo "Security Code:"
        public static float CodeLabelX = 30f;
        public static float CodeLabelY = 150f;
        public static float CodeLabelFont = 11f;

        // Input do código
        public static float CodeInputX = 30f;
        public static float CodeInputY = 168f;
        public static float CodeInputW = 320f;
        public static float CodeInputH = 26f;
        public static float CodeInputFont = 11f;

        // Botão OK
        public static float OkX = 96f;
        public static float OkY = 200f;
        public static float OkW = 84f;
        public static float OkH = 30f;
        public static float OkFont = 11f;

        // Botão Cancel
        public static float CancelX = 200f;
        public static float CancelY = 200f;
        public static float CancelW = 84f;
        public static float CancelH = 30f;
        public static float CancelFont = 11f;
    }
}
