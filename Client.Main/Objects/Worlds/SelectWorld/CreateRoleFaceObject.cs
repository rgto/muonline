#nullable enable
using System.Threading.Tasks;
using Client.Main.Content;
using Client.Main.Core.Utilities;
using Microsoft.Xna.Framework;

// "SelectWrold" (sic): typo herdado — é o namespace que os outros objetos desta pasta
// já usam. Criar um segundo namespace na mesma pasta só pra corrigir a grafia seria
// pior que conviver com ele.
namespace Client.Main.Objects.Worlds.SelectWrold
{
    /// <summary>
    /// Busto 3D da classe na tela de criação de personagem — o "go_showModel" do prefab
    /// mobile login_logincreateroleui.
    ///
    /// Os modelos são os Model/Face/newfaceNN do cliente mobile, convertidos pra glTF
    /// (tools/dh2bmd/fbx_to_gltf.py) e servidos de Data/Face/*.glb. Pose e escala vêm do
    /// cfg_Character_create em runtime (modelPosition/modelRotation/modelScale), via
    /// CharacterCreateDatabase — nada hardcoded aqui.
    /// </summary>
    public sealed class CreateRoleFaceObject : ModelObject
    {
        /// <summary>
        /// Altura que TODO busto tem no mundo, seja qual for a classe. A escala de cada
        /// modelo é derivada daqui (TargetBustHeight / altura crua da malha), nunca fixada
        /// à mão — os prefabs vêm em tamanhos bem diferentes e é isso que os iguala.
        ///
        /// 285 porque, a FaceDistance = 900 (SelectWorld), a janela visível tem 466 de
        /// altura: o busto ocupa ~61% dela, o enquadramento da referência, com folga acima
        /// da cabeça. Escala e distância andam juntas — mexer numa reenquadra o busto.
        ///
        /// Nota sobre a ordem de grandeza (parece grande, mas confere): estes rigs são da
        /// família ANINHADA (armature com escala 0.01). No GltfLoader o root vira
        /// CreateScale(muScale) * armatureWorld e, sem um .bmd irmão pra medir, muScale
        /// fica no default 100 — 100 x 0.01 = 1.0, se anula. O glb chega ao mundo no
        /// tamanho cru (~0.34 de diagonal), então toda a escala sai daqui.
        /// </summary>
        private const float TargetBustHeight = 228f;

        /// <summary>Escala de emergência, se a malha vier sem bbox mensurável.</summary>
        private const float DisplayScale = 845f;

        /// <summary>
        /// Quanto o topo da cabeça fica ACIMA da linha de visada da câmera, em unidades de
        /// mundo. É o que define o enquadramento de busto: a cabeça sobra pra cima da mira
        /// e o corte fica no tronco. Como a mira é o topo MEDIDO da malha, o mesmo valor
        /// enquadra igual todas as classes.
        /// 195: a caixa de descrição ocupa a faixa de baixo da tela e comia o busto (rosto
        /// atrás do texto); subindo a cabeça, o rosto fica na área livre de cima, como na
        /// referência. Subiu de 140 junto com o ContentDY do LoginCreateRoleControl, que
        /// levantou o painel e o bloco — o busto é 3D (mora no mundo, não na UI), então não
        /// vem de carona: os dois têm que andar juntos.
        /// Anda junto com FaceDistance/TargetBustHeight.
        /// </summary>
        private const float HeadRoom = 170f;

        /// <summary>
        /// Ângulo base que põe o busto de frente pra câmera desta cena. Calibrado na tela
        /// com a Fairy Elf e o Dark Lord, que saem corretos com este valor e giro extra 0.
        ///
        /// Por que o modelRotation do config NÃO entra aqui: ele foi autorado pra câmera do
        /// MOBILE, que não é a nossa, e aplicá-lo deixava as classes em ângulos diferentes.
        /// </summary>
        private const float FaceCameraAngle = 138.5f;

        private string? _model;

        /// <summary>
        /// O busto é UM modelo em destaque na tela (não uma multidão), então anima por
        /// frame: sem isso o ModelObject congela os ossos no frame 0 (ele só recalcula a
        /// pose de quem pede animação por frame) e o rosto fica estático.
        /// </summary>
        protected override bool RequiresPerFrameAnimation => true;

        /// <summary>
        /// Velocidade da animação do busto, em QUADROS POR SEGUNDO.
        ///
        /// Precisa ser dita aqui porque estes .bmd não têm nenhuma referência de tempo pro
        /// AdvanceKeysPerSecond se ancorar: sem NativeDurationSeconds (isso é coisa de glTF)
        /// e sem BmdKeys (não há .bmd irmão — o busto JÁ é o bmd), ele cai no default
        /// AnimationSpeed = 4 quadros/s. A ação de idle da Summoner tem 80 quadros: a 4/s
        /// daria um ciclo de 20 SEGUNDOS — a câmera lenta que se via na tela.
        ///
        /// 25 é a taxa em que as animações de personagem do MU foram autoradas (o cliente
        /// clássico toca a 25 fps); a 25/s o mesmo idle de 80 quadros fecha em ~3,2s.
        /// Knob único de calibração: ↑ = mais rápido.
        /// </summary>
        private const float BustAnimationFps = 25f;

