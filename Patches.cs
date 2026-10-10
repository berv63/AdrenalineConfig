using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using static AdrenalineConfig.AdrenalineConfigPlugin;

namespace AdrenalineConfig
{
    // The game calls Character.AddAdrenaline from a dozen places with values taken from items, attacks,
    // projectiles and the player prefab. Instead of rewriting those values on every item, each call gets
    // its value multiplied on the way in, so the percentages apply to whatever the item defines.
    internal static class AdrenalineSources
    {
        private static readonly Dictionary<MethodBase, Source[]> sites = new Dictionary<MethodBase, Source[]>();

        internal static void Patch(Harmony harmony)
        {
            // Sources are listed in the order the AddAdrenaline calls appear in the method
            Add(typeof(Attack), nameof(Attack.OnAttackTrigger), null, Source.WeaponUse);
            Add(typeof(Attack), nameof(Attack.FireProjectileBurst), null, Source.WeaponUse);
            Add(typeof(Attack), nameof(Attack.DoAreaAttack), null, Source.MeleeHit);
            Add(typeof(Attack), nameof(Attack.DoMeleeAttack), null, Source.MeleeHit, Source.MissedAttack);
            Add(typeof(Projectile), nameof(Projectile.OnHit), null, Source.ProjectileHit);
            Add(typeof(Humanoid), nameof(Humanoid.BlockAttack), null, Source.Block, Source.Parry);
            Add(typeof(Character), nameof(Character.RPC_Damage), null, Source.DamageTaken);
            Add(typeof(Character), nameof(Character.AddStaggerDamage), null, Source.StaggerEnemy);
            Add(typeof(SE_Stats), nameof(SE_Stats.StartupEffects), null, Source.StatusEffect);
            Add(typeof(Player), nameof(Player.UpdateStats), new[] { typeof(float) }, Source.Decay);
            Add(typeof(Player), nameof(Player.RPC_HitWhileDodging), null, Source.PerfectDodge);
            Add(typeof(Player), nameof(Player.ActivateGuardianPower), null, Source.ForsakenPower);

            HarmonyMethod transpiler = new HarmonyMethod(typeof(AdrenalineSources), nameof(Transpiler));
            foreach (MethodBase method in sites.Keys)
            {
                try
                {
                    harmony.Patch(method, transpiler: transpiler);
                }
                catch (Exception e)
                {
                    logger.LogWarning($"Could not patch {method.DeclaringType.Name}.{method.Name}, its adrenaline settings will have no effect: {e.Message}");
                }
            }
        }

        private static void Add(Type type, string name, Type[] parameters, params Source[] sources)
        {
            MethodInfo method = AccessTools.Method(type, name, parameters);
            if (method == null)
            {
                logger.LogWarning($"{type.Name}.{name} was not found, its adrenaline settings will have no effect.");
                return;
            }

            sites[method] = sources;
        }

        public static float Scale(float value, int source) => value * Multiplier((Source)source);

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            Source[] sources = sites[original];
            List<CodeInstruction> codes = instructions.ToList();

            List<int> calls = new List<int>();
            for (int i = 0; i < codes.Count; i++)
                if ((codes[i].opcode == OpCodes.Callvirt || codes[i].opcode == OpCodes.Call) && codes[i].operand is MethodInfo { Name: nameof(Character.AddAdrenaline) })
                    calls.Add(i);

            // A game update moved things around, better to leave the method alone than to scale the wrong call
            if (calls.Count != sources.Length)
            {
                logger.LogWarning($"{original.DeclaringType.Name}.{original.Name} has {calls.Count} adrenaline calls where {sources.Length} were expected, its adrenaline settings will have no effect.");
                return codes;
            }

