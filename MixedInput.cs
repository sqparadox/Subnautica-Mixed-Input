using HarmonyLib;
using BepInEx;
using System.Reflection;

namespace MixedInput
{
    [BepInPlugin(GUID, PluginName, VersionString)]
    public class Main : BaseUnityPlugin
    {
        private const string PluginName = "SubnauticaMixedInput";
        private const string VersionString = "1.0.0";
        private const string GUID = "com.sqparadox.Subnautica.MixedInput";

        private void Awake()
        {
            Harmony harmony = new Harmony(GUID);
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
    }

    [HarmonyPatch(typeof(GameInput), "UpdateAxisValues")]
    public static class MixedInput_Patch
    {
        public static void Postfix(bool useKeyboard, bool useController, ref GameInput.Device ___lastDevice, ref float[] ___axisValues)
        {
            if (!useController || !useKeyboard)
                return;
            
            ___lastDevice = GameInput.Device.Controller;

            ___axisValues[10] = UnityEngine.Input.GetAxis("Mouse ScrollWheel");
            ___axisValues[8] = UnityEngine.Input.GetAxisRaw("Mouse X");
            ___axisValues[9] = UnityEngine.Input.GetAxisRaw("Mouse Y");
        }
    }
}