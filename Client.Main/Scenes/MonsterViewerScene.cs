using Client.Main.Controls;
using Client.Main.Objects;
using Client.Main.Objects.Monsters;
using Client.Main.Worlds;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using System;
using System.Threading.Tasks;

namespace Client.Main.Scenes
{
    /// <summary>
    /// Cena de desenvolvimento (desktop): spawna monstros pelo runtime REAL (mesmos
    /// caminhos de animação/GPU/attach do jogo) sem servidor. Ativada com
    /// MU_ENTRY_SCENE=MonsterViewer; monstros via MU_VIEWER_MONSTERS="6,24,36" (NpcInfo
    /// ids, default Lich/Worm/Shadow). Usada para validar os 19 rigs aninhados vendo
    /// exatamente o que o jogador vê — o validador offline não cobre o runtime.
    /// </summary>
    public class MonsterViewerScene : BaseScene
    {
        private double _shotTimer;
        private int _shotIndex;

        private double _cycleTimer;
        private int _cycleStep;
        private static readonly int[] CycleActions = { 0, 2, 3, 6 }; // Stop1, Walk, Attack1, Die

        private System.Reflection.FieldInfo _animTimeField;

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            // MU_VIEWER_FREEZE=<frame>: congela o player na ação forçada num frame exato —
            // permite comparação PAREADA glb×bmd (mesmo pixel, mesmo frame do golpe).
            string freezeStr = Environment.GetEnvironmentVariable("MU_VIEWER_FREEZE");
            if (_playerController != null && !string.IsNullOrEmpty(freezeStr) && World != null
                && double.TryParse(freezeStr, System.Globalization.NumberStyles.Float,
                                   System.Globalization.CultureInfo.InvariantCulture, out double fz))
            {
                _animTimeField ??= typeof(ModelObject).GetField("_animTime",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var forcedAct = Environment.GetEnvironmentVariable("MU_VIEWER_ACTION");
                foreach (var o in World.Objects)
                    if (o is Objects.Player.PlayerObject pl)
                    {
                        if (!string.IsNullOrEmpty(forcedAct) && Enum.TryParse<Models.PlayerAction>(forcedAct, out var ffa))
                            pl.CurrentAction = ffa;
                        _animTimeField?.SetValue(pl, fz);
                    }
            }

            // multi-char: alterna o personagem ativo a cada 5s (simula clicar entre chars
            // na tela de seleção — repro do bug do char duplicado)
            if (_playerController != null && _playerController.Characters.Count > 1
                && Environment.GetEnvironmentVariable("MU_VIEWER_CYCLE") == "1")
            {
                _playerActTimer += gameTime.ElapsedGameTime.TotalSeconds;
                if (_playerActTimer >= 5.0)
                {
                    _playerActTimer = 0;
                    int next = (_playerController.ActiveIndex + 1) % _playerController.Characters.Count;
                    _playerController.SetActiveCharacter(next);
                    Console.WriteLine($"[VIEWERCYCLE] active char={next}");
                }
            }

            // modo player: cicla ações direto no PlayerObject a cada 6s
            if (_playerController != null && _playerController.Characters.Count <= 1
                && string.IsNullOrEmpty(freezeStr)
                && Environment.GetEnvironmentVariable("MU_VIEWER_CYCLE") == "1" && World != null)
            {
                _playerActTimer += gameTime.ElapsedGameTime.TotalSeconds;
                if (_playerActTimer >= 6.0)
                {
                    _playerActTimer = 0;
                    var act = PlayerCycle[++_playerActStep % PlayerCycle.Length];
                    var forced = Environment.GetEnvironmentVariable("MU_VIEWER_ACTION");
                    if (!string.IsNullOrEmpty(forced) && Enum.TryParse<Models.PlayerAction>(forced, out var fa))
                        act = fa;
                    foreach (var o in World.Objects)
                        if (o is Objects.Player.PlayerObject pl)
                            pl.CurrentAction = act;
                    Console.WriteLine($"[VIEWERCYCLE] player action={act}");
                }
            }

            // MU_VIEWER_CYCLE=1: troca a ação de todos os monstros a cada 6s
            if (_playerController == null && Environment.GetEnvironmentVariable("MU_VIEWER_CYCLE") == "1" && World != null)
            {
                _cycleTimer += gameTime.ElapsedGameTime.TotalSeconds;
                if (_cycleTimer >= 6.0)
                {
                    _cycleTimer = 0;
                    _cycleStep++;
                    int act = CycleActions[_cycleStep % CycleActions.Length];
                    foreach (var o in World.Objects)
                        if (o is Objects.MonsterObject m)
                            m.CurrentAction = act;
                    Console.WriteLine($"[VIEWERCYCLE] action={act}");
                }
            }

            // Auto-screenshot do backbuffer (MU_VIEWER_SHOT_DIR): captura determinística
            // para inspecionar o render real sem depender de captura de desktop.
            string dir = Environment.GetEnvironmentVariable("MU_VIEWER_SHOT_DIR");
            int maxShots = int.TryParse(Environment.GetEnvironmentVariable("MU_VIEWER_SHOT_MAX"), out var ms) ? ms : 6;
            double interval = double.TryParse(Environment.GetEnvironmentVariable("MU_VIEWER_SHOT_INTERVAL"), out var si) ? si : 8.0;
            if (string.IsNullOrEmpty(dir) || _shotIndex >= maxShots)
                return;
            _shotTimer += gameTime.ElapsedGameTime.TotalSeconds;
            if (_shotTimer < interval)
                return;
            _shotTimer = 0;
            try
            {
                var gd = MuGame.Instance.GraphicsDevice;
                int w = gd.PresentationParameters.BackBufferWidth;
                int h = gd.PresentationParameters.BackBufferHeight;
                var data = new Microsoft.Xna.Framework.Color[w * h];
                gd.GetBackBufferData(data);
                using var tex = new Microsoft.Xna.Framework.Graphics.Texture2D(gd, w, h);
                tex.SetData(data);
                System.IO.Directory.CreateDirectory(dir);
                using var fs = System.IO.File.Create(System.IO.Path.Combine(dir, $"shot_{_shotIndex++}.png"));
                tex.SaveAsPng(fs, w, h);
                Console.WriteLine($"[VIEWERSHOT] salvo shot_{_shotIndex - 1}.png");
            }
            catch (Exception ex) { Console.WriteLine($"[VIEWERSHOT] FAIL {ex.Message}"); }
        }

