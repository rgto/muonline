using Client.Main.Controls;
using Client.Main.Controls.UI;
using Client.Main.Controls.UI.SelectCharacter;
using Client.Main.Controllers;
using Client.Main.Core.Utilities;
using Client.Main.Models;
using Client.Main.Objects;
using Client.Main.Objects.Player;
using Client.Main.Worlds;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using MUnique.OpenMU.Network.Packets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Client.Main.Scenes.SelectCharacter
{
    public class CharacterSelectionController : IDisposable
    {
        // === Private state ===
        private readonly List<PlayerObject> _characters = new();
        private readonly List<(string Name, CharacterClassNumber Class, ushort Level, byte[] Appearance)> _characterInfos = new();
        private readonly Dictionary<PlayerObject, LoginRoleLabelControl> _labels = new();
        private readonly ILogger<CharacterSelectionController> _logger;
        private int _activeIndex = -1;

        // Double-click detection
        private DateTime _lastClickTime = DateTime.MinValue;
        private string _lastClickedCharacter;
        private const double DoubleClickThresholdMs = 500;

        // Random emote
        private readonly Random _random = new();

        // === Public data (read-only) ===
        public IReadOnlyList<PlayerObject> Characters => _characters;
        public IReadOnlyDictionary<PlayerObject, LoginRoleLabelControl> Labels => _labels;

        // === State ===
        public int ActiveIndex => _activeIndex;
        public PlayerObject ActiveCharacter =>
            _activeIndex >= 0 && _activeIndex < _characters.Count
                ? _characters[_activeIndex]
                : null;

        // === Events ===
        public event EventHandler<string> CharacterClicked;
        public event EventHandler<string> CharacterDoubleClicked;
        /// <summary>Del do rótulo do personagem selecionado (btn_Delete do prefab).</summary>
        public event EventHandler<string> CharacterDeleteClicked;

        // === Constructor ===
        public CharacterSelectionController(ILogger<CharacterSelectionController> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // === Character Creation ===
        public async Task CreateCharactersAsync(
            List<(string Name, CharacterClassNumber Class, ushort Level, byte[] Appearance)> characterInfos,
            WorldControl world,
            GameControl scene,
            Vector3 displayPosition,
            Vector3 displayAngle)
        {
            _logger.LogInformation("Creating {Count} character objects...", characterInfos.Count);
            System.Console.WriteLine($"[DCPROBE] CreateCharacters START count={characterInfos.Count}");

            // Dispose old objects
            DisposeCharacters(world, scene);

            _characterInfos.Clear();
            _characterInfos.AddRange(characterInfos);
            _activeIndex = -1;

            if (characterInfos.Count == 0)
            {
                _logger.LogInformation("No characters provided for selection.");
                return;
            }

            // UM personagem por vez, no PILAR. TODOS carregam AGORA, em sequência (não
            // Task.WhenAll, pra não pico no main thread). Carregar sob demanda no clique
            // NÃO funciona: um char criado Hidden nunca entra no grid espacial de render,
            // e mostrá-lo depois (Hidden=false) não o traz pro _visibleObjects — só o 1º
            // aparecia. O "Connection lost" que motivou o lazy-load era outra causa (pacote
            // CharacterList truncado no header C1, já corrigido no servidor via C2).
            var selectWorld = world as SelectWorld;

            for (int i = 0; i < characterInfos.Count; i++)
            {
                var (name, cls, lvl, appearanceBytes) = characterInfos[i];
                var player = new PlayerObject(new AppearanceData(appearanceBytes))
                {
                    Name = name,
                    CharacterClass = cls,
                    Position = displayPosition,
                    Angle = displayAngle,
                    Interactive = true,
                    World = world,
                    CurrentAction = PlayerAction.PlayerStopMale,
                    Hidden = i != 0,          // só o primeiro aparece; os outros carregam visíveis-capazes
                };

                player.Click += OnPlayerClick;

                _characters.Add(player);
                world.Objects.Add(player);

                // Carrega TODOS agora (o modelo precisa estar carregado E ter passado pelo
                // build do _visibleObjects pra renderizar quando o slot for escolhido).
                await player.Load();

                var label = new LoginRoleLabelControl
                {
                    CharacterName = name,
                    CharacterClass = cls,
                    Level = lvl,
                    Visible = false
                };
                _labels.Add(player, label);
                scene.Controls.Add(label);
                label.BringToFront();
            }

            // Note: Cursor.BringToFront() is handled by the scene after creation

            System.Console.WriteLine("[DCPROBE] CreateCharacters DONE");
            _logger.LogInformation("Finished creating and loading character objects and labels.");

            if (_characters.Count > 0)
            {
                SetActiveCharacter(0);
            }
        }

        // Overload for TestAnimationScene compatibility (uses PlayerClass and AppearanceConfig)
        public async Task CreateCharactersAsync(
            List<(string Name, PlayerClass Class, ushort Level, AppearanceConfig Appearance)> characters,
            WorldControl world,
            GameControl scene,
            Vector3 displayPosition,
            Vector3 displayAngle)
        {
            _logger.LogInformation("Creating {Count} character objects (AppearanceConfig version)...", characters.Count);

            // Dispose old objects
            DisposeCharacters(world, scene);

            _characterInfos.Clear();
            var converted = characters.Select(p => (p.Name, (CharacterClassNumber)p.Class, p.Level, Array.Empty<byte>()));
            _characterInfos.AddRange(converted);
            _activeIndex = -1;

            if (characters.Count == 0)
            {
                _logger.LogInformation("No characters provided for selection.");
                return;
            }

            foreach (var (name, cls, lvl, appearanceConfig) in characters)
            {
                var player = new PlayerObject(new AppearanceData())
                {
                    Name = name,
                    CharacterClass = CharacterClassNumber.DarkWizard,
                    Position = displayPosition,
                    Angle = displayAngle,
                    Interactive = false,
                    World = world,
                    CurrentAction = PlayerAction.PlayerStopMale,
                    Hidden = true
                };

                player.Click += OnPlayerClick;

                _characters.Add(player);
                world.Objects.Add(player);
                await player.Load(appearanceConfig.PlayerClass);
                await player.UpdateEquipmentAppearanceFromConfig(appearanceConfig);
                DumpSelectDiag(player);

                var label = new LoginRoleLabelControl
                {
                    CharacterName = name,
                    CharacterClass = (CharacterClassNumber)cls,
                    Level = lvl,
                    Visible = false
                };

                _labels.Add(player, label);
                scene.Controls.Add(label);
                await label.Load();
                label.BringToFront();
            }

            System.Console.WriteLine("[DCPROBE] CreateCharacters DONE");
            _logger.LogInformation("Finished creating and loading character objects and labels.");

            if (_characters.Count > 0)
            {
                SetActiveCharacter(0);
            }
        }

        /// <summary>Diag dev-only (MU_SELECT_DIAG=1): lista as peças do char na seleção —
        /// caça de cabeça/arma duplicada.</summary>
        private void DumpSelectDiag(PlayerObject pl)
        {
            if (Environment.GetEnvironmentVariable("MU_SELECT_DIAG") != "1")
                return;
            var parts = new (string Tag, ModelObject Obj)[]
            {
                ("HelmMask", pl.HelmMask), ("Helm", pl.Helm), ("Armor", pl.Armor),
                ("Pants", pl.Pants), ("Gloves", pl.Gloves), ("Boots", pl.Boots),
                ("Weapon1", pl.Weapon1), ("Weapon2", pl.Weapon2), ("Wings", pl.EquippedWings),
            };
            Console.WriteLine($"[SELDIAG] === {pl.Name} class={pl.CharacterClass} ===");
            foreach (var (tag, o) in parts)
            {
                if (o == null) { Console.WriteLine($"[SELDIAG] {tag}: null"); continue; }
                Console.WriteLine($"[SELDIAG] {tag}: model={o.Model?.Name ?? "-"} hidden={o.Hidden} link={o.LinkParentAnimation} boneLink={o.ParentBoneLink} item={o.ItemDefinition?.Name ?? "-"} type={o.Type}");
            }
        }

        // === Active Character Management ===
        public void SetActiveCharacter(int index)
        {
            if (_characters.Count == 0)
            {
                _activeIndex = -1;
                return;
            }

            if (index < 0 || index >= _characters.Count)
            {
                _logger.LogWarning("Attempted to activate character at invalid index {Index}", index);
                return;
            }

            if (_activeIndex == index)
            {
                return;
            }

            // Todos ficam visíveis e clicáveis (fileira do mobile); só o Del do rótulo
            // migra pro selecionado. Esconder os outros era a divergência principal.
            for (int i = 0; i < _characters.Count; i++)
            {
                var player = _characters[i];
                if (_labels.TryGetValue(player, out var label))
                {
                    label.IsSelected = i == index;
                }
            }

            _activeIndex = index;

            // Play a random emote animation when character is selected
            var activePlayer = _characters[index];
            if (activePlayer != null && !activePlayer.Hidden)
            {
                PlayRandomEmote(activePlayer);
            }
        }

        // === Emote Animations ===
        private void PlayRandomEmote(PlayerObject player)
        {
            if (player == null || player.Hidden)
                return;

            if (player.IsOneShotPlaying)
                return;

            bool isFemale = PlayerActionMapper.IsCharacterFemale(player.CharacterClass);
            var availableEmotes = isFemale
                ? new[] { PlayerAction.PlayerSeeFemale1, PlayerAction.PlayerWinFemale1, PlayerAction.PlayerSmileFemale1 }
                : new[] { PlayerAction.PlayerSee1, PlayerAction.PlayerWin1, PlayerAction.PlayerSmile1 };

            var randomEmote = availableEmotes[_random.Next(availableEmotes.Length)];

            _logger.LogDebug("Playing random emote {Emote} for character {CharacterName} (Female: {IsFemale})",
                randomEmote, player.Name, isFemale);

            player.PlayEmoteAnimation(randomEmote);
        }

        public void PlayEmoteAnimation(PlayerAction action)
        {
            var activePlayer = ActiveCharacter;
            if (activePlayer == null || activePlayer.Hidden || activePlayer.IsOneShotPlaying)
                return;

            activePlayer.PlayEmoteAnimation(action);
        }

        // === Click Handling ===
        private void OnPlayerClick(object sender, EventArgs e)
        {
            PlayerObject clickedPlayer = null;

            if (sender is PlayerObject player)
            {
                clickedPlayer = player;
            }
            else if (sender is ModelObject bodyPart && bodyPart.Parent is PlayerObject parentPlayer)
            {
                clickedPlayer = parentPlayer;
            }

            if (clickedPlayer == null)
                return;

            // O Del fica SOBRE o personagem selecionado; sem isso, apagar viraria
            // "entrar no jogo" no duplo clique.
            if (_labels.TryGetValue(clickedPlayer, out var lbl) &&
                lbl.HitsDelete(MuGame.Instance.UiMouseState.Position))
                return;

            // Fiel à Lua (ClickDown): clicar em QUALQUER personagem da fileira o
            // seleciona. Só o duplo clique no JÁ selecionado entra no jogo.
            bool wasActive = _activeIndex >= 0 && _characters[_activeIndex] == clickedPlayer;

            DateTime now = DateTime.UtcNow;
            double timeSinceLastClick = (now - _lastClickTime).TotalMilliseconds;
            bool isDoubleClick = wasActive &&
                                timeSinceLastClick < DoubleClickThresholdMs &&
                                _lastClickedCharacter == clickedPlayer.Name;

            _lastClickTime = now;
            _lastClickedCharacter = clickedPlayer.Name;

            if (!wasActive)
                SetActiveCharacter(_characters.IndexOf(clickedPlayer));

            if (isDoubleClick)
            {
                _logger.LogInformation("Character '{Name}' double-clicked - joining game.", clickedPlayer.Name);
                CharacterDoubleClicked?.Invoke(this, clickedPlayer.Name);
            }
            else
            {
                _logger.LogInformation("Character '{Name}' clicked.", clickedPlayer.Name);
                CharacterClicked?.Invoke(this, clickedPlayer.Name);
            }
        }

        // === Cleanup ===
        private void DisposeCharacters(WorldControl world, GameControl scene)
        {
            foreach (var player in _characters)
            {
                player.Click -= OnPlayerClick;
                world?.Objects.Remove(player);
                player.Dispose();
            }
            _characters.Clear();

            foreach (var label in _labels.Values)
            {
                scene?.Controls.Remove(label);
                label.Dispose();
            }
            _labels.Clear();
        }

        /// <summary>
        /// Mostra SÓ o personagem deste nome no pilar e esconde os outros — é o que a lista
        /// de slots chama quando se clica num slot. Todos já estão carregados na mesma
        /// posição, então trocar é só ligar/desligar o Hidden (sem engasgo de load).
        /// </summary>
        public void ShowOnly(string? name)
        {
            PlayerObject? shown = null;
            foreach (var player in _characters)
            {
                bool isThis = string.Equals(player.Name, name, StringComparison.OrdinalIgnoreCase);
                player.Hidden = !isThis;
                if (isThis) shown = player;
            }

            // O toggle de Hidden força o rebuild da lista de visíveis (Object_HiddenChanged),
            // mas garantimos aqui também: objeto criado Hidden precisa reentrar no render.
            if (shown != null)
                (shown.World as WorldControl)?.InvalidateVisibleObjects();

            foreach (var (player, label) in _labels)
                label.Visible = false;   // quem mostra nome/classe/nível agora é a lista
        }

        public void Dispose()
        {
            foreach (var player in _characters)
            {
                player.Click -= OnPlayerClick;
                player.Dispose();
            }
            _characters.Clear();

            foreach (var label in _labels.Values)
            {
                label.Dispose();
            }
            _labels.Clear();

            _characterInfos.Clear();
            _activeIndex = -1;
        }
    }
}
