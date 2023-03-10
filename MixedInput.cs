using HarmonyLib;
using BepInEx;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection;
using UnityEngine;

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
            Debug.Log("[MixedInput] Mod Loaded Successfully");
        }
    }

    [HarmonyPatch(typeof(GameInput), "UpdateAxisValues")]
    //[HarmonyDebug]
    public static class MixedInput_Patch
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il)
        {
            int passed = 0;
            int loopStartIndex = -1;
            int startRemoveIndex = -1;

            var codes = new List<CodeInstruction>(instructions);

            for (var i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Ldc_I4_0)
                    passed++;
                if (loopStartIndex == -1 && passed == 3)
                    loopStartIndex = i;
                else if (loopStartIndex != -1 && codes[i].opcode == OpCodes.Ldc_R4)
                    if (codes[i].operand.ToString() == "0")
                    {
                        startRemoveIndex = i - 3;
                        break;
                    }
            }
            Debug.Log($"[MixedInput] Start of UpdateAxisValues() end loop: {loopStartIndex}");

            if (startRemoveIndex != -1)
                codes.RemoveRange(startRemoveIndex, 5);
            else
                return instructions;

            Label retLabel = il.DefineLabel();
            codes.Last().labels.Add(retLabel);

            var instructionsToInsert = new List<CodeInstruction>();
            instructionsToInsert.Add(new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(GameInput), "lastDevice")));
            instructionsToInsert.Add(new CodeInstruction(OpCodes.Brtrue, retLabel));
            codes.InsertRange(loopStartIndex, instructionsToInsert);

            startRemoveIndex += instructionsToInsert.Count;
            //Debug.Log($"[MixedInput] Opcode at startIndex {startIndex}: {codes[startIndex].opcode}");

            Label fixedJumpLabel = il.DefineLabel();
            codes[startRemoveIndex].labels.Add(fixedJumpLabel);

            for (var i = loopStartIndex; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Beq)
                    codes[i] = new CodeInstruction(OpCodes.Beq, fixedJumpLabel);
                else if (codes[i].opcode == OpCodes.Ble_Un)
                    codes[i] = new CodeInstruction(OpCodes.Ble_Un, fixedJumpLabel);
                else
                    continue;
                Debug.Log($"[MixedInput] Replacing opcode {codes[i].opcode} jump at {i}");
            }

            return codes.AsEnumerable();
        }
    }
}