        private SelectCharacter.CharacterSelectionController _playerController;
        private double _playerActTimer;
        private int _playerActStep;
        private static readonly Models.PlayerAction[] PlayerCycle =
        {
            Models.PlayerAction.PlayerStopMale,
            Models.PlayerAction.PlayerWalkMale,
            Models.PlayerAction.PlayerAttackSwordRight1,
            Models.PlayerAction.PlayerDie1,
        };

        /// <summary>Modo PLAYER (MU_VIEWER_PLAYER=DarkKnight etc.): personagem da classe com
        /// equipamento default da classe, no SelectWorld — valida as peças glb no runtime real.</summary>
        private async Task LoadPlayerViewer(string className)
        {
            var world = new Worlds.SelectWorld();
            Controls.Add(world);
            await world.Initialize();
            World = world;

            _playerController = new SelectCharacter.CharacterSelectionController(
                MuGame.AppLoggerFactory.CreateLogger<SelectCharacter.CharacterSelectionController>());
            world.SetController(_playerController);

            // Aceita lista ("DarkKnight,DarkWizard") — reproduz a tela de seleção com
            // vários personagens no MESMO controller (bug do char duplicado).
            var classNames = className.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var chars = new System.Collections.Generic.List<(string, Models.PlayerClass, ushort, Core.Utilities.AppearanceConfig)>();
            foreach (var cn in classNames)
            {
                if (!Enum.TryParse<Models.PlayerClass>(cn, out var pcx))
                    pcx = Models.PlayerClass.DarkKnight;
                var ac = new Core.Utilities.AppearanceConfig
                {
                    PlayerClass = pcx,
                    HelmItemIndex = 0xFFFF,
                    ArmorItemIndex = 0xFFFF,
                    PantsItemIndex = 0xFFFF,
                    GlovesItemIndex = 0xFFFF,
                    BootsItemIndex = 0xFFFF,
                    LeftHandItemIndex = 0xff,
                    RightHandItemIndex = 0xff,
                    WingInfo = new Models.WingAppearance(0, 0, -1),
                    RidingVehicle = -1,
                };
                if (int.TryParse(Environment.GetEnvironmentVariable("MU_VIEWER_EQUIP"), out int eqSet))
                {
                    ac.HelmItemIndex = eqSet;
                    ac.ArmorItemIndex = eqSet;
                    ac.PantsItemIndex = eqSet;
                    ac.GlovesItemIndex = eqSet;
                    ac.BootsItemIndex = eqSet;
                }
                chars.Add(($"Piloto{chars.Count + 1}", pcx, (ushort)1, ac));
            }
            if (chars.Count > 1)
            {
                await _playerController.CreateCharactersAsync(
                    chars, world, this, world.CharacterDisplayPosition, world.CharacterDisplayAngle);
                return;
            }

            if (!Enum.TryParse<Models.PlayerClass>(className, out var pc))
                pc = Models.PlayerClass.DarkKnight;

            var appearance = new Core.Utilities.AppearanceConfig
            {
                PlayerClass = pc,
                HelmItemIndex = 0xFFFF,
                ArmorItemIndex = 0xFFFF,
                PantsItemIndex = 0xFFFF,
                GlovesItemIndex = 0xFFFF,
                BootsItemIndex = 0xFFFF,
                LeftHandItemIndex = 0xff,
                RightHandItemIndex = 0xff,
                WingInfo = new Models.WingAppearance(0, 0, -1),
                RidingVehicle = -1,
            };

            // MU_VIEWER_EQUIP=<setId>: veste o set (id nos grupos 7-11) em todas as 5 peças.
            // MU_VIEWER_RHAND / MU_VIEWER_LHAND="grp:idx": item na mão (ex. staff DW = 5:61).
            if (int.TryParse(Environment.GetEnvironmentVariable("MU_VIEWER_EQUIP"), out int setId))
            {
                appearance.HelmItemIndex = setId;
                appearance.ArmorItemIndex = setId;
                appearance.PantsItemIndex = setId;
                appearance.GlovesItemIndex = setId;
                appearance.BootsItemIndex = setId;
            }
            void ParseHand(string env, bool right)
            {
                var v = Environment.GetEnvironmentVariable(env)?.Split(':');
                if (v == null || v.Length < 2 || !byte.TryParse(v[0], out byte g) || !byte.TryParse(v[1], out byte ix))
                    return;
                if (right) { appearance.RightHandItemGroup = g; appearance.RightHandItemIndex = ix; }
                else { appearance.LeftHandItemGroup = g; appearance.LeftHandItemIndex = ix; }
            }
            ParseHand("MU_VIEWER_RHAND", true);
            ParseHand("MU_VIEWER_LHAND", false);
            await _playerController.CreateCharactersAsync(
                [("Piloto", pc, (ushort)1, appearance)],
                world, this, world.CharacterDisplayPosition, world.CharacterDisplayAngle);
        }

