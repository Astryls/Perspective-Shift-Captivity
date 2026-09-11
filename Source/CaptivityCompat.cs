using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace PerspectiveShiftCaptivity
{
    // Optional-mod integrations. Everything degrades to a clean no-op when the target
    // mod is absent. No hard dependencies; nothing NSFW is shipped or required. Detailed
    // design lives in _dev/PerspectiveShiftCaptivity/COMPAT_PLAN.md.
    [StaticConstructorOnStartup]
    public static class CaptivityCompat
    {
        public static readonly bool SlaveryOverhaul;
        public static readonly bool Rjw;
        public static readonly bool RjwSexualHarassment;
        public static readonly bool Restraints;
        public static readonly bool SimpleSlaveryCollars;

        // Slavery Overhaul hediffs, resolved softly by defName.
        private static readonly HediffDef SO_Brand;
        private static readonly HediffDef SO_TempInstitutionalised;
        private static readonly HediffDef SO_TempVengeful;

        static CaptivityCompat()
        {
            SlaveryOverhaul = ModLister.GetActiveModWithIdentifier("astryl.SlaveryOverhaul") != null;
            Rjw = ModLister.GetActiveModWithIdentifier("rim.job.world") != null;
            RjwSexualHarassment = ModLister.GetActiveModWithIdentifier("astryl.RJWSexualHarassment") != null;
            Restraints = ModLister.GetActiveModWithIdentifier("BDew.Restraints") != null;
            SimpleSlaveryCollars = ModLister.GetActiveModWithIdentifier("TRIBeagle.simpleslaverycollars") != null;

            if (SlaveryOverhaul)
            {
                SO_Brand = DefDatabase<HediffDef>.GetNamedSilentFail("SO_Brand");
                SO_TempInstitutionalised = DefDatabase<HediffDef>.GetNamedSilentFail("SO_TempInstitutionalised");
                SO_TempVengeful = DefDatabase<HediffDef>.GetNamedSilentFail("SO_TempVengeful");
            }

            CaptivityLog.Msg($"compat: SlaveryOverhaul={SlaveryOverhaul} RJW={Rjw} RJW-SH={RjwSexualHarassment} " +
                             $"Restraints={Restraints} Collars={SimpleSlaveryCollars}");
        }

        // Permanent Slavery Overhaul ownership brand (if present).
        public static void TryBrand(Pawn captive)
        {
            try
            {
                if (!SlaveryOverhaul || SO_Brand == null || captive?.health == null) return;
                if (captive.health.hediffSet.HasHediff(SO_Brand)) return;
                captive.health.AddHediff(SO_Brand);
            }
            catch (System.Exception e) { CaptivityLog.ErrorOnce("TryBrand failed: " + e, 74310201); }
        }

        private static ThingDef _collarDef;
        private static bool _collarResolved;

        // Prefer a Simple Slavery Collars metal collar, else the vanilla Ideology slave collar.
        public static ThingDef ResolveCollarDef()
        {
            if (_collarResolved) return _collarDef;
            _collarResolved = true;
            if (SimpleSlaveryCollars)
                _collarDef = DefDatabase<ThingDef>.GetNamedSilentFail("Apparel_SlaveCollar_Heavy")
                          ?? DefDatabase<ThingDef>.GetNamedSilentFail("Apparel_SlaveCollar_Tribal");
            if (_collarDef == null)
                _collarDef = DefDatabase<ThingDef>.GetNamedSilentFail("Apparel_Collar");
            return _collarDef;
        }

        // Lock a visible slave collar onto the captive as an ownership mark. No-op if no
        // collar apparel is available or one is already worn.
        public static void TryFitCollar(Pawn captive)
        {
            try
            {
                if (captive?.apparel == null) return;
                ThingDef def = ResolveCollarDef();
                if (def == null) return;
                for (int i = 0; i < captive.apparel.WornApparelCount; i++)
                    if (captive.apparel.WornApparel[i].def == def) return;

                Thing t = def.MadeFromStuff
                    ? ThingMaker.MakeThing(def, GenStuff.DefaultStuffFor(def))
                    : ThingMaker.MakeThing(def);
                if (t is Apparel app)
                    captive.apparel.Wear(app, dropReplacedApparel: false, locked: true);
            }
            catch (System.Exception e) { CaptivityLog.ErrorOnce("TryFitCollar failed: " + e, 74310203); }
        }

        public static bool IsCollared(Pawn captive)
        {
            ThingDef def = ResolveCollarDef();
            if (def == null || captive?.apparel == null) return false;
            for (int i = 0; i < captive.apparel.WornApparelCount; i++)
                if (captive.apparel.WornApparel[i].def == def) return true;
            return false;
        }

        public static void RemoveCollar(Pawn captive)
        {
            try
            {
                ThingDef def = ResolveCollarDef();
                if (def == null || captive?.apparel == null) return;
                for (int i = captive.apparel.WornApparelCount - 1; i >= 0; i--)
                {
                    Apparel a = captive.apparel.WornApparel[i];
                    if (a.def == def) { captive.apparel.Remove(a); a.Destroy(); }
                }
            }
            catch (System.Exception e) { CaptivityLog.ErrorOnce("RemoveCollar failed: " + e, 74310204); }
        }

        // Reflect the captive's psychology as a Slavery Overhaul temperament (if present):
        // high obedience over time reads as institutionalised, low as vengeful.
        public static void UpdateTemperament(Pawn captive, int obedience)
        {
            try
            {
                if (!SlaveryOverhaul || captive?.health == null) return;
                HediffDef want = obedience >= 80 ? SO_TempInstitutionalised
                               : obedience <= 20 ? SO_TempVengeful
                               : null;
                SetExclusive(captive, want, SO_TempInstitutionalised, SO_TempVengeful);
            }
            catch (System.Exception e) { CaptivityLog.ErrorOnce("UpdateTemperament failed: " + e, 74310202); }
        }

        private static void SetExclusive(Pawn p, HediffDef want, params HediffDef[] group)
        {
            for (int i = 0; i < group.Length; i++)
            {
                HediffDef d = group[i];
                if (d == null) continue;
                Hediff h = p.health.hediffSet.GetFirstHediffOfDef(d);
                if (d == want)
                {
                    if (h == null) p.health.AddHediff(d);
                }
                else if (h != null)
                {
                    p.health.RemoveHediff(h);
                }
            }
        }

        // Rimpsyche "Tenacity" as 0..1 (0 pliable, 1 iron willed). 0 when Rimpsyche is absent.
        // Reflection bridge mirrors Slavery Overhaul's RimpsycheBridge shape.
        private static bool _rpTried;
        private static MethodInfo _rpGetComp, _rpGetPersonality;
        private static PropertyInfo _rpPersonalityProp;
        private static object _rpWillDef;

        public static float WillStrength(Pawn p)
        {
            try
            {
                if (!_rpTried)
                {
                    _rpTried = true;
                    var pcm = AccessTools.TypeByName("Maux36.RimPsyche.PsycheCacheManager");
                    _rpGetComp = pcm != null ? AccessTools.Method(pcm, "GetCompPsycheCached", new[] { typeof(Pawn) }) : null;
                    _rpPersonalityProp = AccessTools.TypeByName("Maux36.RimPsyche.CompPsyche")?.GetProperty("Personality");
                    var perT = AccessTools.TypeByName("Maux36.RimPsyche.Pawn_PersonalityTracker");
                    var pdefT = AccessTools.TypeByName("Maux36.RimPsyche.PersonalityDef");
                    if (perT != null && pdefT != null)
                        _rpGetPersonality = AccessTools.Method(perT, "GetPersonality", new[] { pdefT });
                    if (pdefT != null)
                        _rpWillDef = GenDefDatabase.GetDefSilentFail(pdefT, "Rimpsyche_Tenacity", false);
                }
                if (p == null || _rpWillDef == null || _rpGetComp == null || _rpPersonalityProp == null || _rpGetPersonality == null)
                    return 0f;
                var comp = _rpGetComp.Invoke(null, new object[] { p });
                var pers = comp == null ? null : _rpPersonalityProp.GetValue(comp);
                if (pers == null) return 0f;
                if (_rpGetPersonality.Invoke(pers, new[] { _rpWillDef }) is float f)
                    return f > 0f ? Mathf.Clamp01(f) : 0f;
            }
            catch { }
            return 0f;
        }

        // Hook for later: RJW-SH is colony-centric and normally will not act on an AI captor
        // map, so we keep our SFW harass pillar. If we ever detect it acting on the captive we
        // can flip this to avoid doubling. Currently always false.
        public static bool SuppressOwnHarass => false;
    }
}
