using System;
using System.Threading.Tasks;
using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Objects.Effects
{
    /// <summary>
    /// O brilho SOBRE o pin no clique de movimento — réplica das camadas aéreas do
    /// prefab oficial do MU mobile (dianji.u3d):
    ///  • "Mesh_obj_7001": espeto de luz dourado (bipirâmide aditiva) girando
    ///    360°/1.333s e quicando em Z (curvas do clip AnimEff_dinji);
    ///  • "lizi": até 20 faíscas billboard (60/s no 1º segundo, vida 2s) com a
    ///    textura starburst oficial e jitter oscilante;
    ///  • "yuanhuan": burst de 4 anéis com orientação aleatória, vida 1s, textura
    ///    de streak com scroll (One·One aditivo).
    /// Todos os números (posições, tints, curvas) vêm do dump do bundle.
    /// </summary>
    public class ClickPinShineEffect : EffectObject
    {
        // ---- espeto (unidades mobile ·100; escala do GO (0.7,0.7,1.2) aplicada) ----
        private const float SpikeDuration = 1.3333f;
        private const float SpikeBaseZ = 48.7f;    // GO local y=0.48679
        private const float SpikeTipZ = 1.1f;      // mesh z=0.009·1.2
        private const float SpikeMidZ = 62.0f;     // mesh z=0.517·1.2 (parte mais larga)
        private const float SpikeTopZ = 94.8f;     // mesh z=0.790·1.2
        private const float SpikeHalfWidth = 13.3f; // 0.1905·0.7
        private const float SpikeSpinSpeed = 4.7124f; // 270°/s (giro 360° em 1.333s)
        private static readonly Vector3 SpikeTint = new(0.623f, 0.352f, 0.191f);

        private static readonly ClickEffectCurve SpikeBounce = new(   // clip: posição Y (s)
            new[] { 0f, 0f, 0f, 1.5f },
            new[] { 0.4f, 0.6f, 1.5f, -1.286f },
            new[] { 0.867f, 0f, -1.286f, 1.286f },
            new[] { 1.3333f, 0.6f, 1.286f, -1.2f });

        private static readonly ClickEffectCurve SpikeAlpha = new(    // clip: _TintColor.a
            new[] { 0f, 0.451f, 0.042f, 0.042f },
            new[] { 1.1667f, 0.5f, -3f, -3f },
            new[] { 1.3333f, 0f, -3f, -3f });

        // ---- faíscas (ParticleSystem "lizi") ----
        private const int MaxSparks = 20;
        private const float SparkRate = 60f;       // por segundo, no 1º segundo
        private const float SparkEmitWindow = 1f;
        private const float SparkLife = 2f;
        private const float SparkEmitZ = 109f;     // GO local y=1.09
        private const float SparkMinSize = 1f;     // startSize 0.01..0.1
        private const float SparkMaxSize = 10f;
        private const float SparkJitter = 100f;    // velocity-over-lifetime ±1 un/s
        private const float SparkJitterFreq = 4.7124f; // ~1.5 ciclos na vida de 2s
        private static readonly Vector3 SparkTint = new(0.5094f, 0.3824f, 0.1514f);
        private const float SparkBaseAlpha = 0.502f;

        // ---- anéis (ParticleSystem "yuanhuan") ----
        private const int RingCount = 4;
        private const float RingDuration = 1f;
        private const int RingSegments = 24;
        private const float RingEmitZ = 104.6f;    // GO local y=1.046
        private const float RingRadius = 25.6f;    // mesh r=1.28 · startSize 0.2
        private const float RingHalfHeight = 6.1f; // mesh h=0.61 · 0.2 / 2
        private const float RingUvTiles = 2f;
        private const float RingUvScroll = 3f;     // aproximação do _FlowFactor
        private static readonly Vector3 RingTint = new(1f, 0.5609f, 0.1274f);

        private static readonly ClickEffectCurve RingSize = new(
            new[] { 0f, 0f, 2.9602f, 2.9602f },
            new[] { 0.2026f, 0.9435f, 0.3797f, 0.3797f },
            new[] { 0.9122f, 0.9978f, -0.2194f, -0.2194f },
            new[] { 1f, 0f, -3.6714f, -3.6714f });

        private const float TotalDuration = 1.8f;  // última faísca visível ~1.78s

        private const string SparkTexPath = "Effect/click_spark.ozp";
        private const string TrailTexPath = "Effect/click_trail.ozp";

        // material dos anéis: Blend One One (aditivo puro)
        private static readonly BlendState AdditiveOneOne = new()
        {
            ColorSourceBlend = Blend.One,
            ColorDestinationBlend = Blend.One,
            AlphaSourceBlend = Blend.One,
            AlphaDestinationBlend = Blend.One,
        };

        private struct Spark
        {
            public bool Alive;
            public float Age;
            public float Size;
            public Vector3 Pos;
            public Vector3 Amp;
            public Vector3 Phase;
        }

        private readonly Spark[] _sparks = new Spark[MaxSparks];
        private readonly Matrix[] _ringOrientations = new Matrix[RingCount];

        private readonly VertexPositionColorTexture[] _spikeVerts = new VertexPositionColorTexture[6];
        private static readonly short[] SpikeIndices =
        {
            0, 1, 2,  0, 2, 3,  0, 3, 4,  0, 4, 1,   // metade de baixo
            5, 2, 1,  5, 3, 2,  5, 4, 3,  5, 1, 4,   // metade de cima
        };

        private readonly VertexPositionColorTexture[] _sparkVerts = new VertexPositionColorTexture[MaxSparks * 4];
        private static readonly short[] SparkIndices = BuildQuadIndices(MaxSparks);

        private readonly VertexPositionColorTexture[] _ringVerts =
            new VertexPositionColorTexture[(RingSegments + 1) * 2];
        private static readonly short[] RingIndices = BuildRingIndices();

        private Texture2D _sparkTex;
        private Texture2D _trailTex;
        private float _time = TotalDuration;
        private float _spawnAccumulator;

        public ClickPinShineEffect()
        {
            BlendState = BlendState.Additive;
            LightEnabled = false;
            IsTransparent = true;
            Hidden = true;
            BoundingBoxLocal = new BoundingBox(
                new Vector3(-120f, -120f, -10f),
                new Vector3(120f, 120f, 260f));
        }

        public override async Task Load()
        {
            await base.Load();
            await TextureLoader.Instance.Prepare(SparkTexPath);
            await TextureLoader.Instance.Prepare(TrailTexPath);
            _sparkTex = TextureLoader.Instance.GetTexture2D(SparkTexPath) ?? GraphicsManager.Instance.Pixel;
            _trailTex = TextureLoader.Instance.GetTexture2D(TrailTexPath) ?? GraphicsManager.Instance.Pixel;
        }

        /// <summary>Reinicia o efeito na posição clicada (chamado pelo CursorObject.Show).</summary>
        public void PlayAt(Vector3 position)
        {
            Position = position;
            _time = 0f;
            _spawnAccumulator = 0f;
            Hidden = false;
            Alpha = 1f;

            var rand = Random.Shared;
            for (int i = 0; i < MaxSparks; i++)
                _sparks[i].Alive = false;
            for (int i = 0; i < RingCount; i++)
            {
                // rotation3D do prefab: X e Y aleatórios 0..2π
                _ringOrientations[i] =
                    Matrix.CreateRotationX((float)rand.NextDouble() * MathHelper.TwoPi) *
                    Matrix.CreateRotationY((float)rand.NextDouble() * MathHelper.TwoPi);
            }
        }

        public override void Update(GameTime gameTime)
        {
            if (_time < TotalDuration)
            {
                float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
                _time += dt;
                UpdateSparks(dt);
                if (_time >= TotalDuration)
                    Hidden = true;
            }

            base.Update(gameTime);
        }

        private void UpdateSparks(float dt)
        {
            var rand = Random.Shared;

            if (_time < SparkEmitWindow)
            {
                _spawnAccumulator += SparkRate * dt;
                while (_spawnAccumulator >= 1f)
                {
                    _spawnAccumulator -= 1f;
                    int slot = -1;
                    for (int i = 0; i < MaxSparks; i++)
                    {
                        if (!_sparks[i].Alive) { slot = i; break; }
                    }
                    if (slot < 0)
                        break;   // maxNumParticles=20: cheio, não emite

                    _sparks[slot] = new Spark
                    {
                        Alive = true,
                        Age = 0f,
                        Size = MathHelper.Lerp(SparkMinSize, SparkMaxSize, (float)rand.NextDouble()),
                        Pos = Position + new Vector3(0f, 0f, SparkEmitZ),
                        Amp = new Vector3(
                            SparkJitter * (0.4f + 0.6f * (float)rand.NextDouble()),
                            SparkJitter * (0.4f + 0.6f * (float)rand.NextDouble()),
                            SparkJitter * (0.4f + 0.6f * (float)rand.NextDouble())),
                        Phase = new Vector3(
                            (float)rand.NextDouble() * MathHelper.TwoPi,
                            (float)rand.NextDouble() * MathHelper.TwoPi,
                            (float)rand.NextDouble() * MathHelper.TwoPi),
                    };
                }
            }

            for (int i = 0; i < MaxSparks; i++)
            {
                ref var s = ref _sparks[i];
                if (!s.Alive)
                    continue;

                s.Age += dt;
                if (s.Age >= SparkLife)
                {
                    s.Alive = false;
                    continue;
                }

                // velocity-over-lifetime: jitter oscilante ±1 un/s por eixo
                s.Pos += new Vector3(
                    s.Amp.X * MathF.Sin(SparkJitterFreq * s.Age + s.Phase.X),
                    s.Amp.Y * MathF.Sin(SparkJitterFreq * s.Age + s.Phase.Y),
                    s.Amp.Z * MathF.Sin(SparkJitterFreq * s.Age + s.Phase.Z)) * dt;
            }
        }

        /// <summary>Gradiente de alpha das faíscas (colorOverLifetime do "lizi").</summary>
        private static float SparkGradient(float lifeFrac)
        {
            if (lifeFrac <= 0.5485f)
                return MathHelper.Lerp(1f, 0.8627f, lifeFrac / 0.5485f);
            if (lifeFrac <= 0.7257f)
                return MathHelper.Lerp(0.8627f, 0f, (lifeFrac - 0.5485f) / (0.7257f - 0.5485f));
            return 0f;
        }

        /// <summary>Gradiente de alpha dos anéis (colorOverLifetime do "yuanhuan").</summary>
        private static float RingGradient(float lifeFrac)
        {
            if (lifeFrac <= 0.1412f)
                return MathHelper.Lerp(0f, 1f, lifeFrac / 0.1412f);
            if (lifeFrac <= 0.797f)
                return MathHelper.Lerp(1f, 0.9373f, (lifeFrac - 0.1412f) / (0.797f - 0.1412f));
            return MathHelper.Lerp(0.9373f, 0f, (lifeFrac - 0.797f) / (1f - 0.797f));
        }

        public override void Draw(GameTime gameTime)
        {
            if (Hidden || _time >= TotalDuration)
                return;

            var gd = GraphicsManager.Instance.GraphicsDevice;
            var effect = GraphicsManager.Instance.BasicEffect3D;
            var camera = Camera.Instance;
            if (effect == null || camera == null)
                return;

            var prevBlend = gd.BlendState;
            var prevDepth = gd.DepthStencilState;
            var prevRaster = gd.RasterizerState;
            var prevSampler = gd.SamplerStates[0];
            bool prevTexEnabled = effect.TextureEnabled;
            bool prevVcEnabled = effect.VertexColorEnabled;
            bool prevLight = effect.LightingEnabled;
            var prevTex = effect.Texture;
            var prevWorld = effect.World;
            var prevView = effect.View;
            var prevProj = effect.Projection;

            try
            {
                gd.DepthStencilState = DepthStencilState.DepthRead;
                gd.RasterizerState = RasterizerState.CullNone;
                gd.SamplerStates[0] = SamplerState.LinearClamp;

                effect.TextureEnabled = true;
                effect.VertexColorEnabled = true;
                effect.LightingEnabled = false;
                effect.World = Matrix.Identity;   // vértices em espaço de mundo
                effect.View = camera.View;
                effect.Projection = camera.Projection;

                // billboard basis (câmera), em espaço de mundo
                Vector3 toCam = camera.Position - Position;
                if (toCam.LengthSquared() < 0.001f)
                    toCam = Vector3.UnitY;
                toCam.Normalize();
                Vector3 right = Vector3.Cross(Vector3.UnitZ, toCam);
                if (right.LengthSquared() < 0.001f)
                    right = Vector3.UnitX;
                right.Normalize();
                Vector3 up = Vector3.Cross(toCam, right);
                up.Normalize();

                DrawRings(gd, effect);              // queue 3010 (por baixo)
                DrawSpike(gd, effect);              // queue 3300
                DrawSparks(gd, effect, right, up);  // queue 3300
            }
            finally
            {
                effect.TextureEnabled = prevTexEnabled;
                effect.VertexColorEnabled = prevVcEnabled;
                effect.LightingEnabled = prevLight;
                effect.Texture = prevTex;
                effect.World = prevWorld;
                effect.View = prevView;
                effect.Projection = prevProj;
                gd.BlendState = prevBlend;
                gd.DepthStencilState = prevDepth;
                gd.RasterizerState = prevRaster;
                gd.SamplerStates[0] = prevSampler;
            }
        }

        private void DrawSpike(GraphicsDevice gd, BasicEffect effect)
        {
            if (_time >= SpikeDuration)
                return;

            float alpha = SpikeAlpha.EvaluateClamped01(_time) * TotalAlpha;
            if (alpha <= 0.004f)
                return;

            float spin = SpikeSpinSpeed * _time;
            float baseZ = Position.Z + SpikeBaseZ + SpikeBounce.Evaluate(_time) * 100f;
            var color = new Color(SpikeTint.X, SpikeTint.Y, SpikeTint.Z, alpha);

            _spikeVerts[0] = new VertexPositionColorTexture(
                new Vector3(Position.X, Position.Y, baseZ + SpikeTipZ), color, Vector2.Zero);
            for (int k = 0; k < 4; k++)
            {
                float a = spin + k * MathHelper.PiOver2;
                _spikeVerts[1 + k] = new VertexPositionColorTexture(
                    new Vector3(
                        Position.X + SpikeHalfWidth * MathF.Cos(a),
                        Position.Y + SpikeHalfWidth * MathF.Sin(a),
                        baseZ + SpikeMidZ),
                    color, Vector2.Zero);
            }
            _spikeVerts[5] = new VertexPositionColorTexture(
                new Vector3(Position.X, Position.Y, baseZ + SpikeTopZ), color, Vector2.Zero);

            gd.BlendState = BlendState.Additive;   // shader oficial: SrcAlpha·One
            effect.Texture = GraphicsManager.Instance.Pixel;
            foreach (var pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                gd.DrawUserIndexedPrimitives(
                    PrimitiveType.TriangleList,
                    _spikeVerts, 0, _spikeVerts.Length,
                    SpikeIndices, 0, SpikeIndices.Length / 3);
            }
        }

        private void DrawSparks(GraphicsDevice gd, BasicEffect effect, Vector3 right, Vector3 up)
        {
            int quads = 0;
            for (int i = 0; i < MaxSparks; i++)
            {
                ref var s = ref _sparks[i];
                if (!s.Alive)
                    continue;

                float alpha = SparkBaseAlpha * SparkGradient(s.Age / SparkLife) * TotalAlpha;
                if (alpha <= 0.004f)
                    continue;

                var color = new Color(SparkTint.X, SparkTint.Y, SparkTint.Z, alpha);
                float half = s.Size * 0.5f;
                Vector3 r = right * half;
                Vector3 u = up * half;
                int v = quads * 4;
                _sparkVerts[v + 0] = new VertexPositionColorTexture(s.Pos - r + u, color, new Vector2(0f, 0f));
                _sparkVerts[v + 1] = new VertexPositionColorTexture(s.Pos + r + u, color, new Vector2(1f, 0f));
                _sparkVerts[v + 2] = new VertexPositionColorTexture(s.Pos - r - u, color, new Vector2(0f, 1f));
                _sparkVerts[v + 3] = new VertexPositionColorTexture(s.Pos + r - u, color, new Vector2(1f, 1f));
                quads++;
            }

            if (quads == 0)
                return;

            gd.BlendState = BlendState.Additive;
            effect.Texture = _sparkTex;
            foreach (var pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                gd.DrawUserIndexedPrimitives(
                    PrimitiveType.TriangleList,
                    _sparkVerts, 0, quads * 4,
                    SparkIndices, 0, quads * 2);
            }
        }

        private void DrawRings(GraphicsDevice gd, BasicEffect effect)
        {
            if (_time >= RingDuration)
                return;

            float lifeFrac = _time / RingDuration;
            float sizeMul = MathF.Max(0f, RingSize.Evaluate(lifeFrac));
            float brightness = RingGradient(lifeFrac) * TotalAlpha;
            if (sizeMul <= 0.01f || brightness <= 0.004f)
                return;

            // Blend One·One ignora alpha: o fade vai premultiplicado no RGB.
            var color = new Color(
                RingTint.X * brightness,
                RingTint.Y * brightness,
                RingTint.Z * brightness,
                1f);

            float radius = RingRadius * sizeMul;
            float halfH = RingHalfHeight * sizeMul;
            float scroll = RingUvScroll * _time;
            Vector3 center = Position + new Vector3(0f, 0f, RingEmitZ);

            gd.BlendState = AdditiveOneOne;
            gd.SamplerStates[0] = SamplerState.LinearWrap;   // streak dá a volta no anel
            effect.Texture = _trailTex;

            for (int ringIdx = 0; ringIdx < RingCount; ringIdx++)
            {
                var orient = _ringOrientations[ringIdx];
                for (int i = 0; i <= RingSegments; i++)
                {
                    float a = i / (float)RingSegments * MathHelper.TwoPi;
                    var radial = Vector3.Transform(
                        new Vector3(MathF.Cos(a) * radius, MathF.Sin(a) * radius, 0f), orient);
                    var axial = Vector3.Transform(new Vector3(0f, 0f, halfH), orient);
                    float uCoord = i / (float)RingSegments * RingUvTiles + scroll;
                    _ringVerts[i * 2 + 0] = new VertexPositionColorTexture(
                        center + radial - axial, color, new Vector2(uCoord, 0f));
                    _ringVerts[i * 2 + 1] = new VertexPositionColorTexture(
                        center + radial + axial, color, new Vector2(uCoord, 1f));
                }

                foreach (var pass in effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    gd.DrawUserIndexedPrimitives(
                        PrimitiveType.TriangleList,
                        _ringVerts, 0, _ringVerts.Length,
                        RingIndices, 0, RingSegments * 2);
                }
            }

            gd.SamplerStates[0] = SamplerState.LinearClamp;
        }

        private static short[] BuildQuadIndices(int quadCount)
        {
            var indices = new short[quadCount * 6];
            for (int q = 0; q < quadCount; q++)
            {
                int v = q * 4;
                int i = q * 6;
                indices[i + 0] = (short)(v + 0);
                indices[i + 1] = (short)(v + 1);
                indices[i + 2] = (short)(v + 2);
                indices[i + 3] = (short)(v + 2);
                indices[i + 4] = (short)(v + 1);
                indices[i + 5] = (short)(v + 3);
            }
            return indices;
        }

        private static short[] BuildRingIndices()
        {
            var indices = new short[RingSegments * 6];
            for (int i = 0; i < RingSegments; i++)
            {
                short v0 = (short)(i * 2);
                short v1 = (short)(i * 2 + 1);
                short v2 = (short)(i * 2 + 2);
                short v3 = (short)(i * 2 + 3);
                int idx = i * 6;
                indices[idx + 0] = v0; indices[idx + 1] = v1; indices[idx + 2] = v2;
                indices[idx + 3] = v2; indices[idx + 4] = v1; indices[idx + 5] = v3;
            }
            return indices;
        }

        public override void Dispose()
        {
            base.Dispose();
            _sparkTex = null;
            _trailTex = null;
        }
    }
}