        public CreateRoleFaceObject()
        {
            Interactive = false;
            LightEnabled = false;
            AnimationSpeed = BustAnimationFps;
        }

        /// <summary>
        /// Troca a classe exibida. Recarrega o modelo só quando o prefab muda.
        /// </summary>
        public async Task SetClass(CharacterCreateClass info, Vector3 anchor)
        {
            // O modelPosition/modelScale/modelRotation do config estão em unidades e base da
            // CÂMERA DO MOBILE, não do mundo do MU: somar o modelPosition enterrava o modelo
            // (o -197 ia parar no Z) e o modelRotation deixava DW e Elf de lado. Quem decide
            // posição, tamanho e giro aqui é a CENA, pelo frustum da câmera dela.
            if (_model != info.Model)
            {
                // Troca de prefab: recarrega o asset e reconstrói os buffers da malha.
                // LoadContent() é o caminho que o ModelObject usa quando o Model muda (é o
                // mesmo do unequip de item); Initialize() só roda uma vez, no primeiro Load.
                _model = info.Model;
                Model = await BMDLoader.Instance.Prepare($"Logo/{info.Model}.bmd");
                await LoadContent();
            }

            // Giro = ângulo base da câmera + a correção DAQUELE modelo, vinda do Data.
            // Cada prefab foi modelado com o corpo num ângulo diferente, e isso não é
            // dedutível dos ossos: Elf e DL saem de frente com correção 0, enquanto os
            // outros precisam de giro próprio. Quem já está certo tem 0 no arquivo e não é
            // tocado — foi assim que uma tentativa "global" quebrou os dois que estavam bons.
            // A correção tem os 3 eixos porque giro lateral (Z) não resolve tudo: alguns
            // prefabs vêm com o corpo TOMBADO (o Dark Wizard), e aí é X/Y que endireita.
            Vector3 extra = FaceFacing.GetExtraRotation(info.Model);
            Angle = new Vector3(
                MathHelper.ToRadians(extra.X),
                MathHelper.ToRadians(extra.Y),
                MathHelper.ToRadians(FaceCameraAngle + extra.Z));

            // Escala: NORMALIZA cada busto pra mesma altura de mundo, medindo o bbox real
            // do modelo em vez de propagar o modelScale do config. O config é relativo à
            // câmera do mobile e os prefabs são modelados em tamanhos diferentes (DK 1150,
            // Elf 1870) justamente pra todos acabarem enquadrados igual; repassar essa
            // razão aqui fazia a Elf sair 463 de altura contra 285 do DK e vazar da tela.
            // Uniformidade é o padrão: o busto tem a mesma altura em todas as classes.
            // A altura sai da MALHA, não do BoundingBoxLocal: o ModelObject só recalcula o
            // bbox no ciclo de update seguinte (o _boundingComputed só é zerado ali), então
            // logo depois do LoadContent ele ainda traz o tamanho da classe ANTERIOR — e
            // todas as classes acabavam com a escala da primeira que carregou.
            var (zMin, zMax) = MeasureModelZ();
            float rawHeight = zMax - zMin;
            Scale = rawHeight > 0.0001f ? TargetBustHeight / rawHeight : DisplayScale;

            // Posiciona pelo TOPO DA CABEÇA medido, não por uma fração da altura: as malhas
            // não compartilham origem nem proporção (a Elf mede 0.18 de altura crua contra
            // 0.43 do DK), então "descer X% da altura" ancorava cada classe num ponto
            // diferente do corpo e jogava a Elf pra fora da tela. Com o topo real da cabeça
            // caindo sempre em HeadRoom acima da mira, todas ficam enquadradas igual.
            float headTopLocal = zMax * Scale;
            Position = anchor + Vector3.UnitZ * (HeadRoom - headTopLocal);
        }

        /// <summary>
        /// Altura da malha em unidades locais (antes do Scale), medida direto nos vértices
        /// já passados pelo BoneTransform — o mesmo caminho que o ModelObject usa pra montar
        /// o BoundingBoxLocal, mas disponível na hora, sem esperar o próximo update.
        /// Estes rigs assam a escala nas matrizes de osso: ignorar o BoneTransform daria
        /// altura errada.
        /// </summary>
        private (float Min, float Max) MeasureModelZ()
        {
            var model = Model;
            var bones = BoneTransform;
            if (model?.Meshes == null || bones == null)
                return (0f, 0f);

            float min = float.MaxValue, max = float.MinValue;
            foreach (var mesh in model.Meshes)
            {
                var vertices = mesh.Vertices;
                if (vertices == null) continue;

                foreach (var vertex in vertices)
                {
                    int bone = vertex.Node;
                    if (bone < 0 || bone >= bones.Length) continue;

                    float z = Vector3.Transform(vertex.Position, bones[bone]).Z;
                    if (z < min) min = z;
                    if (z > max) max = z;
                }
            }

            return max > min ? (min, max) : (0f, 0f);
        }

    }
}
