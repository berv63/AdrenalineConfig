using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using UnityEngine;

namespace AdrenalineConfig
{
    // Every place the game adds or removes adrenaline. The value doubles as the index into AdrenalineConfigPlugin.sourceMultiplier.
    internal enum Source
    {
        MeleeHit,
        ProjectileHit,
        Block,
        Parry,
        PerfectDodge,
        StaggerEnemy,
        ForsakenPower,
        StatusEffect,
        WeaponUse,
        MissedAttack,
        DamageTaken,
        Decay,
        Count
    }

    [BepInPlugin(pluginID, pluginName, pluginVersion)]
    public class AdrenalineConfigPlugin : BaseUnityPlugin
    {
        public const string pluginID = "berv.AdrenalineConfig";
        public const string pluginName = "Adrenaline Config";
        public const string pluginVersion = "0.0.1";

        internal static AdrenalineConfigPlugin instance;
        internal static ManualLogSource logger;

        private readonly Harmony harmony = new Harmony(pluginID);

        internal static ConfigEntry<bool> modEnabled;
        internal static ConfigEntry<int> requirementPercent;
        internal static ConfigEntry<bool> resizeBar;
        internal static ConfigEntry<float> effectDurationMultiplier;
        internal static ConfigEntry<float> allGainsMultiplier;
        internal static ConfigEntry<float> decayDelayMultiplier;
        internal static readonly Func<float>[] sourceMultiplier = new Func<float>[(int)Source.Count];
        internal static ConfigEntry<bool> scaleWithEnemies;
        internal static ConfigEntry<int> bonusPerExtraEnemy;
        internal static ConfigEntry<float> enemyRange;
        internal static ConfigEntry<bool> sprintGain;
        internal static ConfigEntry<float> sprintGainPerSecond;

        private void Awake()
        {
            instance = this;
            logger = Logger;

            modEnabled = Config.Bind("General", "Enabled", true, "Disabled: adrenaline behaves as in the unmodded game. Multipliers of 1 and percentages of 100 change nothing.");

            requirementPercent = Config.Bind("Trigger", "Adrenaline required", 100, new ConfigDescription("Adrenaline an item needs before its effect triggers, as a percentage of the item's own value. 50 triggers every item at half its usual adrenaline.", new AcceptableValueRange<int>(1, 100)));
            resizeBar = Config.Bind("Trigger", "Resize adrenaline bar", true, "Enabled: the adrenaline bar gets shorter or longer with the adrenaline required, as it does between items in the unmodded game. Disabled: the bar keeps the length the item would normally give it.");
            effectDurationMultiplier = BindMultiplier("Trigger", "Effect duration multiplier", "Multiplies the duration of the effect an item triggers at full adrenaline. Applies to effects triggered after the change.");

            allGainsMultiplier = BindMultiplier("Gain multipliers", "All gains", "Multiplies every adrenaline gain, on top of the multipliers for the individual sources below.");
            BindGain(Source.MeleeHit, "Melee hit", "Multiplies adrenaline gained by hitting an enemy with a melee attack.");
            BindGain(Source.ProjectileHit, "Projectile hit", "Multiplies adrenaline gained by hitting an enemy with an arrow, bolt, spell or other projectile.");
            BindGain(Source.Block, "Block", "Multiplies adrenaline gained by blocking an attack.");
            BindGain(Source.Parry, "Parry", "Multiplies adrenaline gained by parrying an attack.");
            BindGain(Source.PerfectDodge, "Perfect dodge", "Multiplies adrenaline gained by dodging through an attack.");
            BindGain(Source.StaggerEnemy, "Stagger enemy", "Multiplies adrenaline gained by staggering an enemy.");
            BindGain(Source.ForsakenPower, "Forsaken power", "Multiplies adrenaline gained by activating a forsaken power.");
            BindGain(Source.StatusEffect, "Status effects", "Multiplies adrenaline granted up front by status effects, such as consumables.");
            BindGain(Source.WeaponUse, "Weapon use", "Multiplies adrenaline gained by using a weapon that grants it on every attack, whether it hits or not.");

            BindLoss(Source.MissedAttack, "Missed attack", "Adrenaline lost when a melee attack hits nothing, as a percentage of the game's own value. 0 removes the penalty.");
            BindLoss(Source.DamageTaken, "Damage taken", "Adrenaline lost when taking a hit without blocking it, as a percentage of the game's own value. 0 removes the penalty.");
            BindLoss(Source.Decay, "Decay rate", "Speed at which adrenaline drains away out of combat, as a percentage of the game's own value. 0 keeps adrenaline until it is used.");
            decayDelayMultiplier = BindMultiplier("Loss", "Decay delay multiplier", "Multiplies the time after the last adrenaline gain before adrenaline starts to drain.");

            scaleWithEnemies = Config.Bind("Nearby enemies", "Scale gain with nearby enemies", false, "Enabled: every adrenaline gain is increased for each enemy nearby beyond the first. Enemies count while they are hostile, alerted and within range.");
            bonusPerExtraEnemy = Config.Bind("Nearby enemies", "Gain bonus per extra enemy", 25, new ConfigDescription("Percent added to adrenaline gains for each nearby enemy beyond the first. At 25, two enemies give 125% and three give 150%.", new AcceptableValueRange<int>(0, 500)));
            enemyRange = Config.Bind("Nearby enemies", "Enemy range", 20f, new ConfigDescription("Distance in meters within which an enemy counts as nearby.", new AcceptableValueRange<float>(5f, 100f)));
            sprintGain = Config.Bind("Nearby enemies", "Gain while sprinting near multiple enemies", false, "Enabled: sprinting builds adrenaline while two or more enemies are nearby.");
            sprintGainPerSecond = Config.Bind("Nearby enemies", "Sprint gain per second", 1f, new ConfigDescription("Adrenaline gained per second of sprinting near multiple enemies, before the other gain settings are applied.", new AcceptableValueRange<float>(0f, 20f)));

            harmony.PatchAll();
            AdrenalineSources.Patch(harmony);
        }

