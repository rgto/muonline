namespace Client.Main.Controls.UI.Login
{
    /// <summary>
    /// Layout do formulário de login (LoginDialog), TODO editável pelo hud-edit-login.
    /// Coordenadas em px, origem no canto SUPERIOR-ESQUERDO do painel, Y pra BAIXO
    /// (mesmo sistema dos controles filhos: X/Y são offset dentro de ControlSize).
    ///
    /// Cole aqui o bloco gerado pelo editor (hud-edit-login) para aplicar os ajustes finos.
    /// </summary>
    public static class LoginLayout
    {
        // ── Painel ───────────────────────────────────────────────
        public static int PanelW = 300;
        public static int PanelH = 270;

        // ── Título "MU Online" (centrado no X do painel) ─────────
        public static float TitleY = 15f;
        public static float TitleFont = 12f;

        // ── Linha de acabamento de cima ──────────────────────────
        public static float Line1X = 10f;
        public static float Line1Y = 40f;
        public static float Line1H = 8f;

        // ── Server label (centrado no X) ─────────────────────────
        public static float ServerY = 55f;
        public static float ServerFont = 12f;

        // ── Rótulo "Account" (User) ──────────────────────────────
        public static float UserLabelX = 20f;
        public static float UserLabelY = 91f;
        public static float UserLabelW = 70f;
        public static float UserLabelFont = 12f;

        // ── Rótulo "Password" ────────────────────────────────────
        public static float PassLabelX = 20f;
        public static float PassLabelY = 126f;
        public static float PassLabelW = 70f;
        public static float PassLabelFont = 12f;

        // ── Linha de acabamento de baixo ─────────────────────────
        public static float Line2X = 10f;
        public static float Line2Y = 152f;
        public static float Line2H = 5f;

        // ── Input de usuário ─────────────────────────────────────
        public static float UserInputX = 100f;
        public static float UserInputY = 88f;
        public static float UserInputW = 176f;
        public static float UserInputH = 24f;
        public static float UserInputFont = 11f;

        // ── Input de senha ───────────────────────────────────────
        public static float PassInputX = 100f;
        public static float PassInputY = 122f;
        public static float PassInputW = 176f;
        public static float PassInputH = 24f;
        public static float PassInputFont = 11f;

        // ── Botão OK ─────────────────────────────────────────────
        public static float OkX = 71f;
        public static float OkY = 162f;
        public static float OkW = 74f;
        public static float OkH = 40f;
        public static float OkFont = 12f;

        // ── Botão Cancel ─────────────────────────────────────────
        public static float CancelX = 155f;
        public static float CancelY = 162f;
        public static float CancelW = 74f;
        public static float CancelH = 40f;
        public static float CancelFont = 12f;

        // ── Botão "Login with Google" (arte pronta, sem texto próprio) ──
        // Arte 1175x369 (razão ~3.18:1): GoogleH acompanha a largura pra não achatar a moldura.
        public static float GoogleX = 70f;
        public static float GoogleY = 208f;
        public static float GoogleW = 160f;
        public static float GoogleH = 50f;
    }
}
