namespace Client.Main.Controls.UI.Game.Hud
{
    /// <summary>
    /// Layout da janela de MASTERY (árvore de master skills) — ajustado pelo editor
    /// hud-edit-mastery (:5193). Coordenadas no ESPAÇO DA ARTE (mastery_bg = 1024x1023);
    /// o controle escala tudo pra caber na tela (PanelMaxH), estilo SkillImprint.
    /// Valores = export LITERAL do editor (não ajustar à mão; usar o editor).
    /// </summary>
    public static class MasteryLayout
    {
        // ── Painel ───────────────────────────────────────────────────────
        public static float ArtW = 1024f;
        public static float ArtH = 1023f;
        // (Sem cap de altura: como TODAS as janelas, o painel ocupa 100% da altura
        // virtual e o UiScaler escala pra tela física — padrão Imprint.)

        // ── Barra de título (vermelha) ───────────────────────────────────
        // Nome da classe centrado no trecho ESQUERDO da barra.
        public static float ClassNameCX = 255.56f;
        public static float TitleTextY = 73.39f;
        public static float TitleFont = 15f;
        // 3 campos à direita (Level / Points / EXP): centro X de cada um.
        public static float Field1CX = 560f;
        public static float Field2CX = 705f;
        public static float Field3CX = 855f;
        public static float FieldTextY = 74.78f;
        public static float FieldFont = 12f;
        // Altura da faixa de ARRASTE no topo (barra de título; janela é arrastável).
        public static float DragBarH = 100f;
        // Botão fechar (X) no canto da barra.
        public static float CloseX = 957.83f;
        public static float CloseY = 57.61f;
        public static float CloseSize = 30f;

        // ── Colunas (3 árvores) ──────────────────────────────────────────
        public static float Col1X = 33f;
        public static float Col2X = 353f;
        public static float Col3X = 688f;
        public static float ColW = 305f;
        // Faixa do header colorido (título da coluna centrado nela).
        public static float ColHeaderY = 132f;
        public static float ColHeaderFont = 13f;
        // Área útil dos nós dentro da coluna.
        public static float ColTop = 160f;
        public static float ColBottom = 990f;

        // Títulos das colunas (o banco só tem "Left/Middle/Right"; renomeáveis no editor).
        public static string ColTitleLeft = "Common Skills";
        public static string ColTitleMiddle = "Specialty I";
        public static string ColTitleRight = "Specialty II";

        // ── Nós (soquete circular + ícone + contagem) ────────────────────
        public static int SubCols = 5;          // sub-colunas por árvore (rank 2 da comum tem 5 skills)
        public static float NodeSize = 52f;     // diâmetro do soquete
        public static float RowH = 92f;         // passo vertical por rank
        public static float FirstRowCY = 210f;  // centro Y do rank 1
        public static float SubColPad = 10f;    // folga lateral dentro da coluna
        public static float CountFont = 11f;    // fonte do "0/20" ao lado do nó
        public static float CountDX = 2f;       // offset do texto a partir da borda do nó
        public static float CountDY = 16f;
        // Linhas de conexão (pré-requisito).
        public static float LinkWidth = 2f;

        // ── Tooltip ──────────────────────────────────────────────────────
        public static float TooltipFont = 11f;
    }
}
