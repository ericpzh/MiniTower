using BepInEx;
using BepInEx.Logging;
using DG.Tweening;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MiniTower
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {   
        private void Awake()
        {
            Log = Logger;

            // Plugin startup logic
            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
            
            SceneManager.sceneLoaded += OnSceneLoaded;
            Harmony harmony = new Harmony(PluginInfo.PLUGIN_GUID);
            harmony.PatchAll();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Logger.LogInfo($"Scene loaded: {scene.name}");
        }
        internal static ManualLogSource Log;
    }

    // Stop aircraft.
    [HarmonyPatch(typeof(Aircraft), "OnPointUp", new Type[] {})]
    class PatchOnPointUp
    {
        static bool Prefix(ref Aircraft __instance)
        {
            if (__instance.targetSpeed == 0f)
            {
                __instance.targetSpeed = 24f;
            }
            else
            {
                __instance.targetSpeed = 0f;
            }
            return true;
        }
    }

    // Disable control on landing aircraft.
    [HarmonyPatch(typeof(Aircraft), "OnPointDown", new Type[] {})]
    class PatchOnPointDown
    {
        static bool Prefix(ref Aircraft __instance)
        {
            if (__instance.direction == Aircraft.Direction.Inbound && __instance.speed > 50f)
            {
                return false;
            }
            return true;
        }
    }

    // Resume moving.
    [HarmonyPatch(typeof(Aircraft), "SetFlyHeading", new Type[] { typeof(float) })]
    class PatchSetFlyHeadingFloat
    {
        static bool Prefix(float heading, ref Aircraft __instance)
        {
            __instance.targetSpeed = 24f;
            return true;
        }
    }

    // Resume moving.
    [HarmonyPatch(typeof(Aircraft), "SetFlyHeading", new Type[] {})]
    class PatchSetFlyHeading
    {
        static bool Prefix(ref Aircraft __instance)
        {
            __instance.targetSpeed = 24f;
            return true;
        }
    }

    // Resume moving.
    [HarmonyPatch(typeof(Aircraft), "SetVectorTo", new Type[] {typeof(Transform)})]
    class PatchSetVectorTo
    {
        static bool Prefix(Transform wayPoint, ref Aircraft __instance)
        {
            __instance.targetSpeed = 24f;
            return true;
        }
    }

    // Resume moving.
    [HarmonyPatch(typeof(Aircraft), "SetVectorTo", new Type[] {typeof(PlaceableWaypoint)})]
    class PatchSetVectorToPlaceableWaypoint
    {
        static bool Prefix(PlaceableWaypoint waypoint, ref Aircraft __instance)
        {
            __instance.targetSpeed = 24f;
            return true;
        }
    }

    // Resume moving.
    [HarmonyPatch(typeof(Aircraft), "SetVectorTo", new Type[] {typeof(WaypointAutoHover)})]
    class PatchSetVectorToWaypointAutoHover
    {
        static bool Prefix(WaypointAutoHover waypoint,  ref Aircraft __instance)
        {
            __instance.targetSpeed = 24f;
            return true;
        }
    }

    // Resume moving.
    [HarmonyPatch(typeof(Aircraft), "TrySetupLanding", new Type[] {typeof(Runway), typeof(bool)})]
    class PatchTrySetupLanding
    {
        static bool Prefix(Runway runway, bool doLand, ref Aircraft __instance)
        {
            __instance.targetSpeed = 24f;
            return true;
        }
    }

    // Initial setup of each aircraft.
    [HarmonyPatch(typeof(Aircraft), "Start", new Type[] {})]
    class PatchAircraftStart
    {
        static void Postfix(ref Aircraft __instance)
        {
            // Set acceleration. & inbound speed
            var bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var field = __instance.GetType().GetField("acceleration", bindingFlags);
            field.SetValue(__instance, 0.1f);

            if (__instance.direction == Aircraft.Direction.Inbound)
            {
                // Set inbound speed.
                __instance.speed = 345f;
            }
        }
    }

    // Disable indicator.
    [HarmonyPatch(typeof(Aircraft), "Update", new Type[] {})]
    class PatchAircraftUpdate
    {
        static void Postfix(ref Aircraft __instance)
        {
            __instance.APStatTri.gameObject.SetActive(false);
        }
    }

    // Disable go-around.
    [HarmonyPatch]
    public class PatchLandCoroutine
    {
        [HarmonyPatch(typeof(Aircraft), "LandCoroutine")]
        [HarmonyPrefix]
        public static bool LandCoroutinePrefix(Aircraft __instance)
        {
            float distanceFromLanding = ((Vector2)__instance.gameObject.transform.position - __instance.landingStartPoint).magnitude;
            if (distanceFromLanding >= 0.1f)
            {
                return true;
            }
            __instance.ConditionalDestroy();
            return false;
        }
    }

    // Do not allow OOB.
    [HarmonyPatch(typeof(Aircraft), "AircraftOOBGameOver", new Type[] {typeof(Aircraft)})]
    class PatchAircraftOOBGameOver
    {
        static bool Prefix(Aircraft aircraft)
        {
            return false;
        }
    }

    // You can always land.
    // [HarmonyPatch(typeof(Aircraft), "GenerateLandingPathL1", 
    //     new Type[] {typeof(Runway), typeof(List<Vector3>), typeof(bool), typeof(bool)}, 
    //     [ArgumentType.Normal, ArgumentType.Out, ArgumentType.Normal, ArgumentType.Normal])]
    // class PatchGenerateLandingPathL1
    // {
    //     static void Postfix(Runway LandingRunway, List<Vector3> path, bool EarlyQuit, bool useExtGuide, ref bool __result)
    //     {
    //         __result = true;
    //     }
    // }

    // Stop aircraft from turning into 090 when stopped.
    [HarmonyPatch(typeof(Aircraft), "GetHeadingHARW", new Type[] {})]
    class PatchGetHeadingHARWDefault
    {
        static bool Prefix(ref Aircraft __instance, ref object[] __state)
        {
            __state = new object[] {__instance.speed, __instance.targetSpeed, __instance.heading};
            return true;
        }

        static void Postfix(ref Aircraft __instance, ref float __result, ref object[] __state)
        {
            if (__state == null || __state.Length != 3 || __instance.targetSpeed != 0)
            {
                return;
            }
            __result = (float)__state[2];
        }
    }

    // Fix of NullReferenceException here when an aircraft stopped.
    [HarmonyPatch(typeof(Aircraft), "OnTriggerEnter2D", new Type[] {typeof(Collider2D)})]
    class PatchOnTriggerEnter2D
    {
        static bool Prefix(Collider2D other, ref Aircraft __instance)
        {
            if (__instance == null || other == null)
            {
                return false;
            }
            return true;
        }
    }

    // Make it less punishing when colloiding with aircraft that's landing fast.
    [HarmonyPatch(typeof(Aircraft), "AircraftCollideGameOver", new Type[] {typeof(Aircraft), typeof(Aircraft)})]
    class PatchAircraftCollideGameOver
    {
        static bool Prefix(Aircraft aircraft1, Aircraft aircraft2)
        {
            if (aircraft1.speed > 50f || aircraft2.speed > 50f)
            {
                return false;
            }
            return true;
        }
    }

    // Randomly assign the two available color at start, more goes to the inner hand-off point.
    [HarmonyPatch(typeof(TakeoffTask), "Start", new Type[] {})]
    class PatchTakeoffTaskStart
    {
        static bool Prefix(ref TakeoffTask __instance)
        {
            // Set outbound color.
            if (UnityEngine.Random.value < 0.75f)
            {
                __instance.colorCode = ColorCode.Option.Red;
            }
            else
            {
                __instance.colorCode = ColorCode.Option.Yellow;
            }
            return true;
        }
    }

    // Fully upgrade before starting.
    [HarmonyPatch(typeof(UpgradeManager), "Start", new Type[] {})]
    class PatchUpgradeManagerStart
    {
        static void Postfix(ref UpgradeManager __instance, ref int[] ___counter)
        {
            if (___counter.Length == 0)
            {
                return;
            }

            // Starts with 3 apron upgrade.
            for (int i = 0; i < 3; i++)
            {
                ___counter[(int)UpgradeOpt.LONGER_TAXIWAY]++;
                TakeoffTaskManager.Instance.AddApron();
            }

            // Max-out all airspace.
            Camera.main.DOOrthoSize(LevelManager.Instance.maximumCameraOrthographicSize * 1.15f, 0.5f).SetUpdate(isIndependentUpdate: true);

            Aircraft.TurnSpeed = 0.09f;
        }
    }

    // Remove not useful upgrades.
    [HarmonyPatch(typeof(UpgradeManager), "ProcessOptionProbs", new Type[] {})]
    class PatchProcessOptionProbs
    {
        static void Postfix(ref List<float> __result)
        {
            __result[3] = 0; // TURN_FASTER
            __result[4] = 0; // AIRSPACE
            __result[7] = 0; // AUTO_HOVERING_PROP
        }
    }

    // Always spawn with exact direction.
    [HarmonyPatch(typeof(AircraftManager), "CreateInboundAircraft", new Type[] {typeof(Vector3), typeof(float)})]
    class PatchCreateInboundAircraft
    {
        static void Postfix(Vector3 position, float heading, ref Aircraft __result)
        {
            __result.heading = 90f;
        }
    }

    // Do not allow camera size change.
    [HarmonyPatch(typeof(LevelManager), "Start", new Type[] {})]
    class PatchLevelManagerStart
    {
        static void Postfix()
        {
            LevelManager.CameraSizeIncByFailGenWaypoint = 0f;
        }
    }

    // Spawn the unique destination waypoint.
    [HarmonyPatch(typeof(WaypointManager), "Start", new Type[] {})]
    class PatchWaypointManagerStart
    {
        static void GenerateWaypoint(Vector3 position, ColorCode.Option color, ref WaypointManager __instance)
        {
            GameObject newWaypoint = UnityEngine.Object.Instantiate<GameObject>((GameObject)(__instance.WaypointCirclePrefab), ((Component)__instance).transform.position, Quaternion.identity);
            Waypoint component = newWaypoint.GetComponent<Waypoint>();
            component.colorCode = color;
            component.shapeCode = ShapeCode.Option.Circle;
            component.SetColor();
            component.AutoDestroy = false;

            newWaypoint.gameObject.transform.position = position;
            newWaypoint.gameObject.transform.localScale = Vector3.one * 0.4f;
            Tweener tweener = newWaypoint.gameObject.transform.DOScale(0.6f, 0.3f);
            tweener.onComplete = (TweenCallback)Delegate.Combine(tweener.onComplete, (TweenCallback)delegate
            {
                newWaypoint.gameObject.transform.DOScale(0.4f, 0.2f);
            });
            newWaypoint.transform.SetParent(((Component)__instance).transform);
            Vector3 position2 = newWaypoint.transform.position;
            position2.z = 0f;
            newWaypoint.transform.position = position2;
        }

        static void Postfix(ref WaypointManager __instance)
        {
            GenerateWaypoint(new Vector3(-22.65f, 4f, -9f), ColorCode.Option.Red, ref __instance);
            GenerateWaypoint(new Vector3(-22.65f, 7.5f, -9f), ColorCode.Option.Yellow, ref __instance);
        }
    }

    // Do not allow new destination waypoint to spawn.
    [HarmonyPatch(typeof(WaypointManager), "CreateNewWaypoint", new Type[] {
        typeof(Vector3), typeof(ColorCode.Option), typeof(ShapeCode.Option), typeof(bool), typeof(bool)})]
    class PatchCreateNewWaypoint
    {
        static bool Prefix(Vector3 position, ColorCode.Option colorOption, ShapeCode.Option shapeOption,
                           bool AutoDestroy, bool checkExist)
        {
            return false;
        }
    }

    // Do not allow new destination waypoint to spawn.
    [HarmonyPatch(typeof(WaypointManager), "CreateNewWaypoint", new Type[] {})]
    class PatchCreateNewWaypointDefault
    {
        static bool Prefix()
        {
            return false;
        }
    }

    // Do not allow new destination waypoint to spawn.
    [HarmonyPatch(typeof(WaypointManager), "CreateNewWaypoint", new Type[] {typeof(Vector3), typeof(bool)})]
    class PatchCreateNewWaypointVector
    {
        static bool Prefix(Vector3 position, bool AutoDestroy)
        {
            return false;
        }
    }

    // Do not allow new destination waypoint to spawn.
    [HarmonyPatch(typeof(WaypointManager), "CreateNewWaypoint", new Type[] {typeof(ColorCode.Option), typeof(ShapeCode.Option)})]
    class PatchCreateNewWaypointCode
    {
        static bool Prefix(ColorCode.Option color, ShapeCode.Option shape)
        {
            return false;
        }
    }

    // Do not allow fixed destination waypoint to disappear.
    [HarmonyPatch(typeof(WaypointManager), "HasSameColorShapeWaypoint", new Type[] {typeof(ColorCode.Option), typeof(ShapeCode.Option)})]
    class PatchHasSameColorShapeWaypoint
    {
        static void Postfix(ColorCode.Option colorCode, ShapeCode.Option shapeCode, ref bool __result)
        {
            __result = true;
        }
    }

    // Do not show directional arrow.
    [HarmonyPatch(typeof(RestrictedLineIndicator), "Update", new Type[] {})]
    class PatchRestrictedLineIndicatorUpdate
    {
        static bool Prefix(ref RestrictedLineIndicator __instance)
        {
            __instance.TArrow.gameObject.SetActive(false);
            __instance.FArrow.gameObject.SetActive(false);
            return false;
        }
    }
}
