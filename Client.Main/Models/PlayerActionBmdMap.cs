using System.Collections.Generic;

namespace Client.Main.Models
{
    /// <summary>
    /// Converte o índice COMPACTO do enum PlayerAction (usado pela lógica do cliente,
    /// ex: PlayerWalkMale = 15) para o índice REAL da ação dentro do Player.bmd ORIGINAL
    /// da Webzen (layout de 380 ações, onde PlayerWalkMale = 47).
    ///
    /// Motivo: o enum foi re-indexado/compactado para um player.bmd customizado que foi
    /// removido do projeto. Hoje o cliente carrega o Player.bmd original (380 ações), então
    /// os índices compactos não batem — walk (15) caía em PlayerStopWand (pose parada),
    /// fazendo o personagem "deslizar" sem animar. Este mapa reconcilia os dois layouts.
    ///
    /// Só deve ser aplicado ao modelo do JOGADOR (Player.bmd). Monstros/NPCs usam seus
    /// próprios BMDs com layout próprio e NÃO passam por este mapa.
    /// </summary>
    public static class PlayerActionBmdMap
    {
        // compacto -> índice no Player.bmd do SEASON 21 (390 ações).
        //
        // DE/PARA calculado por alinhamento LCS entre o Player.bmd ANTIGO (MU_Red_1_20_61,
        // 380 ações) e o NOVO (Season 21, 390 ações). Foram inseridas 10 animações; o shift
        // por faixa do índice antigo é:
        //   antigo 0–7   -> +0
        //   antigo 8–37  -> +2
        //   antigo 38–59 -> +3
        //   antigo 60–272-> +4
        //   antigo 273+  -> +10
        // Cada valor abaixo já é o índice FINAL no BMD novo (comentário mostra o antigo).
        private static readonly Dictionary<int, int> _map = new()
        {
            // --- Set / Stop (idle) ---
            { 0,  0  },  // Set                    (era 0)
            { 1,  1  },  // PlayerStopMale         (era 1)
            { 2,  2  },  // PlayerStopFemale       (era 2)
            { 3,  3  },  // PlayerStopSummoner     (era 3)
            { 4,  1  },  // PlayerStopSword        (era 1)
            { 5,  12 },  // PlayerStopTwoHandSword (era 10)
            { 6,  13 },  // PlayerStopSpear        (era 11)
            { 7,  14 },  // PlayerStopScythe       (era 12)
            { 8,  15 },  // PlayerStopBow          (era 13)
            { 9,  16 },  // PlayerStopCrossbow     (era 14)
            { 10, 17 },  // PlayerStopWand         (era 15)
            { 11, 19 },  // PlayerStopFly          (era 17)
            { 12, 21 },  // PlayerStopFlyCrossbow  (era 19)
            { 13, 307 }, // PlayerStopRide         (era 297)
            { 14, 307 }, // PlayerStopRideWeapon   (era 297)

            // --- Walk ---
            { 15, 50 },  // PlayerWalkMale         (era 47) <<< principal
            { 16, 51 },  // PlayerWalkFemale       (era 48)
            { 17, 52 },  // PlayerWalkSword        (era 49)
            { 18, 53 },  // PlayerWalkTwoHandSword (era 50)
            { 19, 54 },  // PlayerWalkSpear        (era 51)
            { 20, 55 },  // PlayerWalkScythe       (era 52)
            { 21, 56 },  // PlayerWalkBow          (era 53)
            { 22, 57 },  // PlayerWalkCrossbow     (era 54)
            { 23, 58 },  // PlayerWalkWand         (era 55) <<< walk do mago
            { 24, 61 },  // PlayerWalkSwim         (era 58)

            // --- Run ---
            { 25, 79 },  // PlayerRun              (era 75)
            { 26, 80 },  // PlayerRunSword         (era 76)
            { 27, 28 },  // PlayerRunTwoSword      (era 26)
            { 28, 81 },  // PlayerRunTwoHandSword  (era 77)
            { 29, 82 },  // PlayerRunSpear         (era 78)
            { 30, 83 },  // PlayerRunBow           (era 79)
            { 31, 84 },  // PlayerRunCrossbow      (era 80)
            { 32, 62 },  // PlayerRunWand          (era 59)
            { 33, 88 },  // PlayerRunSwim          (era 84)

            // --- Fly / Ride ---
            { 34, 89 },  // PlayerFly              (era 85)
            { 35, 90 },  // PlayerFlyCrossbow      (era 86)
            { 36, 213 }, // PlayerRunRide          (era 209)
            { 37, 213 }, // PlayerRunRideWeapon    (era 209)

            // --- Attacks ---
            { 38, 111 }, // PlayerAttackFist         (era 107)
            { 39, 113 }, // PlayerAttackSwordRight1  (era 109)
            { 40, 114 }, // PlayerAttackSwordRight2  (era 110)
            { 41, 115 }, // PlayerAttackSwordLeft1   (era 111)
            { 42, 116 }, // PlayerAttackSwordLeft2   (era 112)
            { 43, 117 }, // PlayerAttackTwoHandSword1(era 113)
            { 44, 48  }, // PlayerAttackTwoHandSword2(era 45)
            { 45, 49  }, // PlayerAttackTwoHandSword3(era 46)
            { 46, 121 }, // PlayerAttackSpear1       (era 117)
            { 47, 123 }, // PlayerAttackScythe1      (era 119)
            { 48, 124 }, // PlayerAttackScythe2      (era 120)
            { 49, 125 }, // PlayerAttackScythe3      (era 121)
            { 50, 126 }, // PlayerAttackBow          (era 122)
            { 51, 127 }, // PlayerAttackCrossbow     (era 123)
            { 52, 128 }, // PlayerAttackFlyBow       (era 124)
            { 53, 129 }, // PlayerAttackFlyCrossbow  (era 125)

            // --- Skills principais (mago/comum) ---
            { 60, 137 }, // PlayerAttackSkillSword1     (era 133)
            { 62, 148 }, // PlayerAttackSkillSword3     (era 144)
            { 64, 150 }, // PlayerAttackSkillSword5     (era 146)
            { 65, 151 }, // PlayerAttackSkillWheel      (era 147)
            { 66, 152 }, // PlayerAttackSkillFuryStrike (era 148)
            { 70, 157 }, // PlayerAttackSkillSpear      (era 153)
            { 71, 158 }, // PlayerAttackDeathstab       (era 154)
            { 72, 159 }, // PlayerSkillHellBegin        (era 155)
            { 73, 160 }, // PlayerSkillHellStart        (era 156)
        };

        /// <summary>
        /// Retorna o índice real no Player.bmd para o índice compacto dado.
        /// Se não houver mapeamento, retorna o índice original (fail-safe).
        /// </summary>
        public static int ToBmd(int compactIndex)
        {
            return _map.TryGetValue(compactIndex, out var real) ? real : compactIndex;
        }
    }
}
