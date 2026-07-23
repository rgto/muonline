using System;

namespace Client.Main.Objects.Effects
{
    /// <summary>
    /// Curva de keyframes estilo Unity (Hermite cúbica com tangentes por chave).
    /// Usada para reproduzir EXATAMENTE as AnimationCurves extraídas do prefab
    /// oficial do efeito de clique do MU mobile (dianji.u3d) — nada de easing
    /// inventado: os keyframes vêm do dump do bundle.
    /// </summary>
    internal readonly struct ClickEffectCurve
    {
        // (time, value, inSlope, outSlope) por chave, tempos crescentes.
        private readonly float[][] _keys;

        public ClickEffectCurve(params float[][] keys)
        {
            _keys = keys;
        }

        public float Evaluate(float t)
        {
            var keys = _keys;
            if (keys == null || keys.Length == 0)
                return 0f;
            if (keys.Length == 1 || t <= keys[0][0])
                return keys[0][1];

            int last = keys.Length - 1;
            if (t >= keys[last][0])
                return keys[last][1];

            int i = 0;
            while (i < last && keys[i + 1][0] < t)
                i++;

            float t0 = keys[i][0], v0 = keys[i][1], m0 = keys[i][3];
            float t1 = keys[i + 1][0], v1 = keys[i + 1][1], m1 = keys[i + 1][2];
            float dt = t1 - t0;
            if (dt <= 0f)
                return v1;

            float s = (t - t0) / dt;
            float s2 = s * s;
            float s3 = s2 * s;
            float h00 = 2f * s3 - 3f * s2 + 1f;
            float h10 = s3 - 2f * s2 + s;
            float h01 = -2f * s3 + 3f * s2;
            float h11 = s3 - s2;
            return h00 * v0 + h10 * dt * m0 + h01 * v1 + h11 * dt * m1;
        }

        /// <summary>Evaluate com resultado preso em [0, 1] (curvas de alpha).</summary>
        public float EvaluateClamped01(float t) => Math.Clamp(Evaluate(t), 0f, 1f);
    }
}
