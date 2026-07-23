using System.Threading.Tasks;
using Client.Main.Controls;
using Client.Main.Controls.UI;
using Client.Main.Controllers;
using Client.Main.Core.Utilities;
using Client.Main.Graphics;
using Client.Main.Models;
using Client.Main.Objects.Worlds.SelectWrold;
using Client.Main.Scenes.SelectCharacter;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Client.Main.Worlds
{
    public class SelectWorld : WorldControl
    {
        /// <summary>
        /// Onde o personagem fica na tela de SELEÇÃO — em cima do pilar.
        ///
        /// O PILAR é parte do mapa 3D e NÃO se move: quem se ajusta é o personagem. Estes
        /// são os knobs do editor (hud-edit-slots, peça char3d) — X/Y andam no plano do
        /// chão, Z é a altura (subir = ficar mais em cima do pilar).
        /// </summary>
        private const float CharPillarX = 14000f;
        private const float CharPillarY = 12302.5f;
        private const float CharPillarZ = 250f;

        private readonly Vector3 _characterDisplayPosition = new(CharPillarX, CharPillarY, CharPillarZ);
        private readonly Vector3 _characterDisplayAngle = new(0, 0, MathHelper.ToRadians(90));
        private ILogger<SelectWorld> _logger;
        private CharacterSelectionController _controller;

        /// <summary>
        /// Espaçamento entre personagens na fileira, em unidades de mundo.
        /// O mobile (LoginData.roleInitPos) enfileira 4 em x = -3,-1,1,3 — passo de 2
        /// unidades Unity. A escala de mundo aqui é outra (TERRAIN_SCALE=100), então o
        /// passo é este knob: único número a calibrar se a fileira ficar larga/estreita.
        /// 170: com 120 os personagens caíam a ~130px de distância NA TELA e os rótulos
        /// (RoleInfo = 185 prefab ≈ 178 virtuais) se sobrepunham.
        /// </summary>
        private const float CharacterSpacing = 170f;

        /// <summary>Máximo de personagens visíveis lado a lado (4 no mobile).</summary>
        public const int MaxVisibleCharacters = 4;

        /// <summary>
        /// Quanto o rótulo sobe acima da cabeça, em px virtuais (a Lua usa RoleHeight=270
        /// em px de tela do mobile). Knob de calibração visual.
        /// Z do mundo cresce PRA CIMA (o resto do cliente usa BoundingBoxWorld.Max.Z + off
        /// pra balão de chat / dano), então a cabeça é Max.Z.
        /// SetAnchor recebe o CENTRO do rótulo e o RoleInfo tem 57 de altura (~55 virtual):
        /// com lift 28 a metade de baixo caía exatamente sobre a cabeça e a tampava.
        /// 68 = 28 + meia-altura (27.4) + 12 de folga.
        /// </summary>
        private const float RoleLabelLift = 68f;

        public Vector3 CharacterDisplayPosition => _characterDisplayPosition;
        public Vector3 CharacterDisplayAngle => _characterDisplayAngle;

        /// <summary>
        /// Busto da classe na tela de criação (go_showModel do prefab). Fica em frente
        /// à fileira, na direção da câmera, pra aparecer grande no lugar do painel.
        /// </summary>
        private CreateRoleFaceObject _faceModel;

        /// <summary>
        /// Onde o busto nasce, derivado da CÂMERA (não de coordenadas fixas): um ponto à
        /// frente dela, deslocado pra esquerda, pra ocupar a metade esquerda da tela como
        /// no mobile. A câmera desta cena herda a Position da cena anterior (o SelectWorld
        /// só define o Target), então ancorar em coordenadas absolutas do mapa não é
        /// confiável — daí calcular a partir dos vetores dela.
        /// </summary>
        /// <param name="model">
        /// Modelo do busto ("newface06"): traz o ajuste lateral PRÓPRIO dele, do Data. Os
        /// bustos não compartilham o eixo — a Summoner nasce deslocada e só ela precisa
        /// correr; mexer no FaceSideOffset global desalinharia as outras cinco.
        /// </param>
        private Vector3 FaceAnchor(string? model = null)
        {
            var cam = Camera.Instance;
            Vector3 fwd = cam.Target - cam.Position;
            if (fwd.LengthSquared() < 1e-3f)
                return _characterDisplayPosition;
            fwd.Normalize();

            // Esquerda no plano do chão (a câmera olha de cima; Z é a vertical do mundo).
            var left = Vector3.Normalize(Vector3.Cross(Vector3.UnitZ, fwd));
            float side = FaceSideOffset + FaceFacing.GetSideOffset(model);

            // Este é o ponto de MIRA (a linha de visada). Quem desce o modelo pra que a
            // cabeça caia aqui é o próprio CreateRoleFaceObject, que sabe a altura da
            // malha da classe — daqui não dá pra saber, e chutar uma altura fixa fazia a
            // Elf (bem mais alta que o Dark Knight) sumir embaixo da tela.
            return cam.Position + fwd * FaceDistance + left * side
                   + Vector3.UnitZ * FaceHeightOffset;
        }

        /// <summary>
        /// Enquadramento do busto, derivado do FRUSTUM da câmera (FOV 29°), não de
        /// tentativa e erro: a 900 unidades a janela visível tem 466 de altura por 828 de
        /// largura, então o busto de 285 ocupa ~61% — o enquadramento da referência, com
        /// folga acima da cabeça (a 800 ele encostava no topo da tela).
        /// FaceDistance 260 (o primeiro chute) dava uma janela de só 134: o modelo
        /// atravessava o near plane e só aparecia um fragmento dele no céu.
        /// FaceSideOffset desloca o busto pra ESQUERDA da linha de visada, pra ele ocupar a
        /// faixa livre ao lado do painel de atributos (medido na tela: a moldura do painel
        /// começa em x≈867 dos 1280 virtuais).
        /// 60: calibrado NA TELA, não pela conta — o centro geométrico da área livre daria
        /// ~110, mas a caixa de descrição cobre a faixa de baixo e a massa visual do busto
        /// (ombro/cabelo) puxa pra esquerda, então ele lia descentralizado. Cada 15 unidades
        /// daqui ≈ 23 px virtuais na tela.
        /// </summary>
        private const float FaceDistance = 900f;
        private const float FaceSideOffset = 60f;
        private const float FaceHeightOffset = 0f;

        /// <summary>
        /// Mostra o busto da classe (tela de criação). Chamar com null esconde.
        /// A pose (posição/rotação/escala) vem do Data, por classe.
        /// </summary>
        public async Task ShowCreateRoleFace(CharacterCreateClass info)
        {
            if (info == null)
            {
                if (_faceModel != null)
                    _faceModel.Hidden = true;
                return;
            }

            if (_faceModel == null)
            {
                _faceModel = new CreateRoleFaceObject { World = this };
                Objects.Add(_faceModel);
            }

            _faceModel.Hidden = false;
            await _faceModel.SetClass(info, FaceAnchor(info.Model));
        }

        /// <summary>
        /// Posição do i-ésimo de <paramref name="count"/> personagens na fileira,
        /// centrada em CharacterDisplayPosition. Fiel ao mobile: todos visíveis ao mesmo
        /// tempo, não um só com os outros escondidos.
        /// A fileira corre em +Y porque o personagem encara a câmera com Angle.Z=90°.
        /// </summary>
        public Vector3 GetCharacterSlotPosition(int index, int count)
        {
            if (count <= 1)
                return _characterDisplayPosition;

            float offset = (index - (count - 1) / 2f) * CharacterSpacing;
            return _characterDisplayPosition + new Vector3(0, offset, 0);
        }

        public SelectWorld() : base(worldIndex: 94)
        {
            EnableShadows = false;
            _logger = MuGame.AppLoggerFactory?.CreateLogger<SelectWorld>() ?? throw new System.InvalidOperationException("LoggerFactory not initialized in MuGame");
            Camera.Instance.ViewFar = 5500f;
        }

        public void SetController(CharacterSelectionController controller)
        {
            _controller = controller;
        }

        protected override void CreateMapTileObjects()
        {
            base.CreateMapTileObjects();
            MapTileObjects[14] = null;
            MapTileObjects[71] = typeof(BlendedObjects);
            MapTileObjects[11] = typeof(BlendedObjects);
            MapTileObjects[36] = typeof(LightObject);
            MapTileObjects[25] = typeof(BlendedObjects);
            MapTileObjects[33] = typeof(BlendedObjects);
            MapTileObjects[30] = typeof(BlendedObjects);
            MapTileObjects[31] = typeof(FlowersObject2);
            MapTileObjects[34] = typeof(FlowersObject);
            MapTileObjects[26] = typeof(WaterFallObject);
            MapTileObjects[24] = typeof(WaterFallObject);
            MapTileObjects[54] = typeof(WaterSplashObject);
            MapTileObjects[55] = typeof(WaterSplashObject);
            MapTileObjects[56] = typeof(WaterSplashObject);
        }

        public override void AfterLoad()
        {
            base.AfterLoad();

            // water animation parameters
            Terrain.WaterSpeed = 0.05f;
            Terrain.DistortionAmplitude = 0.2f;
            Terrain.DistortionFrequency = 1.0f;

            Camera.Instance.Target = new Vector3(14229.295898f, 12340.358398f, 380);
            Camera.Instance.FOV = 29 * Constants.FOV_SCALE;
        }

        public override void Update(GameTime time)
        {
            base.Update(time);
            if (!Visible) return;

            // Update label positions using controller data
            if (Status == GameControlStatus.Ready && _controller != null)
            {
                foreach (var (player, label) in _controller.Labels)
                {
                    if (player.Status != GameControlStatus.Ready || player.Hidden)
                    {
                        label.Visible = false;
                        continue;
                    }

                    // Fiel à Lua (NameOnRefresh): projeta a posição do personagem e
                    // sobe RoleHeight — o rótulo mora ACIMA da cabeça, não colado nela.
                    var head = new Vector3(
                        player.WorldPosition.Translation.X,
                        player.WorldPosition.Translation.Y,
                        player.BoundingBoxWorld.Max.Z);

                    var sp = GraphicsDevice.Viewport.Project(
                                 head,
                                 Camera.Instance.Projection,
                                 Camera.Instance.View,
                                 Matrix.Identity);

                    if (sp.Z is < 0 or > 1)
                    {
                        label.Visible = false;
                        continue;
                    }

                    var virtualPos = UiScaler.ToVirtual(new Point((int)sp.X, (int)sp.Y));
                    label.SetAnchor(new Vector2(virtualPos.X, virtualPos.Y - RoleLabelLift));
                    label.Visible = true;
                }
            }

            // Debug key handling
            if (MuGame.Instance.PrevKeyboard.IsKeyDown(Keys.Delete) && MuGame.Instance.Keyboard.IsKeyUp(Keys.Delete))
            {
                if (Objects.Count > 0)
                {
                    var obj = Objects[0];
                    _logger?.LogDebug($"Removing obj: {obj.Type} -> {obj.ObjectName}");
                    Objects.RemoveAt(0);
                }
            }
            else if (MuGame.Instance.Keyboard.IsKeyDown(Keys.Add))
            {
                Camera.Instance.ViewFar += 10;
            }
            else if (MuGame.Instance.Keyboard.IsKeyDown(Keys.Subtract))
            {
                Camera.Instance.ViewFar -= 10;
            }
        }

        public override void Draw(GameTime gameTime)
        {
            // Ensure correct render states for DirectX (and OpenGL for consistency)
            var gd = GraphicsManager.Instance.GraphicsDevice;
            gd.BlendState = BlendState.AlphaBlend;
            gd.DepthStencilState = DepthStencilState.Default;
            gd.SamplerStates[0] = SamplerState.LinearClamp;

            base.Draw(gameTime);
        }
    }
}
