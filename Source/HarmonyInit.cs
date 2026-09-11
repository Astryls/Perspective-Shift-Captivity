using System;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace PerspectiveShiftCaptivity
{
    // Resilient boot: patch each [HarmonyPatch] class independently so a single
    // dead target in the separately-versioned Perspective Shift dependency cannot
    // take down every other patch. Runs from our own static ctor (late phase).
    [StaticConstructorOnStartup]
    public static class HarmonyInit
    {
        public static readonly Harmony Harmony;

        static HarmonyInit()
        {
            Harmony = new Harmony("astryl.perspectiveshiftcaptivity");

            Type[] types;
            try { types = typeof(HarmonyInit).Assembly.GetTypes(); }
            catch (ReflectionTypeLoadException e) { types = Array.FindAll(e.Types, t => t != null); }

            int patched = 0;
            foreach (var type in types)
            {
                if (!type.IsDefined(typeof(HarmonyPatch), false)) continue;
                try { Harmony.CreateClassProcessor(type).Patch(); patched++; }
                catch (Exception ex) { CaptivityLog.Warn("skipped patch class " + type.Name + ": " + ex.Message); }
            }
            CaptivityLog.Msg("applied " + patched + " patch class(es).");
        }
    }
}
