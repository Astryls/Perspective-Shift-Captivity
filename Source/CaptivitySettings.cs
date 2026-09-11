using UnityEngine;
using Verse;

namespace PerspectiveShiftCaptivity
{
    public class CaptivitySettings : ModSettings
    {
        public bool permadeathByDefault = false;
        public bool allowSelling = true;
        public bool lethalOnEscape = true;
        public bool enableStruggleMinigame = true;
        public bool showHud = true;
        public bool captorsFeedYou = true;
        public float disciplineIntervalDays = 1.2f;
        public float harassIntervalDays = 0.6f;
        public float sellIntervalDays = 3.5f;
        public float sellChancePerCheck = 0.35f;
        public bool allowOrganHarvest = true;
        public float organHarvestIntervalDays = 8f;
        public float organHarvestChance = 0.35f;

        public void DoWindow(Rect inRect)
        {
            var l = new Listing_Standard();
            l.Begin(inRect);

            l.CheckboxLabeled("PSC_Set_Permadeath".Translate(), ref permadeathByDefault, "PSC_Set_PermadeathDesc".Translate());
            l.CheckboxLabeled("PSC_Set_AllowSelling".Translate(), ref allowSelling, "PSC_Set_AllowSellingDesc".Translate());
            l.CheckboxLabeled("PSC_Set_LethalOnEscape".Translate(), ref lethalOnEscape, "PSC_Set_LethalOnEscapeDesc".Translate());
            l.CheckboxLabeled("PSC_Set_Minigame".Translate(), ref enableStruggleMinigame, "PSC_Set_MinigameDesc".Translate());
            l.CheckboxLabeled("PSC_Set_ShowHud".Translate(), ref showHud, "PSC_Set_ShowHudDesc".Translate());
            l.CheckboxLabeled("PSC_Set_Feed".Translate(), ref captorsFeedYou, "PSC_Set_FeedDesc".Translate());
            l.Gap();

            l.Label("PSC_Set_DisciplineInterval".Translate(disciplineIntervalDays.ToString("0.0")));
            disciplineIntervalDays = l.Slider(disciplineIntervalDays, 0.2f, 5f);

            l.Label("PSC_Set_HarassInterval".Translate(harassIntervalDays.ToString("0.0")));
            harassIntervalDays = l.Slider(harassIntervalDays, 0.1f, 3f);

            l.Label("PSC_Set_SellInterval".Translate(sellIntervalDays.ToString("0.0")));
            sellIntervalDays = l.Slider(sellIntervalDays, 1f, 15f);

            l.Label("PSC_Set_SellChance".Translate(Mathf.RoundToInt(sellChancePerCheck * 100f)));
            sellChancePerCheck = l.Slider(sellChancePerCheck, 0f, 1f);

            l.Gap();
            l.CheckboxLabeled("PSC_Set_AllowHarvest".Translate(), ref allowOrganHarvest, "PSC_Set_AllowHarvestDesc".Translate());
            if (allowOrganHarvest)
            {
                l.Label("PSC_Set_HarvestInterval".Translate(organHarvestIntervalDays.ToString("0.0")));
                organHarvestIntervalDays = l.Slider(organHarvestIntervalDays, 2f, 30f);
                l.Label("PSC_Set_HarvestChance".Translate(Mathf.RoundToInt(organHarvestChance * 100f)));
                organHarvestChance = l.Slider(organHarvestChance, 0f, 1f);
            }

            l.End();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref permadeathByDefault, "permadeathByDefault", false);
            Scribe_Values.Look(ref allowSelling, "allowSelling", true);
            Scribe_Values.Look(ref lethalOnEscape, "lethalOnEscape", true);
            Scribe_Values.Look(ref enableStruggleMinigame, "enableStruggleMinigame", true);
            Scribe_Values.Look(ref showHud, "showHud", true);
            Scribe_Values.Look(ref captorsFeedYou, "captorsFeedYou", true);
            Scribe_Values.Look(ref disciplineIntervalDays, "disciplineIntervalDays", 1.2f);
            Scribe_Values.Look(ref harassIntervalDays, "harassIntervalDays", 0.6f);
            Scribe_Values.Look(ref sellIntervalDays, "sellIntervalDays", 3.5f);
            Scribe_Values.Look(ref sellChancePerCheck, "sellChancePerCheck", 0.35f);
            Scribe_Values.Look(ref allowOrganHarvest, "allowOrganHarvest", true);
            Scribe_Values.Look(ref organHarvestIntervalDays, "organHarvestIntervalDays", 8f);
            Scribe_Values.Look(ref organHarvestChance, "organHarvestChance", 0.35f);
        }
    }
}
