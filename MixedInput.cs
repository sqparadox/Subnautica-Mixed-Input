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
            int ldc_i4_0Passed = 0;
            int axisCheckLoopStartIndex = -1;
            int startZeroAxisIndex = -1;

            var codes = new List<CodeInstruction>(instructions);

            for (var i = 0; i < codes.Count; i++)
            {
                //count the loops by looking for int i=0;
                if (codes[i].opcode == OpCodes.Ldc_I4_0)
                    ldc_i4_0Passed++;
                //Once we've hit the 3rd loop, ensure loop is checking axes by checking that call is where expected
                if (axisCheckLoopStartIndex == -1 && ldc_i4_0Passed == 3 && codes[i + 7].opcode == OpCodes.Call)
                    axisCheckLoopStartIndex = i;
                //if call is not where expected we aren't in the axis check loop, code has changed abandon transpiler
                else if (axisCheckLoopStartIndex == -1 && ldc_i4_0Passed == 3 && codes[i + 7].opcode != OpCodes.Call)
                    return instructions;
                //once we know we are in the axis check loop look for the zeroing of the axis
                //ensure we are in the right place by checking the opcode at the start of the else
                else if (axisCheckLoopStartIndex != -1 && codes[i].opcode == OpCodes.Ldc_R4)
                {
                    if (codes[i].operand.ToString() == "0" && codes[i - 3].opcode == OpCodes.Br)
                    {
                        startZeroAxisIndex = i - 3;
                        break;
                    }
                }
            }

            Debug.Log($"[MixedInput] Start of UpdateAxisValues() axis dif check loop: {axisCheckLoopStartIndex}");

            //add label to return at end of method
            Label retLabel = il.DefineLabel();
            codes.Last().labels.Add(retLabel);

            //add check if last device is controller to skip the axis check loop
            //since we only want to swap from keyboard to controller, not the other way as the UI will flicker
            //we can still add this even if the zeroing of axes wasn't found
            var instructionsToInsert = new List<CodeInstruction>
            {
                new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(GameInput), "lastDevice")),
                new CodeInstruction(OpCodes.Brtrue, retLabel)
            };
            codes.InsertRange(axisCheckLoopStartIndex, instructionsToInsert);

            //if we found axes being zeroed
            if (startZeroAxisIndex != -1)
            {
                //adjust indexes for inserted code
                axisCheckLoopStartIndex += instructionsToInsert.Count;
                startZeroAxisIndex += instructionsToInsert.Count;
                //remove zxis zeroing code
                codes.RemoveRange(startZeroAxisIndex, 5);

                //add label to fix jump that pointed to else we just removed 
                Label fixedJumpLabel = il.DefineLabel();
                codes[startZeroAxisIndex].labels.Add(fixedJumpLabel);

                //go through the axius check loop and look for the jump we just broke
                for (var i = axisCheckLoopStartIndex; i < codes.Count; i++)
                {
                    if (codes[i].opcode == OpCodes.Ble_Un)
                    {
                        //overwrite with a new instruction fixing the jump
                        codes[i] = new CodeInstruction(OpCodes.Ble_Un, fixedJumpLabel);
                        Debug.Log($"[MixedInput] Replacing opcode {codes[i].opcode} jump at {i}");
                        break;
                    }
                }
            }

            return codes.AsEnumerable();
        }
    }
}