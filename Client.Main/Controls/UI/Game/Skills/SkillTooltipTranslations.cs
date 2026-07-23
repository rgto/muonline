using System.Collections.Generic;
using System.Text;

namespace Client.Main.Controls.UI.Game.Skills
{
    /// <summary>
    /// Overlay de tradução EN das descrições de skill do CLIENTE (Data/Local/
    /// skilltooltiptext.bmd, client KR — os textos originais são EUC-KR coreano).
    /// A chave é o hash FNV-1a 64 do texto ORIGINAL do arquivo: se a Webzen mudar
    /// uma descrição num Data novo, o hash não bate e o painel mostra o texto novo
    /// cru do arquivo (nunca uma tradução desatualizada). Os NÚMEROS da skill nunca
    /// passam por aqui — vêm sempre do skill.bmd em runtime.
    /// GERADO por script (scratchpad gen_translations_cs.py) — não editar à mão.
    /// </summary>
    internal static class SkillTooltipTranslations
    {
        /// <summary>
        /// Resolve o texto do tooltip: tradução EN quando o original é conhecido;
        /// senão o próprio texto do arquivo ('#' vira espaço — quebra de linha do
        /// cliente oficial). Null quando não há texto renderizável (fonte da UI não
        /// tem os glifos do idioma do Data, ex.: hangul sem tradução).
        /// </summary>
        public static string Resolve(string original)
        {
            if (string.IsNullOrWhiteSpace(original))
                return null;
            if (Translations.TryGetValue(Fnv1a64(original), out var translated))
                return translated;
            var raw = original.Replace('#', ' ');
            foreach (var ch in raw)
            {
                if (ch > '\u04FF') // fora do atlas da fonte da UI (latim/cirílico)
                    return null;
            }
            return raw;
        }

        private static ulong Fnv1a64(string text)
        {
            ulong hash = 14695981039346656037UL;
            foreach (var b in Encoding.UTF8.GetBytes(text))
            {
                hash ^= b;
                hash *= 1099511628211UL;
            }
            return hash;
        }

