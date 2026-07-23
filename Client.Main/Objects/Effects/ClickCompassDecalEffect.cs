using System.Threading.Tasks;
using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Objects.Effects
{
    /// <summary>
    /// A "bússola" dourada desenhada no CHÃO ao clicar para andar — réplica do
    /// decal do MU mobile (dianji.u3d → MeshUV + Framework.ScreenTerrrinEffect,
    /// material ClickTerrinEffect_02): 3 camadas (círculo rúnico, lanças em X,
    /// estrela cardinal) com alpha/escala dirigidos pelas curvas EXATAS do prefab,
    /// drapejadas sobre o relevo (grade cujos vértices seguem a altura do terreno,
    /// como o vertex shader oficial faz com o heightmap).
    ///
    /// Texturas: bake das oficiais do mobile (tools: bake_click_fx.py) com o tint
    /// do shader já assado (rgb = 2·tex·_CommonColor, alpha = 2·tex.r).
    /// Desenho: BasicEffect3D + vertex color (mesma receita comprovada do
    /// ClickPinShineEffect); depth-test desligado — o decal é curto (1.5s) e
    /// ganhar do z-fight contra o terreno triangulado vale mais que a oclusão.
    /// </summary>
    public class ClickCompassDecalEffect : EffectObject
    {
        private const float Duration = 1.5f;      // duration do ScreenTerrrinEffect
        private const float BaseSize = 200f;      // quad cheio do decal (escala 1.0) = 2 tiles
        // O terreno RENDERIZADO (triângulos) fica acima da altura bilinear amostrada
        // em vários pontos — offset alto (valor comprovado do BloodStainEffect).
        private const float GroundOffset = 70f;
        private const int GridQuads = 8;          // 8x8 quads (9x9 vértices) drapejados

        private const string CircleTexPath = "Effect/click_circle.ozp";
        private const string Arrow1TexPath = "Effect/click_arrow1.ozp";
        private const string Arrow2TexPath = "Effect/click_arrow2.ozp";

        // Curvas do componente ScreenTerrrinEffect (tempo normalizado 0..1 sobre 1.5s).
        // Keyframes literais do dump: (time, value, inSlope, outSlope).
        private static readonly ClickEffectCurve CircleAlpha = new(
            new[] { 0f, 0f, 6.4565067f, 6.4565067f },
            new[] { 0.07784519f, 0.502608f, -0.21546452f, -0.21546452f },
            new[] { 0.6807415f, 0.14429167f, -1.537412f, -1.537412f },
            new[] { 0.8022095f, -0.0058288574f, -0.087292716f, -0.087292716f },
            new[] { 1f, 0f, 0.029469855f, 0.029469855f });

        private static readonly ClickEffectCurve Arrow1Alpha = new(
            new[] { 0f, 0f, 2f, 2f },
            new[] { 0.11919363f, 0.9898252f, -0.749928f, -0.749928f },
            new[] { 1f, 0f, -0.31195763f, -0.31195763f });

        private static readonly ClickEffectCurve Arrow2Alpha = new(
            new[] { -0.0118255615f, 0.9831238f, 2f, -0.07172097f },
            new[] { 0.24286366f, 0.9648572f, -0.578925f, -0.578925f },
            new[] { 1f, 0f, -1.9000758f, -1.9000758f });

        private const float CircleScale = 0.5700543f;   // constante no prefab

        private static readonly ClickEffectCurve Arrow1Scale = new(
            new[] { -0.0010038614f, 0.59922993f, -0.9875818f, -0.9875818f },
            new[] { 0.1795252f, 0.42094272f, -0.034155857f, -0.034155857f },
            new[] { 0.3915091f, 0.61008775f, -0.047684334f, -0.047684334f },
            new[] { 0.59351885f, 0.4209386f, -0.994399f, -0.994399f },
            new[] { 0.79257435f, 0.6112294f, 0.07335405f, 0.07335405f },
            new[] { 0.99662673f, 0.47768456f, -0.8335565f, -0.8335565f });

        private static readonly ClickEffectCurve Arrow2Scale = new(
            new[] { 0f, 0.8f, -3.1581757f, -3.1581757f },
            new[] { 0.078437135f, 0.4f, 0f, 0f },
            new[] { 0.2101487f, 0.80473024f, 0.009797716f, 0.009797716f },
            new[] { 0.46212983f, 0.40424195f, -0.00032731466f, -0.00032731466f },
            new[] { 0.60872364f, 0.7968906f, 0.6110259f, 0.6110259f },
            new[] { 0.76184446f, 0.4040238f, -0.03214143f, -0.03214143f },
            new[] { 0.9918554f, 0.8f, 1.3955637f, 1.3955637f });

        private static readonly short[] GridIndices = BuildGridIndices();
        private readonly VertexPositionColorTexture[] _gridVerts =
            new VertexPositionColorTexture[(GridQuads + 1) * (GridQuads + 1)];

        private Texture2D _circleTex;
        private Texture2D _arrow1Tex;
        private Texture2D _arrow2Tex;
        private float _time = Duration;

        public ClickCompassDecalEffect()
        {
            BlendState = BlendState.NonPremultiplied;
            LightEnabled = false;
            IsTransparent = true;
            Hidden = true;
            BoundingBoxLocal = new BoundingBox(
                new Vector3(-BaseSize, -BaseSize, -50f),
                new Vector3(BaseSize, BaseSize, 50f));
        }

        public override async Task Load()
        {
            await base.Load();
            await TextureLoader.Instance.Prepare(CircleTexPath);
            await TextureLoader.Instance.Prepare(Arrow1TexPath);
            await TextureLoader.Instance.Prepare(Arrow2TexPath);
            _circleTex = TextureLoader.Instance.GetTexture2D(CircleTexPath);
            _arrow1Tex = TextureLoader.Instance.GetTexture2D(Arrow1TexPath);
            _arrow2Tex = TextureLoader.Instance.GetTexture2D(Arrow2TexPath);
        }

        /// <summary>Reinicia o decal na posição clicada (chamado pelo CursorObject.Show).</summary>
        public void PlayAt(Vector3 position)
        {
            Position = position;
            _time = 0f;
            Hidden = false;
            Alpha = 1f;
        }

        public override void Update(GameTime gameTime)
        {
            if (_time < Duration)
            {
                _time += (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (_time >= Duration)
                    Hidden = true;
            }

            base.Update(gameTime);
        }

        public override void Draw(GameTime gameTime)
        {
            if (Hidden || _time >= Duration || World?.Terrain == null)
                return;
            if (_circleTex == null && _arrow1Tex == null && _arrow2Tex == null)
                return;

            float t = _time / Duration;

            var gd = GraphicsManager.Instance.GraphicsDevice;
            var effect = GraphicsManager.Instance.BasicEffect3D;
            if (effect == null)
                return;

            var prevBlend = gd.BlendState;
            var prevSampler = gd.SamplerStates[0];
            var prevDepth = gd.DepthStencilState;
            var prevRaster = gd.RasterizerState;
            var prevDiffuse = effect.DiffuseColor;
            var prevAlpha = effect.Alpha;
            var prevVertexColor = effect.VertexColorEnabled;
            var prevTexEnabled = effect.TextureEnabled;
            var prevLight = effect.LightingEnabled;
            var prevTexture = effect.Texture;
            var prevWorld = effect.World;

            try
            {
                gd.BlendState = BlendState.NonPremultiplied;   // blend do material oficial
                gd.SamplerStates[0] = SamplerState.LinearClamp;
                gd.DepthStencilState = DepthStencilState.None;
                gd.RasterizerState = RasterizerState.CullNone;

                effect.World = Matrix.Identity;                // vértices já em espaço de mundo
                effect.View = Camera.Instance.View;
                effect.Projection = Camera.Instance.Projection;
                effect.VertexColorEnabled = true;              // alpha vai no vertex color
                effect.TextureEnabled = true;
                effect.LightingEnabled = false;
                effect.DiffuseColor = Color.White.ToVector3();
                effect.Alpha = 1f;

                // Ordem do prefab: círculo por baixo, setas por cima.
                DrawLayer(gd, effect, _circleTex, CircleScale, CircleAlpha.EvaluateClamped01(t));
                DrawLayer(gd, effect, _arrow1Tex, Arrow1Scale.Evaluate(t), Arrow1Alpha.EvaluateClamped01(t));
                DrawLayer(gd, effect, _arrow2Tex, Arrow2Scale.Evaluate(t), Arrow2Alpha.EvaluateClamped01(t));
            }
            finally
            {
                effect.Texture = prevTexture;
                effect.VertexColorEnabled = prevVertexColor;
                effect.TextureEnabled = prevTexEnabled;
                effect.LightingEnabled = prevLight;
                effect.DiffuseColor = prevDiffuse;
                effect.Alpha = prevAlpha;
                effect.World = prevWorld;
                gd.BlendState = prevBlend;
                gd.SamplerStates[0] = prevSampler;
                gd.DepthStencilState = prevDepth;
                gd.RasterizerState = prevRaster;
            }
        }

        private void DrawLayer(GraphicsDevice gd, BasicEffect effect, Texture2D texture, float scale, float alpha)
        {
            if (texture == null || alpha <= 0.004f || scale <= 0.01f)
                return;

            BuildGrid(scale * BaseSize, alpha * TotalAlpha);

            effect.Texture = texture;

            foreach (var pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                gd.DrawUserIndexedPrimitives(
                    PrimitiveType.TriangleList,
                    _gridVerts, 0, _gridVerts.Length,
                    GridIndices, 0, GridQuads * GridQuads * 2);
            }
        }

        /// <summary>
        /// Grade centrada na posição do clique com cada vértice na altura REAL do
        /// terreno (equivalente ao drapejo por heightmap do shader oficial).
        /// </summary>
        private void BuildGrid(float size, float alpha)
        {
            var terrain = World.Terrain;
            float half = size * 0.5f;
            float step = size / GridQuads;
            var color = new Color(1f, 1f, 1f, alpha);

            int v = 0;
            for (int gy = 0; gy <= GridQuads; gy++)
            {
                float wy = Position.Y - half + gy * step;
                for (int gx = 0; gx <= GridQuads; gx++, v++)
                {
                    float wx = Position.X - half + gx * step;
                    float wz = terrain.RequestTerrainHeight(wx, wy);
                    // Tiles com flag Height devolvem SpecialHeight (1200) — um único
                    // vértice desses viraria uma tenda gigante; prende no Z do clique.
                    if (wz > Position.Z + 150f || wz < Position.Z - 300f)
                        wz = Position.Z;
                    _gridVerts[v] = new VertexPositionColorTexture(
                        new Vector3(wx, wy, wz + GroundOffset),
                        color,
                        new Vector2(gx / (float)GridQuads, gy / (float)GridQuads));
                }
            }
        }

        private static short[] BuildGridIndices()
        {
            var indices = new short[GridQuads * GridQuads * 6];
            int i = 0;
            for (int gy = 0; gy < GridQuads; gy++)
            {
                for (int gx = 0; gx < GridQuads; gx++)
                {
                    short v0 = (short)(gy * (GridQuads + 1) + gx);
                    short v1 = (short)(v0 + 1);
                    short v2 = (short)(v0 + GridQuads + 1);
                    short v3 = (short)(v2 + 1);
                    indices[i++] = v0; indices[i++] = v1; indices[i++] = v2;
                    indices[i++] = v2; indices[i++] = v1; indices[i++] = v3;
                }
            }
            return indices;
        }

        public override void Dispose()
        {
            base.Dispose();
            _circleTex = null;
            _arrow1Tex = null;
            _arrow2Tex = null;
        }
    }
}