            // The amount is on top of the stack right before the call
            for (int n = calls.Count - 1; n >= 0; n--)
            {
                CodeInstruction call = codes[calls[n]];
                CodeInstruction loadSource = new CodeInstruction(OpCodes.Ldc_I4, (int)sources[n]);
                loadSource.labels.AddRange(call.labels);
                call.labels.Clear();

                codes.Insert(calls[n], new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(AdrenalineSources), nameof(Scale))));
                codes.Insert(calls[n], loadSource);
            }

            return codes;
        }
    }

    // Staggering an enemy that another player's game controls arrives here instead of through AddStaggerDamage
    [HarmonyPatch(typeof(Character), nameof(Character.RPC_AddAdrenaline))]
    internal static class Character_RPC_AddAdrenaline_Patch
    {
        private static void Prefix(Character __instance, ref float amount)
        {
            if (__instance == Player.m_localPlayer)
                amount *= Multiplier(Source.StaggerEnemy);
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.AddAdrenaline))]
    internal static class Player_AddAdrenaline_Patch
    {
        // Status effects started from here can add adrenaline themselves, hence a counter
        internal static int depth;

        private static void Prefix(Player __instance, ref float v, out bool __state)
        {
            __state = false;
            if (__instance != Player.m_localPlayer)
                return;

            depth++;

            if (v > 0f)
            {
                v *= Multiplier(allGainsMultiplier) * NearbyEnemies.GainMultiplier(__instance);
                __state = v > 0f && __instance.GetMaxAdrenaline() > 0f;
            }
        }

        // A gain restarts the decay delay
        private static void Postfix(Player __instance, bool __state)
        {
            if (__state)
                __instance.m_adrenalineDegenTimer *= Multiplier(decayDelayMultiplier);
        }

        private static void Finalizer(Player __instance)
        {
            if (__instance == Player.m_localPlayer && depth > 0)
                depth--;
        }
    }

    internal static class NearbyEnemies
    {
        private const float countInterval = 0.5f;

        private static int count;
        private static float countTime = float.MinValue;

        // Hostile creatures that have noticed something and are within range, recounted twice a second
        internal static int Count(Player player)
        {
            if (Time.time - countTime < countInterval && Time.time >= countTime)
                return count;

            countTime = Time.time;
            count = 0;

            float range = enemyRange.Value;
            Vector3 position = player.transform.position;
            foreach (Character character in Character.GetAllCharacters())
            {
                if (character == null || character == player || character.IsDead() || character.IsTamed())
                    continue;

                BaseAI ai = character.GetBaseAI();
                if (ai == null || !ai.IsAlerted() || !BaseAI.IsEnemy(player, character))
                    continue;

                if (Vector3.Distance(position, character.transform.position) <= range)
                    count++;
            }

            return count;
        }

        internal static float GainMultiplier(Player player)
        {
            if (!modEnabled.Value || !scaleWithEnemies.Value)
                return 1f;

            return 1f + Mathf.Max(0, Count(player) - 1) * bonusPerExtraEnemy.Value / 100f;
        }
    }

    // Runs after the game's own adrenaline decay for the frame
    [HarmonyPatch(typeof(Player), nameof(Player.UpdateStats), typeof(float))]
    internal static class Player_UpdateStats_Patch
    {
        private static void Postfix(Player __instance, float dt)
        {
            if (!modEnabled.Value || !sprintGain.Value || __instance != Player.m_localPlayer)
                return;

            if (__instance.IsRunning() && __instance.GetMaxAdrenaline() > 0f && NearbyEnemies.Count(__instance) >= 2)
                __instance.AddAdrenaline(sprintGainPerSecond.Value * dt);
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.GetMaxAdrenaline))]
    internal static class Player_GetMaxAdrenaline_Patch
    {
        private static void Postfix(Player __instance, ref float __result)
        {
            if (__instance == Player.m_localPlayer)
                __result *= Multiplier(requirementPercent);
        }
    }

    [HarmonyPatch(typeof(Hud), nameof(Hud.SetAdrenalineBarSize))]
    internal static class Hud_SetAdrenalineBarSize_Patch
    {
        private static void Prefix(ref float size)
        {
            if (!resizeBar.Value)
                size /= Multiplier(requirementPercent);
        }
    }

    // The effect is cloned from the item's shared status effect, which is given the configured duration while
    // the clone is made and set up, then put back. Covers the timers a status effect derives from its duration.
    [HarmonyPatch(typeof(SEMan), nameof(SEMan.AddStatusEffect), typeof(StatusEffect), typeof(bool), typeof(int), typeof(float), typeof(short))]
    internal static class SEMan_AddStatusEffect_Patch
    {
        private static void Prefix(SEMan __instance, StatusEffect statusEffect, out float __state)
        {
            __state = -1f;
            if (Player_AddAdrenaline_Patch.depth == 0 || statusEffect == null || statusEffect.m_ttl <= 0f)
                return;

            Player player = Player.m_localPlayer;
            if (player == null || __instance != player.GetSEMan() || !IsFullAdrenalineEffect(player, statusEffect))
                return;

            __state = statusEffect.m_ttl;
            statusEffect.m_ttl *= Multiplier(effectDurationMultiplier);
        }

        private static void Finalizer(StatusEffect statusEffect, float __state)
        {
            if (__state >= 0f)
                statusEffect.m_ttl = __state;
        }

        private static bool IsFullAdrenalineEffect(Player player, StatusEffect statusEffect)
        {
            foreach (ItemDrop.ItemData item in player.GetInventory().GetAllItems())
                if (item.m_equipped && item.m_shared.m_fullAdrenalineSE == statusEffect)
                    return true;

            return false;
        }
    }
}