        private static readonly Dictionary<ulong, string> Translations = new()
        {
            [0x4B50698F4A682547UL] = "Attacks the target with poison, with a chance to inflict poison damage over time.", // 1
            [0x0A0F0A11A0501322UL] = "Drops a huge meteorite from the sky, dealing damage to the target.", // 2
            [0xACC0988866A748DCUL] = "Strikes the target with a bolt of lightning, with a chance to push the target back.", // 3
            [0xCD19BA9D0BFDC10BUL] = "Hurls a burning rock that deals damage to the target.", // 4
            [0xE25E11E6EE2BE50EUL] = "Creates a pillar of flame, dealing damage to the target and nearby enemies.", // 5
            [0xAEFAE41D3F9305BAUL] = "Conceals your body for a moment and teleports you to another location.", // 6
            [0xB1BFA915EFE3D7E6UL] = "An ice attack that damages the target, with a chance to slow its movement speed.", // 7
            [0xFAD80FC9F72974FAUL] = "Summons a whirling gale in the chosen direction, damaging multiple targets in its path.", // 8
            [0x409F8C5E5D893696UL] = "Releases dark energy, dealing damage to multiple targets around you.", // 9
            [0xA37F46177B78A768UL] = "Casts a fire spell on the ground, dealing damage to multiple targets around you.", // 10
            [0x2D4699815D9DD9FFUL] = "Unleashes an electric wave that deals damage to the target.", // 11
            [0x111BDAB595578633UL] = "Releases water-attribute light energy in a straight line, damaging multiple targets in the chosen direction.", // 12
            [0xDA8AD6E7D0575694UL] = "Drops a mass of light from the sky onto the target and nearby enemies, dealing damage.", // 13
            [0xAD85EBDCBA348084UL] = "Creates a burning ring around you, dealing damage to nearby targets.", // 14
            [0x9E5AB8F87C880A27UL] = "Forcibly moves a distant ally to your location.", // 15
            [0x7CC15AD7FF527949UL] = "Creates a mana shield that consumes mana to reduce the damage taken by you or an ally.", // 16
            [0x088C2789488C1169UL] = "Fires a sphere of condensed energy, dealing damage to the target.", // 17
            [0xD1586782B30F5539UL] = "Protects yourself with your equipped shield, temporarily reducing damage taken.", // 18
            [0x8F029855BB8AA2E7UL] = "Swiftly strikes down with your weapon, dealing damage to the target.", // 19
            [0x2803B99A1075E56CUL] = "Swiftly stabs the target with a sharp weapon, dealing damage.", // 20
            [0x882E1A00DB0B2E58UL] = "Strikes upward with your weapon, dealing damage to the target.", // 21
            [0x68E93026A77539AAUL] = "Strikes with a spinning blow, dealing damage to the target.", // 22
            [0xE0A78A84EB468295UL] = "Slashes the target with your weapon, dealing damage.", // 23
            [0x6C4D69C23EA90CAAUL] = "Fires multiple arrows in a fan shape, damaging multiple targets.", // 24
            [0xEE05CF190DE586DFUL] = "Restores the life of the chosen target.", // 26
            [0xBC7BBD9479619C35UL] = "Temporarily increases the defense of the chosen target.", // 27
            [0xA447007C663A8065UL] = "Temporarily increases the attack power and wizardry of the chosen target.", // 28
            [0x1E8EE44A8B1D0A58UL] = "Summons a Goblin to fight by your side. It disappears when its life runs out.", // 30
            [0xBE56BBFAC6EF5144UL] = "Summons a Stone Golem to fight by your side. It disappears when its life runs out.", // 31
            [0xEEEF159AF0EFADFBUL] = "Summons an Assassin to fight by your side. It disappears when its life runs out.", // 32
            [0x973E65AB7F743D3EUL] = "Summons an Elite Yeti to fight by your side. It disappears when its life runs out.", // 33
            [0x316D15EC45BF391BUL] = "Summons a Dark Knight to fight by your side. It disappears when its life runs out.", // 34
            [0xF6C4ECC9D6971E87UL] = "Summons a Bali to fight by your side. It disappears when its life runs out.", // 35
            [0x80BA8ED50A8BD42BUL] = "Summons a Soldier to fight by your side. It disappears when its life runs out.", // 36
            [0x96FBFDD35A985D55UL] = "Drops a venomous meteorite, damaging multiple targets with a chance to inflict poison damage.", // 38
            [0xAE234C35841BC372UL] = "Drops ice shards, damaging multiple targets with a chance to reduce their movement speed.", // 39
            [0x995537734F264FF3UL] = "Gathers the light energy around you and detonates it in an instant, damaging multiple targets. The longer the light is gathered, the more damage it deals.", // 40
            [0xCE10C2CE604BD73BUL] = "Swings your equipped weapon, dealing damage to multiple targets around you.", // 41
            [0x07077FD046BE385BUL] = "Slams your weapon into the ground, causing an earthquake that damages multiple nearby targets.", // 42
            [0x4F8A1C95CA6DA89DUL] = "Creates a wave of wind, damaging the target and enemies in a straight line.", // 43
            [0x387FD597B640C5E3UL] = "Swiftly strikes down with your weapon, dealing damage to the target.", // 44
            [0xA5F36EC08028FB9BUL] = "Throws a spinning blade, dealing damage to the target.", // 45
            [0x4061E0E1EC084882UL] = "Shoots an arrow high into the sky, dealing damage to the target.", // 46
            [0x9D283C24316C272DUL] = "While mounted, swiftly stabs with a long spear, dealing damage to the target.", // 47
            [0xEAF8102CD8EEED2CUL] = "Increases the life of you and your party members for a set duration.", // 48
            [0x00555F2DC8099C08UL] = "While mounted, launches a sword wave, dealing damage to the target.", // 49
            [0xBBFD9703AE7F703AUL] = "Fires an ice arrow that damages the target, with a chance to freeze it in place.", // 51
            [0xF44F22523314AD7CUL] = "Fires an arrow that pierces the target, damaging enemies in a straight line.", // 52
            [0x5EAE4184907F11ACUL] = "Swings a sword imbued with dark energy, damaging the target and reducing its defense for a set duration.", // 55
            [0x8207264EC9B62EFBUL] = "Swings your sword to launch condensed wind force forward, damaging multiple targets.", // 56
            [0x855D204A48068D31UL] = "Swiftly swings your sword, dealing damage to the target.", // 57
            [0xDFC8C8EDDC58E1E0UL] = "Creates a wave of wind, dealing damage to the target.", // 60
            [0x7A26DEE9431711A8UL] = "Creates flaming chains, dealing damage to the target and nearby enemies.", // 61
            [0xDFF3F9C7ED17BDA3UL] = "While riding a Dark Horse, causes an earthquake that damages nearby targets and pushes them back.", // 62
            [0x596A894A9611C7BDUL] = "Forcibly moves a distant party member to your location.", // 63
            [0xE83C61E207E28D57UL] = "Temporarily increases the critical and excellent damage rate of you and your party members.", // 64
            [0x512370C333B1D4FFUL] = "Consumes the life and mana of nearby party members to damage the target and enemies in a straight line.", // 65
            [0xCDC8439C72A64680UL] = "Creates a wave of wind, dealing damage to the target and nearby enemies.", // 66
            [0x312D7204890A9D3FUL] = "Focuses magic power into a single point and fires it, dealing damage to the target.", // 73
            [0x5B9B3E5050F0BBCDUL] = "Summons a pillar of light from the ground, dealing damage to the target.", // 74
            [0x89EA9FDEA62A5029UL] = "Launches chain lightning that damages the target and nearby enemies, reducing the durability of their armor.", // 76
            [0x1AAD5AC380618D0FUL] = "Empowers your arrows so that they are not consumed.", // 77
            [0x5C18B2A16B412875UL] = "Sends three streams of flame along the ground, damaging the target and nearby enemies.", // 78
            [0xCAAB4D00E2A3DC8EUL] = "Follows its master around.", // 120
            [0x265E0886128F9F1AUL] = "Attacks targets near its master.", // 121
            [0x188F54D17E9611DFUL] = "Attacks the same target as its master.", // 122
            [0x08DAEFA0F62F0EE8UL] = "Attacks only the target chosen by its master.", // 123
            [0x7389EDE523D31FF5UL] = "Fires draining energy that damages the target and restores a portion of your life.", // 214
            [0x506D4C985ECD33FCUL] = "Strikes the target and nearby enemies with chaining lightning. The damage decreases with each jump.", // 215
            [0x397BF705DD908C3CUL] = "Returns a portion of the damage received back to the attacker.", // 217
            [0x548E7D5AA78F0491UL] = "Increases your wizardry and attack speed. Your defense and life are reduced.", // 218
            [0xEC9B867998668B68UL] = "Puts the target to sleep for a set duration. A sleeping target cannot act.", // 219
            [0x58A47ED6D7A72737UL] = "Casts a Weakness magic circle, reducing the attack power of targets standing on it.", // 221
            [0x4B1636B3C3F8B035UL] = "Casts an Innovation magic circle, reducing the defense of targets standing on it.", // 222
            [0x3C406D27FAC46BDFUL] = "Calls forth a fire demon, dealing damage to the target and nearby enemies.", // 223
            [0xBF84D1B09287B7B4UL] = "Calls forth a dark demon, dealing damage to the target and nearby enemies.", // 224
            [0xDB3F7473811BFD1BUL] = "Summons a wind demon, dealing damage to the target.", // 225
            [0x4F9EC3ED51C691ECUL] = "Slams the ground with force, damaging targets around you.", // 230
            [0x4A855C4BCC8D850DUL] = "Slams your weapon down, blasting the ground to damage the target and nearby enemies, with a chance to reduce movement speed.", // 232
            [0xB995F9EAAA2473A8UL] = "Increases your minimum wizardry for a set duration.", // 233
            [0x1551098A210DDB55UL] = "Restores the target's SD.", // 234
            [0x3B5864B409061B32UL] = "Rapidly fires your bow, damaging multiple targets in the chosen direction.", // 235
            [0x7820C77F229EB59AUL] = "Swings your weapon in a wide arc, dealing fire damage to multiple targets in the chosen direction.", // 236
            [0x63DB39D5436520FAUL] = "Summons bolts of lightning from the sky, damaging multiple targets.", // 237
            [0x1E1E0B3BD7148C2FUL] = "Summons dark ravens that damage multiple targets in the chosen direction.", // 238
            [0xE0FF1787076C983FUL] = "Fires a magic projectile, dealing damage to the target.", // 240
            [0x22D40901228F7A5BUL] = "Launches a giant bird of light, damaging multiple targets in its path.", // 241
            [0x373B705C7813BC41UL] = "Creates a dragon of light, dealing damage to the target and nearby enemies.", // 242
            [0xEFA7846F46AE0CCFUL] = "Hurls spears of light over a wide area, damaging multiple targets.", // 243
            [0x7847E02E9A81AEA4UL] = "Creates a reflective barrier on you or an ally: shock damage has a 50% chance to be blocked and partially returned to the attacker.", // 244
            [0x805DC8E45EC3B24FUL] = "Creates an exploding orb of energy, damaging the target and nearby enemies.", // 245
            [0x64A26D3EB6280F0FUL] = "Creates a large orb of energy that launches smaller bolts, damaging multiple targets in the area.", // 246
            [0x6C9CF9AF51B24744UL] = "Creates a magic circle that launches small bolts of energy, damaging multiple targets in the area.", // 247
            [0xCA79058FC5182AF2UL] = "Throws rapid punches that damage the target and reduce its attack power for a set duration.", // 260
            [0xF38B1E99457BA746UL] = "Strikes with an uppercut that damages the target and reduces its defense.", // 261
            [0x7492A7C9BDF82019UL] = "Kicks the target, dealing damage with a chance to reduce its movement speed.", // 262
            [0xCC13FF9C3BFD4917UL] = "Creates copies of yourself that damage multiple targets.", // 263
            [0xDFB6DECE79CB93E4UL] = "Hurls a fireball, dealing damage to the target and nearby enemies.", // 264
            [0xB458E4FD22D2BEABUL] = "Charges at the target, with a chance to damage its life and SD.", // 265
            [0xE0761B2755F476B5UL] = "For a set duration, your attacks ignore the enemy's defense.", // 266
            [0xDBF3AA7FAF637229UL] = "Grants you and your party members increased vitality for a set duration.", // 267
            [0xCFAA2C7CA8DA5C9AUL] = "Grants you and your party members an increased defense success rate for a set duration.", // 268
            [0xA39BCB37282BB4F9UL] = "Delivers a powerful blow, dealing damage to the target.", // 269
            [0x26B21F93EC8EB729UL] = "Launches a whirlwind that damages the target and nearby enemies, with a chance to push them back and reduce their defense success rate.", // 270
            [0x891937A7F7C17950UL] = "Swiftly stabs with your weapon, damaging the target and nearby enemies.", // 271
            [0x4867FBD46C76EDE8UL] = "For a set duration, your attacks reduce the target's AG.", // 272
            [0x1D6E39DD60F9D44FUL] = "Grants party members increased skill attack power for a set duration.", // 273
            [0xE121A1C4B0EAD004UL] = "Swiftly stabs the target three times with your weapon, dealing damage.", // 274
            [0x86A7A08769F5CA3EUL] = "Charges forward, pushing back the target and nearby enemies.", // 275
            [0xD605D157D711CE6FUL] = "Swings a lance, dealing damage to the target.", // 276
            [0x428CE5CA615928B9UL] = "Swiftly thrusts your weapon, damaging targets in the chosen direction.", // 277
            [0xFE35495FF2655F15UL] = "Increases your attack power and combat power. Your defense is reduced.", // 278
            [0x55D26D51E971EAF0UL] = "Swings your weapon in a wide arc, damaging targets in the chosen direction.", // 279
            [0x121E26A2BAE72AF6UL] = "Throws hooks with both hands, damaging the target and nearby enemies.", // 282
            [0x879B75AE8BACA140UL] = "Fires magic arrows, damaging the target and nearby enemies.", // 283
            [0x53AF62534B251F45UL] = "Creates moving rune energy that deals damage to the target.", // 284
            [0xAC2DD46C2EB53CCDUL] = "Shoots rune energy into the sky that crashes down, damaging nearby targets.", // 285
            [0xFC90F84E06B71213UL] = "Increases your damage for a set duration. Skill MP cost is increased.", // 286
            [0x7445F681B9506574UL] = "Increases the attack speed of you and your party members for a set duration. Your skill AG cost is increased.", // 287
            [0x724434A7ECB5133DUL] = "Summons and controls a reaper of death, damaging nearby targets.", // 288
            [0xF9D7C76F2477B492UL] = "Increases your curse power and defense for a set duration. Your life is reduced.", // 289
            [0x61946D4453826645UL] = "Dashes left and right while throwing blades, damaging multiple targets.", // 292
            [0x39C2A5B9C321B0C1UL] = "Sends a swarm of bats to the chosen spot, damaging multiple targets; some bats linger, dealing damage over time.", // 293
            [0xC4B5A800BDEA93A2UL] = "Rapidly closes in on multiple targets, dealing damage. Deals extra damage to targets under the Bat Flock damage-over-time effect.", // 294
            [0xA2D5E95F87C4DDF6UL] = "Detects living creatures in the surrounding area for a set duration. (Results are shown on the minimap for 1 minute.)", // 295
            [0xA21F8B18C96B64ACUL] = "For a set duration, you and your party members partially ignore enemy defense when attacking. Your skill attack power (%) is reduced by 3%.", // 297
            [0x5E1A4690C75EAB8CUL] = "Blesses you or an ally to gain 35% bonus base experience. Used on a target that has not learned the skill, it teaches the skill so the blessing can be spread.", // 298
            [0xEE7814525DE8ADF2UL] = "Swiftly stabs the target with a sharp weapon, dealing damage.", // 329
            [0xA17BD7C4E13E5D9EUL] = "Swings your equipped weapon, damaging multiple targets around you with a chance to push them back.", // 332
            [0xAC70153AAF5E9C08UL] = "Slams your weapon into the ground, causing an earthquake that damages multiple nearby targets, with a chance to reduce the durability of their armor.", // 333
            [0xF7BA6329F772BAB5UL] = "Slams your weapon down, blasting the ground to damage the target and nearby enemies. Damaged targets may be slowed or immobilized.", // 340
            [0x9810257A2EEF5199UL] = "Creates a wave of wind that damages targets in a straight line, with a chance to stun them or inflict additional damage over time.", // 342
            [0x2AFBAA0D9F956447UL] = "Slams your weapon down, blasting the ground to damage the target and nearby enemies. Damaged targets may have movement and attack speed reduced, or become immobilized.", // 343
            [0x13A34A0E33D3F74EUL] = "Launches blades, dealing damage to the target and nearby enemies.", // 344
            [0x53582335A5745646UL] = "Increases the life and mana of you and your party members for a set duration.", // 360
            [0x9D8E56050182ED86UL] = "Increases the life, mana and AG of you and your party members for a set duration.", // 363
            [0xC9BE995B7893D101UL] = "Increases your minimum/maximum wizardry and critical damage rate for a set duration.", // 383
            [0x2E6BD3F7F018846EUL] = "Casts a fire spell on the ground, damaging multiple targets around you.", // 388
            [0xDC330440629497CFUL] = "Drops ice shards, damaging multiple targets with a chance to slow or immobilize them.", // 393
            [0x5D60E137BFE524ADUL] = "Drops a huge meteorite from the sky that damages the target, with a chance to stun it.", // 394
            [0xD88D6AFBD1DF79BEUL] = "Creates a mana shield that consumes mana to reduce damage taken by you or an ally, and increases maximum mana for a set duration.", // 406
            [0xCBBA0213601F947CUL] = "Fires even more arrows in a fan shape than the basic Multi-Shot, damaging multiple targets.", // 418
            [0x07BD0EFDB5A818D0UL] = "Has a chance to remove status ailments from the chosen target.", // 425
            [0x9BF47B86B84EB7C7UL] = "Restores the life of all party members.", // 426
            [0x40BA942943BD7CEBUL] = "Fires a poison arrow that damages the target, with a chance to inflict poison damage over time.", // 427
            [0x5CA80B3511764B2BUL] = "Increases all stats of the target for a set duration.", // 430
            [0x6114B68A65C49D1DUL] = "Rapidly fires your bow, damaging multiple targets in the chosen direction with a chance to stun them.", // 431
            [0x44F4D2F8665C83ACUL] = "Summons a Satyros to fight by your side. It disappears when its life runs out.", // 432
            [0x6889DC36101B2AC6UL] = "Blinds the target, dealing damage and greatly reducing its attack success rate by impairing its sight.", // 461
            [0x7EF8B4A4F2FA3308UL] = "Fires draining energy that damages the target, restores a portion of your life, and drains the target's life for a set duration.", // 462
            [0xA2EF6448710AED5CUL] = "Blinds the target, dealing damage and greatly reducing its attack success rate by impairing its sight. Additionally stuns the target.", // 463
            [0x46011C79E4ADF85AUL] = "Increases your wizardry and attack speed.", // 470
            [0x1598871413827E87UL] = "An ice attack that damages the target, with a chance to slow or immobilize it.", // 491
            [0xF94DD3DE1BB5100BUL] = "Swings your weapon in a wide arc, dealing fire damage to multiple targets in the chosen direction with a chance to push them back.", // 494
            [0xE068980C02ADB13FUL] = "Grows a plant that discharges sparks from the ground, damaging nearby targets with a chance to immobilize them.", // 495
            [0x2057CEC0BF257299UL] = "The Dark Horse stomps the ground, causing an earthquake that damages nearby targets and pushes them back.", // 512
            [0x603FAE2A83867A87UL] = "Creates flaming chains that damage the target and nearby enemies, with a chance to stun them.", // 514
            [0xA867AF5F0B30FE8AUL] = "Temporarily increases the critical damage of you and your party members.", // 515
            [0x881DD77D7120BF98UL] = "The Dark Horse stomps the ground, causing an earthquake that damages nearby targets, pushes them back, and has a chance to stun them.", // 516
            [0x066D4DD4B8AA5077UL] = "Temporarily increases the critical damage rate and critical damage of you and your party members.", // 517
            [0x0722CE3ED35727E9UL] = "Sends three streams of flame along the ground that damage the target, bursting to damage nearby enemies as well.", // 518
            [0x582CD79424E9AC71UL] = "Temporarily increases the critical damage rate and critical damage of you and your party members, and additionally increases the excellent damage rate.", // 522
            [0x8935E350C2009C5AUL] = "Grants you and your party members an increased defense success rate for a set duration.", // 556
            [0x634E6CD3849CC52AUL] = "Hurls a fireball that damages the target and nearby enemies, with a chance to inflict fire damage over time.", // 561
            [0x0D9C89F877ED4045UL] = "Kicks the target, dealing damage with a chance to slow it and inflict damage over time.", // 562
            [0x817B0078D09A6166UL] = "Charges at the target, with a chance to damage its life and SD, and stuns it.", // 564
            [0xF7423BFC5AE42233UL] = "Activates a buff that inflicts bleed damage each time you use a skill.", // 565
            [0xD043EEA929EE8F67UL] = "Grants you and your party members increased defense success rate and defense for a set duration.", // 572
            [0x957C982E05FDE622UL] = "Temporarily creates a shield that absorbs damage.", // 618
            [0xC49049C6CF60AAB2UL] = "Temporarily immobilizes the target.", // 619
            [0x83BAE845D4562F10UL] = "Moves to the target who used the sacred relic.", // 620
            [0xFA4CBE04BF79EB16UL] = "Reduces the target's current SD by 50%.", // 621
            [0x34D3BC87EE3CD30DUL] = "Charges in swiftly and delivers a powerful blow, dealing damage to the target.", // 631
            [0x271CC895311DB469UL] = "Creates a phantom identical to yourself that fights alongside you. The phantom absorbs part of the damage you take.", // 642
            [0x1250127C7C74CAD2UL] = "Quickly dashes in the chosen direction.", // 651
            [0xF3A917596C53E052UL] = "Increases your base defense success rate for a set duration.", // 652
            [0x65FDB7D9523691B8UL] = "Creates a cursed barrier around you, with a chance to reduce the movement speed of all targets inside.", // 661
            [0x83710D906682D066UL] = "Creates a cursed barrier around you, with a chance to reduce the movement speed of all targets inside and deal damage over time.", // 662
            [0x6445C4AD3CB363BBUL] = "Swiftly stabs the target three times with your weapon, with a chance to stun it.", // 694
            [0xF2D5374C3662656FUL] = "Swings your weapon in a wide arc, damaging targets in the chosen direction with a chance to stun them.", // 697
            [0x74CC426482743969UL] = "Increases your chance to ignore enemy defense for a set duration.", // 700
            [0x41792812A0F1A0E5UL] = "Thrusts your fire-imbued weapon, damaging the target and nearby enemies with a chance to deal one additional hit.", // 723
            [0x7AAF1C5A3E6042AFUL] = "Hurls a powerful meteorite that damages the target and nearby enemies. Has a chance to deal one additional hit.", // 724
            [0x1C5FAD9A5D8B2D7BUL] = "Hurls meteorites over a wide area, damaging multiple targets.", // 725
            [0x74A9D4B5F02A4030UL] = "Unleashes a wicked spirit's energy that damages the target and nearby enemies. Has a chance to deal one additional hit.", // 726
            [0x15161A8B4E37359FUL] = "Fires homing arrows that damage multiple targets in the area. Has a chance to deal one additional hit.", // 727
            [0x94B6C61F3B5EB13FUL] = "Fires flaming arrows that damage multiple targets around you.", // 728
            [0xE3B596221ED9D427UL] = "Summons a fire beast that damages the target and nearby enemies, with a chance to deal one additional hit.", // 729
            [0xBF219A565828F56CUL] = "Summons a water beast that damages the target and nearby enemies. Has a chance to deal one additional hit.", // 730
            [0x4B1A9879C654D571UL] = "Swings your ice-imbued weapon, damaging multiple targets in the chosen direction with a chance to deal one additional hit. Damaged targets may be slowed.", // 731
            [0xD0DF2D9731244048UL] = "Swings your fire-imbued weapon, damaging multiple targets in the chosen direction with a chance to deal one additional hit. Damaged targets may suffer fire damage over time.", // 732
            [0xC2D63A66736CD97DUL] = "Fires a dark blast that damages the target and nearby enemies. Has a chance to deal one additional hit.", // 733
            [0x16B24A3C2052D7C4UL] = "Thrusts your weapon, damaging targets in a straight line with a chance to deal one additional hit.", // 735
            [0x872916F718208A54UL] = "Launches fire-attribute energy that damages multiple targets in the chosen direction, with a chance to deal one additional hit.", // 736
            [0x648F49E2EA542674UL] = "Unleashes a wind spirit that damages the target and nearby enemies. Has a chance to deal one additional hit.", // 737
            [0x8D2A09C03969CA95UL] = "Damages the target and nearby enemies with an earthquake. Has a chance to deal one additional hit.", // 738
            [0xD78686DF7FAD2BCAUL] = "Unleashes dark energy that damages the target and nearby enemies. Has a chance to deal one additional hit.", // 739
            [0xEA177B1E913725FCUL] = "Increases the PvP attack damage and skill attack power of you and your party members.", // 740
            [0x8919BDC987F0A6DBUL] = "Increases your curse power and defense for a set duration.", // 771
            [0xBFDDFECC59B49AD1UL] = "Casts a Weakness magic circle, reducing the physical and elemental attack power of targets standing on it.", // 772
            [0x62E8E466AF5581E9UL] = "Casts an Innovation magic circle, reducing the physical and elemental defense of targets standing on it.", // 773
            [0x4F137BCD54C66EE0UL] = "Transforms into a Rage Knight. Increases the damage range and casting distance of Blow skills (including Fire Blow and Sword Blow).", // 801
            [0x4B3465829F90DEE0UL] = "Increases party members' attack power and wizardry and takes part of their damage in their place. Absorbs party members' HP.", // 803
            [0xB5A26BCD4E28A00EUL] = "Slams your weapon down, blasting the ground to damage the target and nearby enemies.", // 807
            [0xBA58C8A67D1995E7UL] = "Transforms into a Faith Knight. Stat efficiency changes and exclusive skills become available.", // 810
            [0xB239977351FCB979UL] = "Fires dark plasma. The plasma persists for a set duration, dealing damage to the target.", // 820
            [0x99C8A79D43F018FBUL] = "Fires an ice bullet, dealing damage to the target.", // 823
            [0x6492B97FA2B0A1A2UL] = "Rapidly sprays bullets, damaging targets in front of you.", // 825
            [0xD4AC79C3FE93AC9EUL] = "Changes your stats: wizardry and attack speed increase while defense decreases.", // 828
            [0xD7B66BB9965AA1DEUL] = "Rapidly sprays ice bullets, damaging targets in front of you.", // 835
            [0x23ED255858BA39B3UL] = "Increases the target's defense for a set duration.", // 851
            [0x09EB85132BFC2AE7UL] = "Increases the target's attack power and wizardry for a set duration.", // 853
            [0x601D3A290759C506UL] = "Transforms into a Two-Handed Sword Magic Swordsman. Attack power and defense increase. Chaos Blade and Fire Blood gain 6 range, and every third skill attack detonates a sword wave around you, dealing extra damage.", // 865
            [0xBBEAB4B82610B7ACUL] = "Transforms into a One-Handed Sword Magic Swordsman. Attack speed and defense increase. Chaos Blade and Ice Blood gain 5 range, and every third skill attack detonates a sword wave around you, dealing extra damage.", // 867
            [0x2F2A0A707C54C282UL] = "Transforms into a Spell Magic Swordsman. Wizardry and defense increase. Havoc Spear gains 7 range, and every third skill attack detonates a nova, dealing extra damage.", // 869
            [0xCDE73C2B01286E29UL] = "Gathers holy energy and launches it forward, dealing damage to the target.", // 876
            [0x146C77569033D2B5UL] = "Rushes at nearby enemies, damaging enemies in a wide area in front with a sword wave.", // 877
            [0xF71A075CC2D94BAFUL] = "Launches a wind-imbued sword wave forward, damaging multiple enemies.", // 879
            [0xDB266E59FDF95085UL] = "Sharp phantom blades whip up a storm, damaging multiple enemies.", // 881
            [0x7B1FD790D37771A6UL] = "Summons phantoms of the Illusion Knight, damaging nearby enemies. (Phantoms deal no damage in PvP.)", // 883
            [0xB91839C3C50ABE02UL] = "Summons the Illusion Blade. Increases your attack power.", // 885
            [0x2A3721AFF5E75751UL] = "Creates a copy of yourself that damages enemies. The fewer enemies it attacks, the higher the damage.", // 894
            [0x530E62E780389E6BUL] = "Swings your weapon in a wide arc, damaging enemies in a large area.", // 896
            [0xE8578D4332856DA3UL] = "Creates two homunculi through alchemy and orders them to attack, damaging multiple targets.", // 900
            [0xE553701ECB0D9514UL] = "Creates a stone bomb through alchemy, damaging multiple targets in the area.", // 903
            [0xCB36D5AD90256EFFUL] = "'Alchemy rate %d%%' Creates a Stone of Chaos through alchemy; for a set duration it summons field monsters one at a time. (Cannot be used in some fields.)", // 905
            [0x9A5F8AF9A44C4F26UL] = "Borrows the power of the Divine Spirit to deal area damage to nearby enemies.", // 911
            [0xD42B2D7A7EEFE588UL] = "The lord's charisma increases all stats of party members.", // 912
            [0x59B26F4DC472AEA3UL] = "The Divine Spirit's attack power effect is applied to party members.", // 913
            [0x4BA0FD564C3F042CUL] = "The Divine Horse's defense effect is applied to party members.", // 914
            [0xF3B4693DCCCC4105UL] = "Increases your attack power and combat power.", // 915
            [0xBBC705D202E60317UL] = "Iron Defense (learned).", // 1016
            [0x691D5D2D51729D54UL] = "Iron Defense enhancement.", // 1017
            [0x419A00A32A35615AUL] = "Swell Life HP bonus enhancement.", // 1073
            [0x4E91347A1B81E453UL] = "Hurls meteorites over a wide area, damaging multiple targets.", // 1076
            [0xB7D2267DC39CDAB6UL] = "Soul Barrier damage reduction increase.", // 1080
            [0x85CDD3F9E335951CUL] = "Berserker attack power enhancement.", // 1106
            [0x6B3FDD8DA0364BDEUL] = "Removes Berserker's defense reduction effect.", // 1107
            [0xF2904C968B1E294BUL] = "Vitality skill effect enhancement.", // 1114
            [0x650AD3E98BBD2AB5UL] = "Dark Side skill attack power enhancement.", // 1120
            [0x58679E69E0274BF8UL] = "Deals explosion damage to poisoned targets; enhanced option effects from the 'Debuff Detonation' tree are triggered.", // 1125
            [0xADABD959B6A65A77UL] = "Deals explosion damage to frozen targets; enhanced option effects from the 'Debuff Detonation' tree are triggered.", // 1126
            [0x1755EBFAA656B80CUL] = "Deals explosion damage to heavily bleeding targets; enhanced option effects from the 'Debuff Detonation' tree are triggered.", // 1127
            [0x390DF2B961A18760UL] = "Deals explosion damage to stunned targets; enhanced option effects from the 'Debuff Detonation' tree are triggered.", // 1128
            [0xAC5EFC378994082EUL] = "Creates a sword wave, damaging the target and nearby enemies.", // 1203
            [0x8DB478C0F66EF906UL] = "Takes a portion of the damage dealt to party members in their place. Absorbs party members' HP.", // 1204
            [0x8D5DCC008814A866UL] = "Fires an ice blast around you, damaging nearby targets.", // 1212
            [0x41A3963ABFACB413UL] = "Fires a powerful bullet, damaging nearby targets and those in a straight line.", // 1213
            [0x5DD7F5650A7436D2UL] = "Swings a dark-attribute sword in a wide arc in front of you, damaging multiple targets with a chance to deal one additional hit.", // 1214
            [0x0064B918360F6439UL] = "Summons magic spears from the ground, damaging multiple targets.", // 1215
            [0x87C56A2905DB3445UL] = "Shoots several arrows into the air that rain down, damaging multiple targets.", // 1222
            [0x1C7573C596970678UL] = "Increases the target's elemental attack power for a set duration.", // 1224
            [0x39CA847876A732DBUL] = "Increases the target's elemental defense for a set duration.", // 1225
            [0x107EDFC0B476086EUL] = "Summons phantoms of the Illusion Knight, damaging nearby enemies.", // 1234
            [0xDC2E88AA4F1DAB7BUL] = "Forges weapons out of air through alchemy and launches them, damaging multiple targets in the area.", // 1239
            [0xB2172F0E50942CA1UL] = "[Requires Divine Spirit equipped] Borrows the power of the Divine Spirit to deal area damage to nearby enemies.", // 1240
            [0x8A69F79510DD62BCUL] = "Transforms into a Rage Knight. Increases the damage range and casting distance of Blow-type skills (including Fire Blow and Sword Blow).", // 1500
            [0x53F392F22992FAF4UL] = "Rapidly sprays fire bullets, damaging targets in front of you.", // 2004
            [0x092379B17EF2CEBFUL] = "After using Spiral Charge, every third attack with Chaos Blade or Fire Blood triggers a sword-wave explosion, dealing damage.", // 2017
            [0x8B3083B7AABA09A1UL] = "After using Crusher Charge, every third attack with Chaos Blade or Ice Blood triggers a sword-wave explosion, dealing damage.", // 2018
            [0x0E8573714A71C231UL] = "After using Elemental Charge, every third attack with Havoc Spear or Gigantic Storm triggers a nova, dealing damage.", // 2019
            [0xB9A0786C85ACE4D2UL] = "Greatly increases your movement speed, attack speed and attack success rate for a set duration.", // 2024
            [0xE052155F229D03EAUL] = "Summons phantoms of the Illusion Knight, damaging nearby enemies. (Phantoms deal no damage in PvP.)", // 2031
            [0x0B466D0BE6976A58UL] = "For a set duration, spears of judgment rain from the sky, damaging enemies. (Used automatically while MU Helper runs; power scales with the party's Nuke damage.)", // 2033
            [0xA280CC655EF917E8UL] = "For a set duration, a black hole forms, damaging multiple targets in the area. (Used automatically while MU Helper runs; power scales with the party's Bolt damage.)", // 2034
            [0x84102483C237DBCAUL] = "For a set duration, spinning blades form, damaging multiple targets over a wide area. (Used automatically while MU Helper runs; power scales with the party's area damage.)", // 2035
            [0x6FE614377B04FEBEUL] = "When using Gale Meteor Storm, the Evil Spirit skill's ability is applied, granting the 'Attack Speed +15' effect.", // 2037
            [0x52E86329EF067CE9UL] = "When using Barrage Sword Blow, the Rageful Blow skill's ability is applied, granting the 'Range +1' effect.", // 2040
            [0x2FD6445C226A50ABUL] = "When using Gale Crushing Blow, the Rageful Blow skill's ability is applied, granting the 'Attack Speed +15' effect.", // 2043
            [0x91946F87FA5298A5UL] = "When using Barrage Raining Arrows, the Multi-Shot skill's ability is applied, granting the 'Range +1' effect.", // 2044
            [0x8637BEB4623413B0UL] = "When using Gale Holy Bolt, the Multi-Shot skill's ability is applied, granting the 'Attack Speed +15' effect.", // 2047
            [0x338A2C23277948C7UL] = "When using Barrage Chaos Blade, the Fire Blood skill's ability is applied, granting the 'Range +1' effect.", // 2049
            [0x172446C99EBBC041UL] = "When using Rage Havoc Spear, the Gigantic Storm skill's ability is applied, granting the 'Splash Damage +1' effect.", // 2051
            [0xF406708E4E63D32EUL] = "When using Barrage Wind Soul, the Chaotic Diseier skill's ability is applied, granting the 'Range +1' effect.", // 2054
            [0x9F65AB25FB84BCCDUL] = "When using Barrage Fire Beast, the Lightning Shock skill's ability is applied, granting the 'Range +1' effect.", // 2058
            [0x42770664D5AD6BE3UL] = "When using Rage Death Side, the Lightning Shock skill's ability is applied, granting the 'Splash Damage +1' effect.", // 2060
            [0xA32B837CD2502433UL] = "When using Barrage Dark Side, the Dragon Roar skill's ability is applied, granting the 'Range +1' effect.", // 2061
            [0x8347F589D63E8A64UL] = "When using Barrage Spirit Hook, the Dragon Roar skill's ability is applied, granting the 'Range +1' effect.", // 2063
            [0x95C707A6FCF5EFD0UL] = "When using Barrage Over Sting, the Breche skill's ability is applied, granting the 'Range +1' effect.", // 2065
            [0xC7B015254FC85693UL] = "When using Gale Lightning Storm, the Evil Spirit skill's ability is applied, granting the 'Attack Speed +15' effect.", // 2068
            [0x56AFB34F4515ED72UL] = "When using Barrage Pierce Attack, the Bat Flock skill's ability is applied, granting the 'Range +1' effect.", // 2071
            [0xABCD5283D928DC8BUL] = "When using Gale Bursting Flare, the Ice Blast skill's ability is applied, granting the 'Attack Speed +15' effect.", // 2073
            [0x9A881225DDEA28CFUL] = "When using Barrage Ultimate Force, the Marvel Burst skill's ability is applied, granting the 'Range +1' effect.", // 2076
            [0x984CB4B5B0CB7164UL] = "When using Barrage Spear Storm, the Shining Bird skill's ability is applied, granting the 'Range +1' effect.", // 2080
            [0xE6D24B6126256457UL] = "When using Barrage Blade Storm, the Charge Slash skill's ability is applied, granting the 'Range +1' effect.", // 2082
            [0xD564CE0A90964101UL] = "When using Gale Wild Breche, the Breche skill's ability is applied, granting the 'Attack Speed +15' effect.", // 2089
            [0x3EE9479007C06387UL] = "When using Barrage Countless Weapon, the Ignition Bomber skill's ability is applied, granting the 'Range +1' effect.", // 2094
            [0xD201FFE11F075D99UL] = "[Requires Divine Spirit equipped] When using Rage Spirit Blast, the Wind Soul skill's ability is applied, granting the 'Splash Damage +1' effect.", // 2096
            [0xEB9D7C878C9246D3UL] = "The Dark Lord's leadership increases all stats of party members.", // 2097
        };
    }
}
