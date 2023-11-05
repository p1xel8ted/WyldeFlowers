using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

namespace WyldeFlowers;

[Harmony]
[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public class Plugin : BasePlugin
{
    private const string PluginGuid = "p1xel8ted.wyldeflowers.tweaks";
    private const string PluginName = "Wylde Flowers Tweaks";
    private const string PluginVersion = "0.1.0";
    private static PlayerState PlayerState { get; set; }
    private static ManualLogSource Logger { get; set; }
    private static Harmony Harmony { get; set; }
    private static ConfigEntry<bool> SkipLogos { get; set; }
    private static ConfigEntry<int> StaminaInterval { get; set; }
    private static ConfigEntry<int> StaminaAmount { get; set; }
    private const float RunSpeed = 7f;
    private const string Player = "Player";
    private const string FrontendFarm = "frontend_farm";
    private static ConfigEntry<float> RunSpeedPercentIncrease { get; set; }
    private static ConfigEntry<bool> AlsoAdjustRunAnimationSpeed { get; set; }
    private static ConfigEntry<bool> PauseOnFocusLost { get; set; }
    private UpdateEvent UpdateEventInstance { get; set; }
    private static float _time;

    public class UpdateEvent : MonoBehaviour
    {
        public void Update()
        {
            if (PlayerState == null) return;


            if (Input.GetKeyUp(KeyCode.Plus) || Input.GetKeyUp(KeyCode.KeypadPlus))
            {
                RunSpeedPercentIncrease.Value += 5f;
                RunSpeedPercentIncrease.Value = Mathf.Clamp(RunSpeedPercentIncrease.Value, 0f, 500f);
            }
            else if (Input.GetKeyUp(KeyCode.Minus) || Input.GetKeyUp(KeyCode.KeypadMinus))
            {
                RunSpeedPercentIncrease.Value -= 5f;
                RunSpeedPercentIncrease.Value = Mathf.Clamp(RunSpeedPercentIncrease.Value, 0f, 500f);
            }

            if (Time.time > _time + StaminaInterval.Value)
            {
                if (PlayerState.stamina < PlayerState.maxStamina)
                {
                    PlayerState.stamina += StaminaAmount.Value;
                    Logger.LogInfo("Stamina: " + PlayerState.stamina);
                }

                _time = Time.time;
            }
        }
    }

    public override void Load()
    {
        SceneManager.sceneLoaded += (UnityAction<Scene, LoadSceneMode>) SceneManagerOnSceneLoaded;
        SkipLogos = Config.Bind("01. General", "Skip Logos", true, new ConfigDescription("Skip the intro logos.", null, new ConfigurationManagerAttributes {Order = 50}));
        PauseOnFocusLost = Config.Bind("01. General", "Pause On Focus Lost", false, new ConfigDescription("Pause the game when the window loses focus.", null, new ConfigurationManagerAttributes {Order = 51}));
        StaminaInterval = Config.Bind("02. Stamina", "Interval", 5, new ConfigDescription("Time in seconds between each stamina tick.", null, new ConfigurationManagerAttributes {Order = 49}));
        StaminaAmount = Config.Bind("02. Stamina", "Amount", 10, new ConfigDescription("Amount of stamina to add each tick.", null, new ConfigurationManagerAttributes {Order = 48}));
        RunSpeedPercentIncrease = Config.Bind("03. Movement", "Run Speed Percentage Increase", 25f, new ConfigDescription("Run speed multiplier. Default is a 25% speed increase.", new AcceptableValueRange<float>(0f, 500f), new ConfigurationManagerAttributes {ShowRangeAsPercent = true, Order = 47}));
        AlsoAdjustRunAnimationSpeed = Config.Bind("03. Movement", "Also Adjust Run Animation Speed", true, new ConfigDescription("Also adjust the run animation speed. This will make the run animation look more natural(?). Test both and see for yourself.", null, new ConfigurationManagerAttributes {Order = 46}));
        Logger = Log;
        Harmony = Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), PluginGuid);

        UpdateEventInstance = AddComponent<UpdateEvent>();

        Application.runInBackground = !PauseOnFocusLost.Value;
    }

    public override bool Unload()
    {
        SceneManager.sceneLoaded -= (UnityAction<Scene, LoadSceneMode>) SceneManagerOnSceneLoaded;
        UpdateEventInstance.Destroy();
        Harmony.UnpatchSelf();
        return base.Unload();
    }

    private static void SceneManagerOnSceneLoaded(Scene arg0, LoadSceneMode arg1)
    {
        var maxRefreshRate = Screen.resolutions.Max(a => a.refreshRate);
        Screen.SetResolution(Display._mainDisplay.systemWidth, Display._mainDisplay.systemHeight, FullScreenMode.FullScreenWindow, maxRefreshRate);
        Time.fixedDeltaTime = 1f / maxRefreshRate;
        Logger.LogInfo($"Set resolution to {Screen.currentResolution} and fixedDeltaTime to {Time.fixedDeltaTime}.");
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(SplashVideo), nameof(SplashVideo.Start))]
    private static void SplashVideo_Start()
    {
        if (SkipLogos.Value)
        {
            SplashVideo.Skip();
        }
    }

    private static SaveManager SaveManager { get; set; }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.Awake))]
    private static void SaveManager_Awake(ref SaveManager __instance)
    {
        SaveManager = __instance;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(CutscenePlayer), nameof(CutscenePlayer.Begin))]
    private static void CutscenePlayer_Begin(ref CutscenePlayer __instance)
    {
        if (__instance.cutscene.name.Equals(FrontendFarm))
        {
            var saveGameSlot = SaveManager._continueSlot;
            var saveGame = saveGameSlot.localFile.saveGame;
            Shell.instance.Load(saveGameSlot, saveGame, GameContext.InGame);
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlayerState), nameof(global::PlayerState.CreateSnapshot))]
    [HarmonyPatch(typeof(PlayerState), nameof(global::PlayerState.Restore))]
    private static void PlayerState_Start(ref PlayerState __instance)
    {
        PlayerState = __instance;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CharacterLocomotion), nameof(CharacterLocomotion.OnUpdate))]
    private static void CharacterLocomotion_OnUpdate(CharacterLocomotion __instance)
    {
        if (!__instance.actor.name.Equals(Player)) return;
        var speedMultiplier = 1 + RunSpeedPercentIncrease.Value / 100f;
        var newSpeed = RunSpeed * speedMultiplier;
        __instance.actor.movement.runSpeed = newSpeed;
        __instance.animations.runSpeed = AlsoAdjustRunAnimationSpeed.Value ? newSpeed : RunSpeed;
    }
    
    
    [HarmonyPostfix]
    [HarmonyPatch(typeof(InteractionTrigger), nameof(InteractionTrigger.OnTriggerExit))]
    [HarmonyPatch(typeof(InteractionTrigger), nameof(InteractionTrigger.OnTriggerEnter))]
    [HarmonyPatch(typeof(InteractionTrigger), nameof(InteractionTrigger.Refresh))]
    private static void InteractionTrigger_OnTriggerExit(ref InteractionTrigger __instance)
    {
        PlayerState = __instance.actor.player;
    }
}