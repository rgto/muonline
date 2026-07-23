#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Xna.Framework;

namespace Client.Main.Core.Utilities
{
    /// <summary>
    /// Giro extra (graus) por modelo de busto da tela de criação, lido de
    /// Data/Local/LoginRole/face_facing.json em runtime.
    ///
    /// Por que isto existe: cada prefab newfaceNN foi modelado com o corpo num ângulo
    /// diferente, e esse ângulo NÃO é dedutível dos ossos do rig — a Fairy Elf e o Dark Lord
    /// saem de frente com giro 0, enquanto os outros precisam de correção própria. Tentar
    /// derivar isso de uma medição (eixo da cabeça, linha dos ombros) girava também os que já
    /// estavam certos. Então o valor é calibrado NA TELA, por modelo.
    ///
    /// Fica no Data e não no código pelo mesmo motivo do resto da tela: se o MU trocar os
    /// assets, ajusta-se o json — sem recompilar o cliente.
    /// Modelo ausente ou arquivo faltando = 0 (não mexe).
    /// </summary>
    public static class FaceFacing
    {
        private const string RelativePath = "Local/LoginRole/face_facing.json";

        private static Dictionary<string, Vector3>? _yaw;
        private static Dictionary<string, float>? _side;
        private static DateTime _stamp;

        public static void Initialize()
        {
            var map = new Dictionary<string, Vector3>(StringComparer.OrdinalIgnoreCase);
            var side = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

            try
            {
                string path = Path.Combine(Constants.DataPath ?? string.Empty, RelativePath);
                if (File.Exists(path))
                {
                    _stamp = File.GetLastWriteTimeUtc(path);
                    using var doc = JsonDocument.Parse(File.ReadAllText(path));

                    // "side": deslocamento lateral POR MODELO, somado ao FaceSideOffset da
                    // cena. Existe porque os bustos não são modelados no mesmo eixo: a
                    // Summoner nasce mais à esquerda que os outros e só ela precisa correr.
                    if (doc.RootElement.TryGetProperty("side", out var sideEl)
                        && sideEl.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var s in sideEl.EnumerateObject())
                            if (s.Value.ValueKind == JsonValueKind.Number)
                                side[s.Name] = s.Value.GetSingle();
                    }

                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        if (prop.NameEquals("side")) continue;   // tratado acima
                        // Número = só o giro em Z (a esmagadora maioria dos casos).
                        if (prop.Value.ValueKind == JsonValueKind.Number)
                        {
                            map[prop.Name] = new Vector3(0f, 0f, prop.Value.GetSingle());
                        }
                        // Array [x, y, z] = giro nos 3 eixos, pra quem precisa de inclinação
                        // além do giro lateral (ex.: o Dark Wizard, que vem com a cabeça
                        // tombada e nenhum valor de Z sozinho endireita).
                        else if (prop.Value.ValueKind == JsonValueKind.Array)
                        {
                            var v = new float[3];
                            int i = 0;
                            foreach (var e in prop.Value.EnumerateArray())
                            {
                                if (i >= 3) break;
                                v[i++] = e.GetSingle();
                            }
                            map[prop.Name] = new Vector3(v[0], v[1], v[2]);
                        }
                        // "_comment"/"_classes" (strings) caem fora, como deve ser.
                    }
                }
            }
            catch (Exception ex)
            {
                // Sem o arquivo todo mundo fica com giro 0 — pior enquadramento, nunca crash.
                Console.WriteLine($"[FaceFacing] falha lendo {RelativePath}: {ex.Message}");
            }

            _yaw = map;
            _side = side;
        }

        /// <summary>
        /// Giro extra do modelo, em graus, por eixo (X/Y = inclinação, Z = giro lateral).
        /// Vector3.Zero quando não há calibração.
        /// </summary>
        public static Vector3 GetExtraRotation(string? model)
        {
            // Relê quando o arquivo muda: permite calibrar o ângulo com o jogo aberto, sem
            // rebuild nem restart. (Sem isto, o cache lia uma vez só e as trocas do arquivo
            // eram silenciosamente ignoradas — dá pra passar horas ajustando à toa.)
            if (_yaw == null || FileChanged())
                Initialize();

            if (string.IsNullOrEmpty(model) || _yaw == null)
                return Vector3.Zero;

            return _yaw.TryGetValue(model, out var rot) ? rot : Vector3.Zero;
        }

        /// <summary>
        /// Deslocamento lateral extra DESTE modelo, em unidades de mundo, somado ao
        /// FaceSideOffset da cena (positivo = mais pra esquerda da linha de visada, igual ao
        /// da cena). 0 quando o Data não traz o modelo.
        ///
        /// Existe porque os bustos não compartilham o eixo: a maioria fica certa com o offset
        /// global, mas a Summoner nasce deslocada e só ela precisa correr — mover o global
        /// pra corrigi-la desalinharia as outras cinco.
        /// </summary>
        public static float GetSideOffset(string? model)
        {
            if (_side == null || FileChanged())
                Initialize();

            if (string.IsNullOrEmpty(model) || _side == null)
                return 0f;

            return _side.TryGetValue(model, out var v) ? v : 0f;
        }

        private static bool FileChanged()
        {
            try
            {
                string path = Path.Combine(Constants.DataPath ?? string.Empty, RelativePath);
                return File.Exists(path) && File.GetLastWriteTimeUtc(path) != _stamp;
            }
            catch
            {
                return false;
            }
        }
    }
}