        public override async Task Load()
        {
            string playerClass = Environment.GetEnvironmentVariable("MU_VIEWER_PLAYER");
            if (!string.IsNullOrEmpty(playerClass))
            {
                await LoadPlayerViewer(playerClass);
                await base.Load();
                return;
            }

            // MU_FLAGS="DLS=1,OPT=0,MM=1,DL=1,IM=1": força flags de render p/ bisseção
            var flags = Environment.GetEnvironmentVariable("MU_FLAGS");
            if (!string.IsNullOrEmpty(flags))
                foreach (var kv in flags.Split(','))
                {
                    var p2 = kv.Split('=');
                    if (p2.Length != 2) continue;
                    bool on = p2[1] == "1";
                    switch (p2[0])
                    {
                        case "DLS": Constants.ENABLE_DYNAMIC_LIGHTING_SHADER = on; break;
                        case "OPT": Constants.OPTIMIZE_FOR_INTEGRATED_GPU = on; break;
                        case "MM": Constants.ENABLE_MONSTER_MATERIAL_SHADER = on; break;
                        case "DL": Constants.ENABLE_DYNAMIC_LIGHTS = on; break;
                        case "IM": Constants.ENABLE_ITEM_MATERIAL_SHADER = on; break;
                        case "TGL": Constants.ENABLE_TERRAIN_GPU_LIGHTING = on; break;
                    }
                }

            var world = new DeviasWorld();
            Controls.Add(world);

            // Walker é obrigatório (câmera + sons usam Walker.Position). Um WalkerObject
            // parado no meio dos monstros serve.
            var anchor = new Worm { Location = new Vector2(225, 124) };
            world.Walker = anchor;

            await world.Initialize();
            World = world;

            anchor.World = world;
            world.Objects.Add(anchor);
            await anchor.Load();

            var spec = Environment.GetEnvironmentVariable("MU_VIEWER_MONSTERS") ?? "6,24,36";
            int slot = 0;
            foreach (var part in spec.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!ushort.TryParse(part, out ushort typeId))
                    continue;
                if (!Core.Utilities.NpcDatabase.TryGetNpcType(typeId, out var npcClassType))
                    continue;
                if (Activator.CreateInstance(npcClassType) is not WalkerObject obj)
                    continue;

                obj.Location = new Vector2((ushort)(222 + (slot % 4) * 3), (ushort)(121 + (slot / 4) * 3));
                obj.World = world;
                world.Objects.Add(obj);
                await obj.Load();
                if (obj is ModelObject mo)
                    Console.WriteLine($"[VIEWERLOAD] type={typeId} model={mo.Model?.Name} isGltf={mo.Model?.IsGltf} bufDiagEnv={Environment.GetEnvironmentVariable("MU_BUF_DIAG")}");
                slot++;
            }

            await base.Load();
        }
    }
}
