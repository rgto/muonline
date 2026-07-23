#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Client.Data.BMD;
using Microsoft.Extensions.Logging;

namespace Client.Main.Core.Utilities
{
    /// <summary>
    /// Static database for skill definitions loaded from skill_eng.bmd.
    /// </summary>
    public static class SkillDatabase
    {
        private static readonly ILogger? _logger = MuGame.AppLoggerFactory?.CreateLogger("SkillDatabase");

        /// <summary>Lookup cache: SkillId → skill definition.</summary>
        private static Dictionary<int, SkillBMD> _skillDefinitions = [];

        /// <summary>Lookup cache: SkillId → textos de tooltip (skilltooltiptext.bmd).</summary>
        private static Dictionary<int, SkillTooltip> _skillTooltips = [];

        public static async Task Initialize()
        {
            _skillDefinitions = await InitializeSkillData();
            _skillTooltips = await InitializeTooltipData();
        }

        /// <summary>
        /// Loads Data/Local/skill.bmd and builds the definition table.
        /// </summary>
        private static async Task<Dictionary<int, SkillBMD>> InitializeSkillData()
        {
            var skillPath = Path.Combine(Constants.DataPath, "Local", "skill.bmd");
            var reader = new SkillBMDReader();
            var skills = await reader.Load(skillPath);
            _logger?.LogInformation($"Loaded {skills.Count} skills from skill.bmd");
            return skills;
        }

        /// <summary>
        /// Loads Data/Local/skilltooltiptext.bmd (descrições oficiais das skills).
        /// Opcional: Data antigo pode não ter o arquivo — segue sem descrições.
        /// </summary>
        private static async Task<Dictionary<int, SkillTooltip>> InitializeTooltipData()
        {
            var tooltipPath = Path.Combine(Constants.DataPath, "Local", "skilltooltiptext.bmd");
            try
            {
                var reader = new SkillTooltipReader();
                var tooltips = await reader.Load(tooltipPath);
                _logger?.LogInformation($"Loaded {tooltips.Count} skill tooltips from skilltooltiptext.bmd");
                return tooltips;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning($"Skill tooltips unavailable ({ex.Message})");
                return [];
            }
        }

        #region Public API ------------------------------------------------------

        /// <summary>
        /// Gets skill definition by skill ID.
        /// </summary>
        public static SkillBMD? GetSkillDefinition(int skillId)
        {
            _skillDefinitions.TryGetValue(skillId, out var def);
            return def;
        }

        /// <summary>
        /// Gets skill name by skill ID. The local skill.bmd of this Data (KR client,
        /// S20 record layout) misparses under the S6 reader — names come out as XOR-phase
        /// garbage ("W3dW3d…"). The server speaks S6, so the English table generated from
        /// OpenMU's SkillNumber enum is authoritative here; the bmd name is only a fallback
        /// for ids outside the table.
        /// </summary>
        public static string GetSkillName(int skillId)
        {
            if (SkillNames.TryGet(skillId, out var name))
                return name;
            return GetSkillDefinition(skillId)?.Name ?? $"Skill {skillId}";
        }

        /// <summary>
        /// Textos de tooltip da skill (skilltooltiptext.bmd) no idioma do Data instalado.
        /// </summary>
        public static SkillTooltip? GetSkillTooltip(int skillId)
        {
            _skillTooltips.TryGetValue(skillId, out var tooltip);
            return tooltip;
        }

        /// <summary>
        /// Gets skill type (AREA/TARGET/SELF) by skill ID.
        /// </summary>
        public static SkillType GetSkillType(int skillId) =>
            SkillDefinitions.GetSkillType(skillId);

        /// <summary>
        /// Gets animation ID for skill by skill ID.
        /// Returns -1 if no specific animation.
        /// </summary>
        public static int GetSkillAnimation(int skillId) =>
            SkillDefinitions.GetSkillAnimation(skillId);

        /// <summary>
        /// Checks if skill is area type.
        /// </summary>
        public static bool IsAreaSkill(int skillId) =>
            SkillDefinitions.IsAreaSkill(skillId);

        /// <summary>
        /// Checks if skill is target type.
        /// </summary>
        public static bool IsTargetSkill(int skillId) =>
            SkillDefinitions.IsTargetSkill(skillId);

        /// <summary>
        /// Checks if skill is self-cast type.
        /// </summary>
        public static bool IsSelfSkill(int skillId) =>
            SkillDefinitions.IsSelfSkill(skillId);

        /// <summary>
        /// Gets all loaded skills.
        /// </summary>
        public static IReadOnlyDictionary<int, SkillBMD> GetAllSkills() => _skillDefinitions;

        /// <summary>
        /// Gets skill mana cost.
        /// </summary>
        public static ushort GetSkillManaCost(int skillId) =>
            GetSkillDefinition(skillId)?.ManaCost ?? 0;

        /// <summary>
        /// Gets skill AG cost.
        /// </summary>
        public static ushort GetSkillAGCost(int skillId) =>
            GetSkillDefinition(skillId)?.AbilityGaugeCost ?? 0;

        /// <summary>
        /// Gets skill range/distance.
        /// </summary>
        public static uint GetSkillRange(int skillId) =>
            GetSkillDefinition(skillId)?.Distance ?? 0;

        /// <summary>
        /// Gets skill cooldown delay in milliseconds.
        /// </summary>
        public static int GetSkillCooldown(int skillId) =>
            GetSkillDefinition(skillId)?.Delay ?? 0;

        /// <summary>
        /// Gets required level for skill.
        /// </summary>
        public static ushort GetRequiredLevel(int skillId) =>
            GetSkillDefinition(skillId)?.RequiredLevel ?? 0;

        #endregion
    }
}
