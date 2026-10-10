using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace AdrenalineConfig
{
    // Every place the game adds or removes adrenaline. The value doubles as the index into AdrenalineConfigPlugin.sourcePercent.
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
        internal static ConfigEntry<int> effectDurationPercent;
        internal static ConfigEntry<int> allGainsPercent;
        internal static ConfigEntry<int> decayDelayPercent;
        internal static readonly ConfigEntry<int>[] sourcePercent = new ConfigEntry<int>[(int)Source.Count];
        internal static ConfigEntry<bool> scaleWithEnemies;
        internal static ConfigEntry<int> bonusPerExtraEnemy;
        internal static ConfigEntry<float> enemyRange;
        internal static ConfigEntry<bool> sprintGain;
        internal static ConfigEntry<float> sprintGainPerSecond;

        private void Awake()
        {
            instance = this;
            logger = Logger;

            modEnabled = Config.Bind("General", "Enabled", true, "Disabled: adrenaline behaves as in the unmodded game. All values below are percentages of the game's own values, 100 changes nothing.");

            requirementPercent = Config.Bind("Trigger", "Adrenaline required", 100, new ConfigDescription("Adrenaline an item needs before its effect triggers, as a percentage of the item's own value. 50 triggers every item at half its usual adrenaline, 200 at double.", new AcceptableValueRange<int>(1, 1000)));
            resizeBar = Config.Bind("Trigger", "Resize adrenaline bar", true, "Enabled: the adrenaline bar gets shorter or longer with the adrenaline required, as it does between items in the unmodded game. Disabled: the bar keeps the length the item would normally give it.");
            effectDurationPercent = Config.Bind("Trigger", "Effect duration", 100, new ConfigDescription("Duration of the effect an item triggers at full adrenaline. Applies to effects triggered after the change.", new AcceptableValueRange<int>(1, 1000)));

            allGainsPercent = Config.Bind("Gain", "All gains", 100, Percent("Applies to every adrenaline gain, on top of the settings for the individual sources below."));
            BindSource(Source.MeleeHit, "Gain", "Melee hit", "Adrenaline gained by hitting an enemy with a melee attack.");
            BindSource(Source.ProjectileHit, "Gain", "Projectile hit", "Adrenaline gained by hitting an enemy with an arrow, bolt, spell or other projectile.");
            BindSource(Source.Block, "Gain", "Block", "Adrenaline gained by blocking an attack.");
            BindSource(Source.Parry, "Gain", "Parry", "Adrenaline gained by parrying an attack.");
            BindSource(Source.PerfectDodge, "Gain", "Perfect dodge", "Adrenaline gained by dodging through an attack.");
            BindSource(Source.StaggerEnemy, "Gain", "Stagger enemy", "Adrenaline gained by staggering an enemy.");
            BindSource(Source.ForsakenPower, "Gain", "Forsaken power", "Adrenaline gained by activating a forsaken power.");
            BindSource(Source.StatusEffect, "Gain", "Status effects", "Adrenaline granted up front by status effects, such as consumables.");
            BindSource(Source.WeaponUse, "Gain", "Weapon use", "Adrenaline gained by using a weapon that grants it on every attack, whether it hits or not.");

            BindSource(Source.MissedAttack, "Loss", "Missed attack", "Adrenaline lost when a melee attack hits nothing. 0 removes the penalty.");
            BindSource(Source.DamageTaken, "Loss", "Damage taken", "Adrenaline lost when taking a hit without blocking it. 0 removes the penalty.");
            BindSource(Source.Decay, "Loss", "Decay rate", "Speed at which adrenaline drains away out of combat. 0 keeps adrenaline until it is used.");
            decayDelayPercent = Config.Bind("Loss", "Decay delay", 100, Percent("Time after the last adrenaline gain before adrenaline starts to drain."));

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

        private static ConfigDescription Percent(string description) => new ConfigDescription(description, new AcceptableValueRange<int>(0, 1000));

        private void BindSource(Source source, string section, string key, string description)
        {
            sourcePercent[(int)source] = Config.Bind(section, key, 100, Percent(description));
        }

        internal static float Multiplier(ConfigEntry<int> percent) => modEnabled.Value ? percent.Value / 100f : 1f;

        internal static float Multiplier(Source source) => Multiplier(sourcePercent[(int)source]);
    }
}