        private void OnDestroy()
        {
            harmony?.UnpatchSelf();
            instance = null;
        }

        // Settings only go in the direction that makes adrenaline easier: gains can be raised, losses and the requirement lowered.
        // Multipliers go from 1 to 10 in steps of 0.1, whatever a slider or a hand edited config file gives.
        private ConfigEntry<float> BindMultiplier(string section, string key, string description)
        {
            ConfigEntry<float> entry = Config.Bind(section, key, 1f, new ConfigDescription(description + " 1 to 10 in steps of 0.1.", new AcceptableValueRange<float>(1f, 10f)));

            void RoundToStep()
            {
                float rounded = Mathf.Round(entry.Value * 10f) / 10f;
                if (!Mathf.Approximately(rounded, entry.Value))
                    entry.Value = rounded;
            }

            entry.SettingChanged += (sender, args) => RoundToStep();
            RoundToStep();
            return entry;
        }

        private void BindGain(Source source, string key, string description)
        {
            ConfigEntry<float> entry = BindMultiplier("Gain multipliers", key, description);
            sourceMultiplier[(int)source] = () => entry.Value;
        }

        private void BindLoss(Source source, string key, string description)
        {
            ConfigEntry<int> entry = Config.Bind("Loss", key, 100, new ConfigDescription(description, new AcceptableValueRange<int>(0, 100)));
            sourceMultiplier[(int)source] = () => entry.Value / 100f;
        }

        internal static float Multiplier(ConfigEntry<int> percent) => modEnabled.Value ? percent.Value / 100f : 1f;

        internal static float Multiplier(ConfigEntry<float> multiplier) => modEnabled.Value ? multiplier.Value : 1f;

        internal static float Multiplier(Source source) => modEnabled.Value ? sourceMultiplier[(int)source]() : 1f;
    }
}
