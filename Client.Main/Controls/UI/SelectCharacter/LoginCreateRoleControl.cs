#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
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
    /// Tela de CRIAÇÃO de personagem — recriação fiel do prefab mobile
    /// login_logincreateroleui (design 1334x750), com a arte real do Atlas_Login.
    ///
    /// Layout (dump_prefab_layout.py), origem no CENTRO da tela e Y pra CIMA:
    ///   grid_att         pos=(362.3,59)   size=(386,476)   [painel STR/AGI/CON/INT]
    ///     img_Att        pos=(-91,174)    size=(125,25)    célula 100x32, spacing y=2
    ///   Scroll_Career    pos=(450,81)     size=(200,350)   célula 170x45, spacing y=6
    ///   createBg         pos=(11,-197)    size=(816,156)   [caixa da descrição]
    ///   InputField_Name  pos=(-108,-173)  size=(500,51)
    ///   Button_Ok        pos=(290,-173)   size=(193,50)
    ///   Button_Cancel    pos=(42,-52)     size=(50,50)     anchor sup-esq
    ///
    /// Todo dado (nome, descrição, stats) vem do Data via CharacterCreateDatabase —
    /// nada de tabela hardcoded.
    /// </summary>
    public sealed class LoginCreateRoleControl : UIControl
    {
        private const string TexDir = "Interface/LoginRole/";

        /// <summary>
        /// Arte da referência do MU oficial (as peças que o usuário recortou à mão do GFx —
        /// ver PROJECT-CONTEXT §9.0). Empacotada como OZT nativo no Data.zip.
        /// </summary>
        private const string ArtDir = "Interface/CharCreate/";

        // ── Recortes DENTRO de cada arte ──────────────────────────────────────────
        // Todo OZT é potência de 2 e o conteúdo fica no canto (o resto é padding vazio),
        // então TODA peça é desenhada por source rect. Ver DrawPiece.

        /// <summary>class_btn.OZT: 3 estados + o glow. Conteúdo 709x30 (textura 1024x32).</summary>
        private const int ClassBtnH = 30;

        // ── Botão de classe ───────────────────────────────────────────────────────
        // Posicionado no EDITOR (hud-edit/index.html), não derivado da moldura: as
        // constantes do prefab mobile (CareerRight etc.) descrevem a arte ANTIGA, e ancorar
        // nelas jogava a coluna pra fora da moldura nova.
        private const float ClassBtnDrawW = 177.8f;
        private const float ClassBtnDrawH = 39.4f;

        /// <summary>Centro X da coluna de botões.</summary>
        private const float ClassBtnCx = 373.8f;

        /// <summary>Centro Y do PRIMEIRO botão (os outros descem pelo pitch).</summary>
        private const float ClassBtnTopY = 302.5f;

        /// <summary>
        /// Passo vertical entre botões (centro a centro). É o espaçamento: igual à altura =
        /// colados; maior = folga entre eles.
        /// </summary>
        private const float ClassBtnPitch = 39.4f;

        /// <summary>Altura do glow, relativa à do botão. É um risco de luz fino, não uma barra.</summary>
        private const float GlowHeightFactor = 0.85f;

        // ── Nome da classe (o texto em cima do botão) ─────────────────────────────
        // Ajustado no EDITOR. DX/DY deslocam o texto dentro do botão (a arte não é
        // simétrica); Scale é o tamanho da fonte (1 = tamanho base do cliente).
        private const float ClassTextDX = 0f;
        private const float ClassTextDY = 0f;
        private const float ClassTextScale = 14f / 24f;

        /// <summary>
        /// Os 3 estados do botão, MEDIDOS no alpha da folha (não dá pra dividir por 3: as
        /// peças têm folgas irregulares entre si — 15, 14 e 36px).
        /// O 4º vermelho (claro demais pro texto) foi REMOVIDO da arte pelo usuário.
        /// </summary>
        private static readonly Rectangle[] ClassBtnSrc =
        {
            new(0, 0, 152, ClassBtnH),     // 0 = default (escuro)   x=0..151
            new(167, 0, 152, ClassBtnH),   // 1 = hover              x=167..318
            new(333, 0, 152, ClassBtnH),   // 2 = SELECIONADO        x=333..484
        };
        /// <summary>O raio amarelo horizontal da classe selecionada. x=521..680.</summary>
        private static readonly Rectangle ClassGlowSrc = new(521, 0, 160, ClassBtnH);

        /// <summary>ok_cancel.OZT nova (149x32): default (esq) 0..73, hover (dir) 74..148.</summary>
        private static readonly Rectangle OkBtnSrc = new(0, 0, 74, 32);
        private static readonly Rectangle OkBtnHoverSrc = new(74, 0, 75, 32);

        /// <summary>panel_frame.OZT: moldura da lista + painel de atributos. 344x358 (tex 512x512).</summary>
        private static readonly Rectangle PanelFrameSrc = new(0, 0, 344, 358);

        /// <summary>name_desc.OZT: plaquinha "Name" + caixa da prosa + assassino. 581x366 (tex 1024x512).</summary>
        private static readonly Rectangle NameDescSrc = new(0, 0, 581, 366);

        /// <summary>name_input.OZT: o campo do nickname. 220x58 (tex 256x64).</summary>
        private static readonly Rectangle NameInputSrc = new(0, 0, 220, 58);

        /// <summary>
        /// name_plate.OZT: a plaquinha ornamentada onde vai escrito "Name". 123x74
        /// (tex 128x128). O miolo é vazio — o texto é desenhado por cima; o vão útil NÃO é
        /// o centro da arte (ver NamePlateTextDX/DY).
        /// </summary>
        private static readonly Rectangle NamePlateSrc = new(0, 0, 123, 74);

        /// <summary>
        /// spider_newfaceNN.OZT: o gráfico radar de atributos, UM POR CLASSE (a arte já vem
        /// com o polígono desenhado). 512x512, sem padding.
        /// </summary>
        private static readonly Rectangle SpiderSrc = new(0, 0, 512, 512);

        /// <summary>
        /// dark_label.OZT: painel escuro translúcido (preto a 40%), 220x46 (tex 256x64).
        /// É uma cor CHAPADA — estica pra qualquer tamanho sem deformar, e por isso a mesma
        /// arte serve as duas instâncias (atrás da descrição e atrás dos atributos).
        /// </summary>
        private static readonly Rectangle DarkLabelSrc = new(0, 0, 220, 46);

        private const float S = 720f / 750f;
        private const float PrefabW = 1334f, PrefabH = 750f;

        /// <summary>
        /// Sobe TODO o conteúdo da tela (painel de atributos, lista de classes e o bloco de
        /// baixo) — só o botão voltar fica onde está, ancorado no canto.
        /// O prefab é do mobile (750 de altura útil numa tela mais alta em proporção); aqui
        /// o conteúdo inteiro assentava baixo demais. Y do prefab cresce pra CIMA, então
        /// subir é somar. Medido no mock do usuário: ~55px do prefab.
        /// </summary>
        private const float ContentDY = 55f;

        // grid_att (painel de atributos) — a arte CreateRoleBg é 386x476 NATIVA.
        // Painel de atributos + moldura: posicionado no EDITOR (hud-edit).
        private const float AttX = 298.5f, AttY = 120.7f, AttW = 392.7f, AttH = 488.2f;
        // img_Att = a CÉLULA do UIContainer (125x25), 1ª linha em (-91,174) do grid_att.
        // A grade empilha com cell.y=32 + spacing.y=2 (GridLayoutGroup_#52).
        private const float AttCellX = -91f, AttCellTopY = 174f;
        private const float AttCellW = 125f, AttCellH = 25f;
        private const float AttRowPitch = 34f;   // 32 + 2
        // O RÓTULO (lab_attTip) tem sizeDelta (0,0) no prefab: quem dá tamanho a ele é o
        // ContentSizeFitter em runtime, então o prefab NÃO diz onde o texto acaba — não
        // dá pra derivar a linha dele. O que o prefab garante é o passo: o VALOR
        // (lab_attCount) fica +81px em X do rótulo. Ancoramos os dois na célula: rótulo
        // right-aligned na metade esquerda, valor right-aligned +81 depois.
        // AttRowShiftX desloca a linha inteira (rótulo + valor); AttValueDX é o passo do
        // rótulo até o valor. Os dois andam juntos: empurrar a linha pra dentro (shift)
        // sem encurtar o passo joga o VALOR em cima da moldura do outro lado — medido na
        // tela, com shift 14 e passo 81 os valores caíam em x 943..957 virtuais, colados
        // na coluna de classes (que começa em ~950).
        // shift 14 afasta o rótulo da borda esquerda; passo 62 traz o valor de volta pra
        // dentro, mantendo a linha inteira folgada nos DOIS lados.
        private const float AttValueDX = 62f;
        private const float AttLabelW = 40f;
        private const float AttValueW = 44f;
        private const float AttRowShiftX = 14f;

        // Scroll_Career: lista de classes.
        // Largura da célula: com FontPanel o pior nome ("Magic Gladiator") mede ~136px, e
        // 170 (útil ~154 descontando o bisel da arte) veste o texto sem sobra de espaço
        // vazio. A célula 210 de antes foi dimensionada pra uma fonte maior e ainda passava
        // da moldura (terminava em 564 contra 555.3 do createrole_bg) — era a borda de
        // larguras diferentes.
        // A borda DIREITA fica fixa na moldura (555.3) e a célula encolhe pela esquerda,
        // que é o lado livre. TODAS as células usam a mesma largura: mexer só na que vaza
        // quebraria a uniformidade da família.
        private const float CareerCellW = 170f, CareerCellH = 45f, CareerSpacingY = 6f;
        // A moldura do createrole_bg termina em 555.3 (AttX + AttW/2), mas essa é a borda
        // EXTERNA da arte: o bisel decorativo ocupa os últimos px, então encostar a célula
        // ali deixava os botões passando por cima dele. -12 recua a coluna pra dentro do
        // vão útil da moldura.
        private const float CareerRight = 555.3f - 12f;
        private const float CareerX = CareerRight - CareerCellW / 2f;  // centro da célula
        private const float CareerY = 81f + ContentDY;
        private const float CareerViewW = CareerCellW, CareerViewH = 350f;
        private const float CareerPadL = 0f, CareerPadT = 3f;

        // createBg + filhos.
        /// <summary>
        /// Quanto o bloco inteiro de baixo (caixa + nome + botão + descrição) desce, em px
        /// do prefab. O prefab é do mobile, cuja tela é mais alta em proporção; aqui a caixa
        /// subia demais e cobria o PEITO dos bustos. Desce o BLOCO TODO de uma vez pra não
        /// desalinhar nome/botão/prosa entre si.
        /// Y do prefab cresce pra CIMA, então descer é subtrair. -26 mal se notou (o peito
        /// seguia escondido); -70 põe a caixa abaixo da linha do peito, com o busto inteiro
        /// visível como na referência. O bloco tem 156 de altura e o prefab vai até -375,
        /// então o limite antes de vazar pela borda de baixo é ~-100.
        /// </summary>
        private const float BottomBlockDY = -71f;

        // DescH 156 = tamanho NATIVO da arte (imgCreateBg). Não dá pra esticar: os
        // ornamentos dourados dos cantos e o degradê vertical são fixos (não é 9-slice),
        // então qualquer altura diferente deforma a moldura. Quem se ajusta é o texto —
        // ver DescScale(), que encolhe a prosa até o pior caso caber.
        // A arte oficial (name_desc, 581x334) é mais alta em proporção que a caixa do mobile
        // (816x156): esticada pra 156 ela achatava (plaquinha "Name", moldura e assassino
        // esmagados).
        // A LARGURA é a do prefab e NÃO se mexe nela: 816 é o que casa com a tela e com o
        // texto da prosa (encolher a largura foi um erro — espremeu o bloco de lado).
        // Só a ALTURA cresce, pra peça respirar. A proporção pura daria 816*334/581 = 469,
        // que cobriria o busto; DescH é o knob visual entre 156 (achatado) e 469 (proporção
        // exata). 260 = a caixa da referência, alta o bastante pro assassino e pra prosa.
        private const float DescArtW = 581f, DescArtH = 366f;

        // Bloco nome + descrição: posicionado no EDITOR (hud-edit).
        // A arte cresceu 334 -> 366 (+9.58%) só pra baixo, pra cobrir a faixa que sobrava.
        // A altura DESENHADA acompanha na mesma razão (senão a peça deforma) e o centro desce
        // METADE do ganho — assim o TOPO fica exatamente onde estava (27.1 no prefab).
        private const float DescX = 18.8f, DescY = -181.2f, DescW = 643.7f, DescH = 416.5f;
        // jobIntroduction: anchor em STRETCH com sizeDelta (-60,-85) => tamanho =
        // 756x71; pos (0,-42.5) desce o centro. Em coords do prefab isso dá um bloco
        // centrado em (11,-239.5) que ocupa y -204..-275 — ou seja, a faixa de BAIXO do
        // createBg, abaixo do campo de nome (base -198.5). O nome/botão ficam na faixa
        // de cima da MESMA caixa; por isso a prosa não pode começar no topo dela.
        // DescTextH 64 (o prefab traz 71): o vão entre a base do campo de nome e o fundo da
        // caixa tem 76px reais, e 71 encostava nos dois lados. 64 deixa folga; a prosa que
        // não couber é encolhida pelo DescScale.
        // Bloco da PROSA (descrição da classe): posicionado no EDITOR, como as outras peças.
        // Era derivado da caixa (DescY/DescH), mas o texto tem alinhamento próprio e precisa
        // ser ajustado à mão — daí ser um retângulo independente.
        private const float DescTextCX = -37.9f, DescTextCY = -204.4f;
        private const float DescTextW = 316.9f, DescTextH = 203f;

        // Campo do NOME: posicionado no EDITOR.
        // NameX/W = a área CLICÁVEL (abre o teclado); NameText* = onde o texto digitado é
        // escrito. São separados porque a plaquinha "Name" da arte ocupa a esquerda do campo.
        private const float NameX = -108f, NameY = -190f, NameW = 500f, NameH = 51f;

        // A ARTE do campo (name_input): vai POR CIMA do name_desc e o texto digitado vai por
        // cima dela. Retângulo próprio, ajustado no EDITOR.
        private const float NameInputCX = -37.7f, NameInputCY = -66.6f;
        private const float NameInputW = 300f, NameInputH = 24.7f;

        // Plaquinha "Name": vai POR CIMA da plaquinha que já existe na arte do name_desc.
        // O texto é centrado nela (horizontal e vertical). Ajustada no EDITOR.
        // Gráfico radar (spider): posicionado no EDITOR.
        private const float SpiderCX = 210.7f, SpiderCY = 107.8f;
        private const float SpiderW = 137.8f, SpiderH = 132.2f;

        private const float NamePlateCX = -235f, NamePlateCY = -59.9f;
        private const float NamePlateW = 144.1f, NamePlateH = 85.1f;
        private const float FontNamePlate = 11f / 24f;

        /// <summary>
        /// Desloca o "Name" dentro da plaquinha — a arte NÃO é simétrica em nenhum dos eixos,
        /// então centrar no retângulo dela não centra no VÃO onde o texto cabe. Medido na
        /// própria arte (123x74) e convertido pra escala em que ela é desenhada (144.1x85.1):
        ///   X: o miolo está 4.5px à ESQUERDA do centro  -> -5.3
        ///   Y: o vão real (y=32..54) está 6.5px ABAIXO do centro -> +7.5
        ///      (a espada/ornamento ocupa o topo da plaquinha, então o vão útil fica baixo)
        /// Knobs de ajuste fino: +X vai pra direita, +Y desce.
        /// </summary>
        private const float NamePlateTextDX = -5.3f;
        private const float NamePlateTextDY = 7.5f;

        // ── Painéis escuros (dark_label) ──────────────────────────────────────────
        // A MESMA arte em dois lugares, cada um com seu retângulo: um atrás da prosa da
        // classe, outro atrás das linhas de atributo. Ambos ajustados no EDITOR.
        private const float DarkDescCX = 13.2f, DarkDescCY = -226.6f;
        private const float DarkDescW = 467f, DarkDescH = 293f;

        private const float DarkAttCX = 212.2f, DarkAttCY = 134f;
        private const float DarkAttW = 135.5f, DarkAttH = 371.1f;
        private const float NameTextCX = -58.8f, NameTextCY = -66.6f, NameTextW = 193.8f, NameTextH = 25.4f;

        // OK: posicionado no EDITOR (hud-edit).
        private const float OkX = 224.4f, OkY = -66.2f, OkW = 104.4f, OkH = 41.1f;

        // CANCEL: mesma arte do OK (a folha só tem default/hover, molduras vazias — o rótulo
        // é desenhado por cima). Posição vem do EDITOR, igual ao resto.
        private const float CancelBtnX = 224.3f, CancelBtnY = -112f, CancelBtnW = 104.4f, CancelBtnH = 41.1f;

        // Cores exatas dos Text do prefab.
        private static readonly Color TextNormal = new(220, 225, 229);   // 0.863,0.882,0.898
        private static readonly Color TextValue = new(255, 138, 0);      // 1,0.541,0
        private static readonly Color TextPlaceholder = new(153, 153, 153); // 0.6
        private static readonly Color Pressed = new(180, 180, 180);

        // FontSize do prefab / BASE_FONT_SIZE do cliente.
        // Classe e atributo são a MESMA família de texto do painel: mesma escala nos dois,
        // sempre. (Tamanhos mistos lado a lado leem como amadorismo.)
        private const float FontPanel = 14f / 24f;
        private const float FontClass = FontPanel;
        private const float FontStat = FontPanel;
        private const float FontDesc = 17f / 24f;
        private const float FontName = 16f / 24f;

        private Texture2D? _texPanelFrame, _texClassBtn, _texNameDesc, _texOkCancel,
                           _texNameInput, _texDarkLabel, _texNamePlate;

        private Rectangle _attRect, _careerViewRect, _descRect, _nameRect, _okRect, _cancelRect;
        private readonly List<Rectangle> _careerRects = new();
        private Rectangle _nameTextRect, _nameInputRect, _darkDescRect, _darkAttRect, _namePlateRect;
        private Rectangle _spiderRect;

        /// <summary>
        /// Um gráfico por classe, indexado pelo modelo do Data ("newface02"). Carregados
        /// uma vez no Load; o Draw só escolhe o da classe selecionada.
        /// </summary>
        private readonly Dictionary<string, Texture2D?> _texSpider = new();

        private bool _pressedOk, _pressedCancel;
        private int _pressedCareer = -1;
        /// <summary>Escala global da prosa (0 = ainda não medida). Ver DescScale().</summary>
        private float _descScale;

        /// <summary>
        /// Entrada de texto do nome: só captura teclado/foco/cursor. Fica VISÍVEL (o
        /// Update do GameControl não roda em controle invisível e o teclado morreria),
        /// mas não pinta nada — todas as cores são transparentes. Quem desenha a moldura
        /// é a arte (name_input) e o texto somos nós, na escala do prefab (FontName).
        ///
        /// É um TextFieldControl.Create() e NÃO um TextBoxControl: o Create() devolve a
        /// implementação DA PLATAFORMA (no Android, o AndroidTextFieldControl, que abre o
        /// teclado do sistema no OnFocus). O TextBoxControl só escuta eventos de teclado
        /// físico — no celular o campo ganhava foco e nada acontecia. É o mesmo caminho que
        /// a tela de login já usa nos campos de usuário/senha.
        /// </summary>
        private readonly TextFieldControl _nameBox = CreateNameBox();

        private static TextFieldControl CreateNameBox()
        {
            var box = TextFieldControl.Create();
            box.Skin = TextFieldSkin.Flat;        // sem moldura: quem desenha é a arte
            box.TextColor = Color.Transparent;    // o texto é desenhado por nós, na escala do prefab
            // Nome de personagem: só letras e números, sem espaço nem quebra de linha, até
            // 10 chars. O teclado do Android devolve o texto inteiro (sem passar por tecla),
            // então o filtro tem que estar no Value — daí o Sanitizer.
            box.Sanitizer = SanitizeName;
            return box;
        }

        private static string SanitizeName(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;
            var sb = new System.Text.StringBuilder(raw.Length);
            foreach (var c in raw)
            {
                if (sb.Length >= 10) break;
                if (char.IsLetterOrDigit(c)) sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>Classes oferecidas: as do Data que o servidor sabe criar.</summary>
        private readonly List<(CharacterCreateClass Info, CharacterClassNumber Class)> _classes = new();

        private int _selected;

        /// <summary>Nome digitado.</summary>
        public string CharacterName
        {
            get => _nameBox.Value;
            set => _nameBox.Value = value;
        }

        /// <summary>Conta sem nenhum personagem: o botão vira "Start Game".</summary>
        public bool AccountIsEmpty { get; set; }

        public CharacterClassNumber SelectedClass =>
            _classes.Count > 0 ? _classes[_selected].Class : CharacterClassNumber.DarkWizard;

        /// <summary>Classe selecionada (pra cena posar o modelo 3D do rosto).</summary>
        public CharacterCreateClass? SelectedInfo =>
            _classes.Count > 0 ? _classes[_selected].Info : null;

        public event EventHandler? OkClicked;
        public event EventHandler? CancelClicked;
        public event EventHandler? NameFieldClicked;
        /// <summary>Trocou de classe: a cena troca o modelo 3D.</summary>
        public event EventHandler? SelectionChanged;

        public LoginCreateRoleControl()
        {
            Interactive = true;
            AutoViewSize = false;
            ControlSize = new Point(Constants.BASE_UI_WIDTH, Constants.BASE_UI_HEIGHT);
            ViewSize = ControlSize;
            X = 0;
            Y = 0;

            Controls.Add(_nameBox);
        }

        public override async Task Load()
        {
            // Arte da referência oficial (Data/Interface/CharCreate). Uma folha por peça,
            // desenhada por recorte — ver DrawPiece e os *Src acima.
            _texPanelFrame = await L(ArtDir + "panel_frame.OZT");
            _texClassBtn = await L(ArtDir + "class_btn.OZT");
            _texNameDesc = await L(ArtDir + "name_desc.OZT");
            _texNameInput = await L(ArtDir + "name_input.OZT");
            _texDarkLabel = await L(ArtDir + "dark_label.OZT");
            _texNamePlate = await L(ArtDir + "name_plate.OZT");

            _texOkCancel = await L(ArtDir + "ok_cancel.OZT");

            // Só aqui: no ctor o Data ainda pode não ter carregado e o texto sairia como
            // a CHAVE crua ("qingshurunicheng") em vez da frase.
            _nameBox.Placeholder = CharacterCreateDatabase.GetWord("qingshurunicheng");

            BuildClassList();

            // DEPOIS do BuildClassList: é ele que preenche _classes. Um radar por classe —
            // a arte já traz o polígono pronto, então só carregamos a textura de cada uma.
            // Itera sobre uma CÓPIA (_classes.ToList()): o loop tem um await no meio, e se
            // BuildClassList rodar de novo nesse intervalo (Clear/Add) a enumeração da lista
            // viva estoura "Collection was modified" — travava a tela de criação após deletar.
            foreach (var (info, _) in _classes.ToList())
                if (!string.IsNullOrEmpty(info.Model) && !_texSpider.ContainsKey(info.Model))
                    _texSpider[info.Model] = await L(ArtDir + $"spider_{info.Model}.OZT");
            LayoutRects();
            await base.Load();

            static Task<Texture2D?> L(string p) => TextureLoader.Instance.PrepareAndGetTexture(p)!;
        }

        /// <summary>
        /// Só entram as classes que o Data descreve E o servidor sabe criar. O config do
        /// mobile traz Grow Lancer (id 17), que o OpenMU não tem no enum — renderizá-la
        /// daria um botão que falha no clique. Rage Fighter é o inverso (o servidor tem,
        /// o Data não): no mobile ela é "special career" e vem do CareerUnlockInfo, que
        /// está VAZIO neste Data.
        /// </summary>
        private void BuildClassList()
        {
            _classes.Clear();
            foreach (var info in CharacterCreateDatabase.Classes)
            {
                var cls = MapToServerClass(info.Id);
                if (cls.HasValue)
                    _classes.Add((info, cls.Value));
            }
        }

        /// <summary>Id do cfg_Character_create -> classe do OpenMU. null = não criável.</summary>
        private static CharacterClassNumber? MapToServerClass(int configId) => configId switch
        {
            11 => CharacterClassNumber.DarkKnight,
            12 => CharacterClassNumber.DarkWizard,
            13 => CharacterClassNumber.FairyElf,
            14 => CharacterClassNumber.MagicGladiator,
            15 => CharacterClassNumber.DarkLord,
            16 => CharacterClassNumber.Summoner,
            _ => null,   // 17 = Grow Lancer: sem equivalente no OpenMU
        };

        // ----- conversão prefab -> tela --------------------------------------------

        private static Vector2 P(float x, float y) => new(
            Constants.BASE_UI_WIDTH / 2f + x * S,
            Constants.BASE_UI_HEIGHT / 2f - y * S);

        private static Rectangle R(float x, float y, float w, float h)
        {
            var c = P(x, y);
            return new Rectangle(
                (int)MathF.Round(c.X - w * S / 2f),
                (int)MathF.Round(c.Y - h * S / 2f),
                (int)MathF.Round(w * S),
                (int)MathF.Round(h * S));
        }

        private void LayoutRects()
        {
            _attRect = R(AttX, AttY, AttW, AttH);
            _careerViewRect = R(CareerX, CareerY, CareerViewW, CareerViewH);
            _descRect = R(DescX, DescY, DescW, DescH);
            // Área clicável do nome = a ARTE do campo. NameX/W (do prefab mobile) descreviam
            // outro campo, bem maior, e engoliam cliques fora da caixa que se vê na tela.
            _nameRect = R(NameInputCX, NameInputCY, NameInputW, NameInputH);
            _okRect = R(OkX, OkY, OkW, OkH);
            _cancelRect = R(CancelBtnX, CancelBtnY, CancelBtnW, CancelBtnH);

            // Área útil do texto do nome: sizeDelta (-135) tira 67.5 de cada lado, mas o
            // Text é ancorado com pos.x=22.5 => começa depois da plaquinha "Name".
            _nameTextRect = R(NameTextCX, NameTextCY, NameTextW, NameTextH);
            _nameInputRect = R(NameInputCX, NameInputCY, NameInputW, NameInputH);
            _darkDescRect = R(DarkDescCX, DarkDescCY, DarkDescW, DarkDescH);
            _darkAttRect = R(DarkAttCX, DarkAttCY, DarkAttW, DarkAttH);
            _namePlateRect = R(NamePlateCX, NamePlateCY, NamePlateW, NamePlateH);
            _spiderRect = R(SpiderCX, SpiderCY, SpiderW, SpiderH);
            // A caixa de entrada cobre a ARTE do campo (name_input), não só a área do texto:
            // é ela quem recebe o clique (TextBoxControl.OnClick dá o foco e abre o teclado),
            // então precisa ocupar todo o retângulo clicável que o usuário enxerga.
            _nameBox.X = _nameInputRect.X;
            _nameBox.Y = _nameInputRect.Y;
            _nameBox.ViewSize = new Point(_nameInputRect.Width, _nameInputRect.Height);
            _nameBox.ControlSize = _nameBox.ViewSize;

            // Lista de classes: GridLayoutGroup vertical, célula 170x45 + spacing 6,
            // ancorada no TOPO-ESQUERDA do viewport (pivot 0,1) com padding L4 T3.
            // Coluna de classes: tudo do editor — centro X fixo, 1º botão em ClassBtnTopY e
            // os demais descendo de ClassBtnPitch.
            _careerRects.Clear();
            for (int i = 0; i < _classes.Count; i++)
            {
                float cy = ClassBtnTopY - ClassBtnPitch * i;
                _careerRects.Add(R(ClassBtnCx, cy, ClassBtnDrawW, ClassBtnDrawH));
            }
        }

        protected override void OnScreenSizeChanged()
        {
            base.OnScreenSizeChanged();
            LayoutRects();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            if (!Visible) return;

            var mouse = MuGame.Instance.Mouse;
            var prev = MuGame.Instance.PrevMouseState;
            var p = MuGame.Instance.UiMouseState.Position;

            bool down = mouse.LeftButton == ButtonState.Pressed;
            bool wasDown = prev.LeftButton == ButtonState.Pressed;

            if (down && !wasDown)
            {
                _pressedOk = _okRect.Contains(p);
                _pressedCancel = _cancelRect.Contains(p);
                _pressedCareer = _careerRects.FindIndex(r => r.Contains(p));
            }
            else if (!down && wasDown)
            {
                if (_pressedOk && _okRect.Contains(p))
                    OkClicked?.Invoke(this, EventArgs.Empty);
                else if (_pressedCancel && _cancelRect.Contains(p))
                    CancelClicked?.Invoke(this, EventArgs.Empty);
                else if (_nameRect.Contains(p))
                    NameFieldClicked?.Invoke(this, EventArgs.Empty);
                else if (_pressedCareer >= 0 && _pressedCareer < _careerRects.Count &&
                         _careerRects[_pressedCareer].Contains(p))
                {
                    if (_selected != _pressedCareer)
                    {
                        _selected = _pressedCareer;
                        SelectionChanged?.Invoke(this, EventArgs.Empty);
                    }
                }

                _pressedOk = _pressedCancel = false;
                _pressedCareer = -1;
            }
        }

        public override void Draw(GameTime gameTime)
        {
            if (!Visible) return;

            var sb = GraphicsManager.Instance.Sprite;
            var font = GraphicsManager.Instance.Font;
            if (sb == null || font == null) return;

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                     null, null, null, UiScaler.SpriteTransform);

            // Moldura da lista de classes + painel de atributos (uma peça só) e as linhas
            // STR/AGI/CON/INT por cima.
            DrawPiece(_texPanelFrame, _attRect, PanelFrameSrc);
            // Painel escuro ATRÁS das linhas de atributo (dá contraste pro texto sobre o
            // cenário 3D). Vem depois da moldura e antes do texto.
            DrawPiece(_texDarkLabel, _darkAttRect, DarkLabelSrc);
            DrawAttributes(font);

            // Radar da classe selecionada. Cada classe tem a SUA arte (o polígono já vem
            // desenhado), então aqui é só escolher a textura certa.
            if (_classes.Count > 0
                && _texSpider.TryGetValue(_classes[_selected].Info.Model, out var spider))
                DrawPiece(spider, _spiderRect, SpiderSrc);

            // Lista de classes. Três estados DISTINTOS, como no oficial:
            //   selecionado = vermelho vivo + GLOW laranja por trás;
            //   hover       = quadro intermediário;
            //   normal      = escuro.
            var mousePos = MuGame.Instance.UiMouseState.Position;
            for (int i = 0; i < _careerRects.Count; i++)
            {
                var rect = _careerRects[i];
                bool selected = i == _selected;
                bool hover = rect.Contains(mousePos);

                // A arte tem 3 estados: default / hover / selecionado. O "pressionado" usa o
                // de hover (não existe arte própria) — quem marca o clique é a seleção, que
                // acontece no soltar do botão.
                int state = selected ? 2 : (hover || _pressedCareer == i) ? 1 : 0;
                DrawPiece(_texClassBtn, rect, ClassBtnSrc[state]);

                // O nome fica CENTRADO no botão (horizontal e vertical) — o DrawString com
                // center:true já faz os dois. ClassTextDX/DY são só um ajuste fino opcional
                // por cima disso; em 0 o texto cai no centro exato.
                // O DY é somado no Y DE TELA (cresce pra baixo): + desce, - sobe.
                var textRect = new Rectangle(
                    rect.X + (int)MathF.Round(ClassTextDX * S),
                    rect.Y + (int)MathF.Round(ClassTextDY * S),
                    rect.Width, rect.Height);
                DrawString(font, _classes[i].Info.Name, textRect, ClassTextScale,
                           TextNormal, center: true);

                // O glow vai POR CIMA do botão selecionado (é o risco de luz que corta a
                // barra na referência, não um fundo). Desenhado DEPOIS do botão e do texto,
                // com blend ADITIVO: é brilho, então soma luz em vez de tapar o que está
                // embaixo. Sangra pros lados na proporção da arte (203 contra 147).
                if (selected)
                {
                    int glowW = (int)(rect.Width * (ClassGlowSrc.Width / (float)ClassBtnSrc[2].Width));
                    int glowH = (int)(rect.Height * GlowHeightFactor);
                    var glowRect = new Rectangle(
                        rect.Center.X - glowW / 2, rect.Center.Y - glowH / 2, glowW, glowH);

                    sb.End();
                    sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                             null, null, null, UiScaler.SpriteTransform);
                    DrawPiece(_texClassBtn, glowRect, ClassGlowSrc);
                    sb.End();
                    sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                             null, null, null, UiScaler.SpriteTransform);
                }
            }

            // Caixa do nome + descrição + o assassino: tudo numa peça só, então ela ocupa o
            // bloco inteiro de baixo (o _descRect), e o campo do nome é desenhado DENTRO.
            DrawPiece(_texNameDesc, _descRect, NameDescSrc);
            // Painel escuro ATRÁS da prosa da classe.
            DrawPiece(_texDarkLabel, _darkDescRect, DarkLabelSrc);
            DrawDescription(font);

            // Ordem importa: a caixa do nickname vai POR CIMA do name_desc, e o texto
            // digitado por cima dela.
            DrawPiece(_texNameInput, _nameInputRect, NameInputSrc);

            // Plaquinha "Name" (vai por cima da que já vem na arte do name_desc) + o rótulo
            // centrado nela. O texto é centrado no VÃO da plaquinha, não na arte inteira: o
            // ornamento não é simétrico — medido, o miolo escuro fica 2.5px (de 123) à
            // ESQUERDA do centro da arte, e centrar na arte jogava o texto pra direita.
            DrawPiece(_texNamePlate, _namePlateRect, NamePlateSrc);
            var plateTextRect = new Rectangle(
                _namePlateRect.X + (int)MathF.Round(NamePlateTextDX * S),
                _namePlateRect.Y + (int)MathF.Round(NamePlateTextDY * S),
                _namePlateRect.Width, _namePlateRect.Height);
            DrawString(font, "Name", plateTextRect, FontNamePlate, TextNormal, center: true);

            // Só o que o usuário digitou: sem placeholder. A arte já traz a plaquinha "Name",
            // então a frase ("Please enter the nickname") era ruído em cima do campo.
            if (!string.IsNullOrEmpty(CharacterName))
                DrawString(font, CharacterName, _nameTextRect, FontName, TextNormal);

            // OK e CANCEL: a MESMA arte (a folha só traz default e hover — as duas metades são
            // molduras VAZIAS, sem texto assado). Quem diferencia é o rótulo, desenhado por
            // cima. Conta vazia = "Start Game" (cria e entra direto), com personagens = OK.
            DrawButton(font, _okRect, AccountIsEmpty ? "Start Game" : "OK", _pressedOk, mousePos);
            DrawButton(font, _cancelRect, "Cancel", _pressedCancel, mousePos);


            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                     null, null, null, UiScaler.SpriteTransform);

            base.Draw(gameTime);
        }

        /// <summary>
        /// Botão da folha ok_cancel: moldura (default/hover) + rótulo centrado.
        /// As duas metades da arte são molduras VAZIAS — o texto é nosso.
        /// </summary>
        private void DrawButton(SpriteFont font, Rectangle rect, string text, bool pressed, Point mouse)
        {
            bool hover = rect.Contains(mouse) || pressed;
            DrawPiece(_texOkCancel, rect, hover ? OkBtnHoverSrc : OkBtnSrc,
                      tint: pressed ? Pressed : Color.White);
            DrawString(font, text, rect, FontClass, TextNormal, center: true);
        }

        /// <summary>
        /// Linhas de atributo do Data: rótulo à esquerda, valor laranja 81px à direita.
        /// A LISTA manda — o Dark Lord traz CHA no lugar de INT.
        /// </summary>
        private void DrawAttributes(SpriteFont font)
        {
            if (_classes.Count == 0) return;

            var attrs = _classes[_selected].Info.Attributes;
            for (int i = 0; i < attrs.Count; i++)
            {
                // Centro da célula img_Att desta linha.
                float cy = AttY + AttCellTopY - AttRowPitch * i;
                float cx = AttX + AttCellX;

                // Rótulo termina no meio da célula; valor termina +81 depois (passo do
                // prefab). Mesma linha de base: ambos centrados em cy.
                float labelRight = cx - AttCellW / 2f + AttLabelW + AttRowShiftX;
                var labelRect = R(labelRight - AttLabelW / 2f, cy, AttLabelW, AttCellH);
                var valueRect = R(labelRight + AttValueDX - AttValueW / 2f, cy,
                                  AttValueW, AttCellH);

                // Ambos m_Alignment=3 = MiddleRight.
                DrawString(font, attrs[i].Label, labelRect, FontStat, TextNormal, right: true);
                DrawString(font, attrs[i].Value.ToString(), valueRect, FontStat,
                           TextValue, right: true);
            }
        }

        /// <summary>Prosa da classe com quebra de linha na largura da caixa.</summary>
        private void DrawDescription(SpriteFont font)
        {
            if (_classes.Count == 0) return;

            string text = _classes[_selected].Info.Description;
            if (string.IsNullOrEmpty(text)) return;

            var box = R(DescTextCX, DescTextCY, DescTextW, DescTextH);

            float scale = DescScale(font, box);
            var lines = WrapText(font, text, scale, box.Width);

            // m_LineSpacing = 1.1 no prefab.
            float lineH = font.LineSpacing * scale * 1.1f;
            float x = box.X;
            float y = box.Y;   // m_Alignment=0 = UpperLeft

            var sb = GraphicsManager.Instance.Sprite;
            foreach (var line in lines)
            {
                sb.DrawString(font, line, new Vector2(x, y) + new Vector2(1, -1),
                              Color.Black * 0.5f, 0f, Vector2.Zero, scale,
                              SpriteEffects.None, 0f);
                sb.DrawString(font, line, new Vector2(x, y), TextNormal, 0f, Vector2.Zero,
                              scale, SpriteEffects.None, 0f);
                y += lineH;
            }
        }

        /// <summary>
        /// Escala ÚNICA da prosa, para TODAS as classes: o pior caso (a descrição mais
        /// longa) é quem define. Escala por-classe daria uma fonte diferente em cada
        /// aba do mesmo lugar — sem uniformidade, e o texto "pula" ao trocar de classe.
        /// A caixa do prefab (71px) foi medida pra fonte do mobile, bem mais compacta que
        /// a Arial do cliente, então o nominal (17/24) não cabe e precisa encolher.
        /// Medida uma vez e cacheada (depende só da fonte e do tamanho da caixa).
        /// </summary>
        private float DescScale(SpriteFont font, Rectangle box)
        {
            if (_descScale > 0f)
                return _descScale;

            _descScale = FontDesc;
            foreach (var (info, _) in _classes)
            {
                string t = info.Description;
                if (string.IsNullOrEmpty(t))
                    continue;

                // Piso 0.35: a caixa é a arte NATIVA (156px, não estica — ver DescH) e o
                // vão útil da prosa tem só ~64px. Com piso 0.5 o laço parava antes de
                // caber e a 3ª linha vazava pra fora da moldura, por cima do cenário.
                while (_descScale > FontDesc * 0.35f)
                {
                    var lines = WrapText(font, t, _descScale, box.Width);
                    if (lines.Count * font.LineSpacing * _descScale * 1.1f <= box.Height)
                        break;
                    _descScale -= 0.02f;
                }
            }
            return _descScale;
        }

        private static List<string> WrapText(SpriteFont font, string text, float scale, float maxW)
        {
            var lines = new List<string>();
            var line = new System.Text.StringBuilder();

            foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                string probe = line.Length == 0 ? word : $"{line} {word}";
                if (font.MeasureString(probe).X * scale > maxW && line.Length > 0)
                {
                    lines.Add(line.ToString());
                    line.Clear().Append(word);
                }
                else
                {
                    line.Clear().Append(probe);
                }
            }
            if (line.Length > 0)
                lines.Add(line.ToString());
            return lines;
        }

        private static void DrawString(SpriteFont font, string text, Rectangle rect,
                                       float scale, Color color,
                                       bool center = false, bool right = false)
        {
            if (string.IsNullOrEmpty(text)) return;

            var sb = GraphicsManager.Instance.Sprite;
            Vector2 size = font.MeasureString(text) * scale;
            float x = center ? rect.X + (rect.Width - size.X) / 2f
                    : right ? rect.Right - size.X
                    : rect.X;
            float y = rect.Y + (rect.Height - size.Y) / 2f;
            var pos = new Vector2(x, y);

            // Outline do prefab: preto 50%, offset (1,-1).
            sb.DrawString(font, text, pos + new Vector2(1, -1), Color.Black * 0.5f,
                          0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            sb.DrawString(font, text, pos, color, 0f, Vector2.Zero, scale,
                          SpriteEffects.None, 0f);
        }

        /// <summary>
        /// Desenha a textura inteira em <paramref name="rect"/>.
        /// Para arte OZT use <see cref="DrawPiece"/>: OZT é potência de 2 e a textura vem com
        /// padding, então "inteira" inclui o vazio.
        /// </summary>
        private static void Draw(Texture2D? tex, Rectangle rect, Color? tint = null)
        {
            if (tex == null) return;
            GraphicsManager.Instance.Sprite.Draw(tex, rect, tint ?? Color.White);
        }

        /// <summary>
        /// Desenha um RECORTE da textura — o que a arte da tela de criação exige, por dois
        /// motivos:
        ///
        /// 1) O OZT é POTÊNCIA DE 2: o OZTReader arredonda pra cima e põe o conteúdo no canto
        ///    (class_btn = 794x26 de arte numa textura 1024x32). Desenhar a textura inteira
        ///    estica a peça e traz o padding vazio junto.
        /// 2) A arte vem em FOLHA: os 4 estados do botão de classe (e o default/hover do
        ///    OK/Cancel) moram lado a lado no mesmo arquivo.
        /// </summary>
        private static void DrawPiece(Texture2D? tex, Rectangle rect, Rectangle src, Color? tint = null)
        {
            if (tex == null) return;
            GraphicsManager.Instance.Sprite.Draw(tex, rect, src, tint ?? Color.White);
        }
    }
}
