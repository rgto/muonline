#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Client.Main.Core.Utilities
{
    /// <summary>
    /// Uma classe jogável oferecida na tela de criação, como o Data descreve.
    /// </summary>
    public sealed class CharacterCreateClass
    {
        /// <summary>Id do config do mobile (11 = Dark Knight, 12 = Dark Wizard, ...).</summary>
        public int Id { get; init; }

        /// <summary>Nome de exibição (name_en do Data).</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>Prosa da classe (description_en do Data).</summary>
        public string Description { get; init; } = string.Empty;

        /// <summary>Prefab do rosto 3D — "newface02". Sem extensão nem pasta.</summary>
        public string Model { get; init; } = string.Empty;

        /// <summary>
        /// Atributos na ordem do Data, já com o rótulo resolvido: [("STR", 28), ...].
        /// A LISTA é a fonte da verdade, não um struct de 4 campos fixos: o Dark Lord
        /// traz CHA no lugar de INT.
        /// </summary>
        public IReadOnlyList<(string Label, int Value)> Attributes { get; init; } =
            Array.Empty<(string, int)>();

        /// <summary>Pose do modelo no visor 3D (modelPosition/Rotation/Scale do Data).</summary>
        public (float X, float Y, float Z) ModelPosition { get; init; }
        public (float X, float Y, float Z) ModelRotation { get; init; }
        public (float X, float Y, float Z) ModelScale { get; init; }
    }

    /// <summary>
    /// Classes da tela de criação de personagem, lidas do Data em runtime.
    ///
    /// Fonte: Data/Local/LoginRole/cfg_Character_create.json (+ ui_word.json para os
    /// rótulos de atributo), gerados por tools/ui-pipeline/bake_loginrole_data.py a
    /// partir do config do cliente mobile. Nada aqui é hardcoded: Data novo da Webzen
    /// = re-rodar o script, sem recompilar.
    /// </summary>
    public static class CharacterCreateDatabase
    {
        private static readonly ILogger? _logger =
            MuGame.AppLoggerFactory?.CreateLogger(nameof(CharacterCreateDatabase));

        private static IReadOnlyList<CharacterCreateClass> _classes = Array.Empty<CharacterCreateClass>();
        private static IReadOnlyDictionary<string, string> _words = new Dictionary<string, string>();

        /// <summary>Classes na ordem do Data. Vazia se o Data não trouxer o config.</summary>
        public static IReadOnlyList<CharacterCreateClass> Classes => _classes;

        public static async Task Initialize()
        {
            var dir = Path.Combine(Constants.DataPath, "Local", "LoginRole");
            _words = await LoadWords(Path.Combine(dir, "ui_word.json"));
            _classes = await LoadClasses(Path.Combine(dir, "cfg_Character_create.json"));
            _logger?.LogInformation("Loaded {Count} creatable classes from Data", _classes.Count);
        }

        /// <summary>
        /// Texto de UI pelo id do cfg_Ui_word ("zhishao" → "At least two characters...").
        /// Devolve o próprio id se o Data não tiver a chave — some na tela, mas não quebra.
        /// </summary>
        public static string GetWord(string id) =>
            _words.TryGetValue(id, out var w) ? w : id;

        private static async Task<IReadOnlyDictionary<string, string>> LoadWords(string path)
        {
            try
            {
                var json = await File.ReadAllTextAsync(path);
                return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                       ?? new Dictionary<string, string>();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning("ui_word.json unavailable ({Message})", ex.Message);
                return new Dictionary<string, string>();
            }
        }

        private static async Task<IReadOnlyList<CharacterCreateClass>> LoadClasses(string path)
        {
            try
            {
                var json = await File.ReadAllTextAsync(path);
                using var doc = JsonDocument.Parse(json);
                return doc.RootElement.EnumerateArray().Select(ParseClass).ToList();
            }
            catch (Exception ex)
            {
                _logger?.LogError("cfg_Character_create.json unavailable ({Message})", ex.Message);
                return Array.Empty<CharacterCreateClass>();
            }
        }

        private static CharacterCreateClass ParseClass(JsonElement e) => new()
        {
            Id = Str(e, "id") is { Length: > 0 } s && int.TryParse(s, out var id) ? id : Num(e, "id"),
            // O Data tem espaço sobrando em alguns nomes ("Dark Lord ").
            Name = Str(e, "name_en").Trim(),
            Description = CleanProse(Str(e, "description_en")),
            Model = Str(e, "model"),
            Attributes = ParseAttributes(Str(e, "attCount")),
            ModelPosition = ParseVector(Str(e, "modelPosition")),
            ModelRotation = ParseVector(Str(e, "modelRotation")),
            ModelScale = ParseVector(Str(e, "modelScale")),
        };

        /// <summary>
        /// "liliang#28&amp;minjie#20&amp;tili#25&amp;zhili#10" → [("STR",28), ("AGI",20), ...].
        /// A ordem do Data é preservada — é a ordem que a tela desenha.
        /// </summary>
        private static IReadOnlyList<(string, int)> ParseAttributes(string raw)
        {
            var list = new List<(string, int)>();
            if (string.IsNullOrEmpty(raw))
                return list;

            foreach (var pair in raw.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = pair.Split('#');
                if (kv.Length == 2 && int.TryParse(kv[1], out var value))
                    list.Add((GetWord(kv[0]), value));
            }
            return list;
        }

        /// <summary>"-44#-197#100" → (-44, -197, 100).</summary>
        private static (float, float, float) ParseVector(string raw)
        {
            var p = raw.Split('#');
            if (p.Length != 3)
                return (0, 0, 0);
            return (Parse(p[0]), Parse(p[1]), Parse(p[2]));

            static float Parse(string s) =>
                float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0f;
        }

        /// <summary>
        /// O Data usa vírgula ideográfica (U+FF0C) mesmo nos textos EN, e a fonte do
        /// cliente não tem esse glifo. Troca pelo equivalente ASCII.
        /// </summary>
        private static string CleanProse(string s) =>
            s.Replace('，', ',').Replace('、', ',').Trim();

        private static string Str(JsonElement e, string name) =>
            e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
                ? v.GetString() ?? string.Empty
                : string.Empty;

        private static int Num(JsonElement e, string name) =>
            e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number
                ? v.GetInt32()
                : 0;
    }
}
