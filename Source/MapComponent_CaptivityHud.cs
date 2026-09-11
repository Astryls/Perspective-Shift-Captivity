using System;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace PerspectiveShiftCaptivity
{
    // Small over-map status panel: owner, status, defiance, how closely you are watched, and
    // your restraints. Follows the suite over-map rules (opaque plate, 1px border, a single
    // left state strip, one status ramp, Small font, sentence case). Auto-created per map.
    public class MapComponent_CaptivityHud : MapComponent
    {
        private static readonly Color PanelBG = new Color(0.106f, 0.117f, 0.137f, 0.92f);
        private static readonly Color Well = new Color(0.06f, 0.06f, 0.07f, 0.92f);
        private static readonly Color Border = new Color(0.18f, 0.20f, 0.22f);
        private static readonly Color Stat = new Color(0.89f, 0.89f, 0.89f);
        private static readonly Color TextDim = new Color(0.62f, 0.65f, 0.70f);
        private static readonly Color Good = new Color(0.40f, 0.85f, 0.40f);
        private static readonly Color Warn = new Color(0.95f, 0.65f, 0.20f);
        private static readonly Color Bad = new Color(0.90f, 0.35f, 0.35f);

        public MapComponent_CaptivityHud(Map map) : base(map) { }

        public override void MapComponentOnGUI()
        {
            if (CaptivityMod.Settings != null && !CaptivityMod.Settings.showHud) return;
            if (Find.CurrentMap != map) return;

            GameComponent_Captivity comp = GameComponent_Captivity.Comp;
            if (comp == null || !comp.Active) return;
            Pawn captive = comp.captive;
            if (captive == null || captive.MapHeld != map) return;

            try { Draw(comp, captive); }
            catch (Exception e) { CaptivityLog.ErrorOnce("HUD draw failed: " + e, 74310401); }
            finally
            {
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
            }
        }

        private void Draw(GameComponent_Captivity comp, Pawn captive)
        {
            const float w = 244f;
            bool held = comp.status == CaptivityStatus.Held;
            bool shackled = captive.health?.hediffSet?.HasHediff(PSCDefOf.PSC_Shackled) ?? false;
            bool restrained = shackled || CaptivityCompat.IsCollared(captive);
            Building_Door pickDoor = held ? CaptivityUtility.FindPickableDoor(comp, captive) : null;
            Thing stealFood = held ? CaptivityUtility.FindStealableFood(captive) : null;

            float h = 10f + 26f + 24f + 32f + 32f + 22f + 22f;
            if (held)
            {
                h += 6f + 30f;
                if (restrained) h += 30f;
                if (pickDoor != null) h += 30f;
                if (stealFood != null) h += 30f;
            }
            h += 8f;
            Rect panel = new Rect(8f, 60f, w, h);

            Widgets.DrawBoxSolid(panel, PanelBG);
            GUI.color = Border;
            Widgets.DrawBox(panel, 1);
            GUI.color = Color.white;

            Color heatCol = Ramp(comp.heat, invert: true);       // high heat = bad
            Widgets.DrawBoxSolid(new Rect(panel.x, panel.y, 3f, panel.height), heatCol);

            Rect inner = panel.ContractedBy(10f);
            inner.x += 3f; inner.width -= 3f;
            float cy = inner.y;

            Text.Font = GameFont.Small;
            GUI.color = Stat;
            Widgets.Label(new Rect(inner.x, cy, inner.width, 24f), "PSC_Hud_Title".Translate());
            cy += 26f;

            GUI.color = TextDim;
            Widgets.Label(new Rect(inner.x, cy, inner.width, 22f), StatusLine(comp));
            cy += 24f;
            GUI.color = Color.white;

            int defiance = 100 - comp.obedience;
            cy = DrawBar(inner, cy, "PSC_Hud_Defiance".Translate(), defiance / 100f, Ramp(defiance, invert: false));
            cy = DrawBar(inner, cy, "PSC_Hud_Heat".Translate(), comp.heat / 100f, heatCol);

            GUI.color = TextDim;
            Widgets.Label(new Rect(inner.x, cy, inner.width, 20f), RestraintLine(captive));
            cy += 20f;

            GUI.color = Ramp(comp.heat, invert: true);
            Widgets.Label(new Rect(inner.x, cy, inner.width, 20f), EscapeHint(comp.heat));
            GUI.color = Color.white;
            cy += 22f;

            if (!held) return;
            cy += 4f;

            if (pickDoor != null)
            {
                Rect pkb = new Rect(inner.x, cy, inner.width, 26f);
                cy += 30f;
                if (Widgets.ButtonText(pkb, "PSC_Btn_Pick".Translate()))
                    CaptivityDirector.StartLockpick(comp, pickDoor);
            }

            if (stealFood != null)
            {
                Rect fb = new Rect(inner.x, cy, inner.width, 26f);
                cy += 30f;
                int pct = Mathf.RoundToInt(CaptivityDirector.StealChance(comp) * 100f);
                if (Widgets.ButtonText(fb, "PSC_Btn_Steal".Translate(stealFood.LabelNoCount, pct)))
                    CaptivityDirector.TryStealFood(comp, stealFood);
            }

            if (restrained)
            {
                Rect rb = new Rect(inner.x, cy, inner.width, 26f);
                cy += 30f;
                bool canWork = CaptivityUtility.CountNearbyCaptors(comp, 8f) == 0;
                if (Widgets.ButtonText(rb, "PSC_Btn_Work".Translate(), active: canWork) && canWork)
                    OpenRestraintStruggle(comp, shackled);
                if (!canWork) TooltipHandler.TipRegion(rb, "PSC_Tip_Watched".Translate());
            }

            Rect sb = new Rect(inner.x, cy, inner.width, 26f);
            float chance = CaptivityDirector.EscapeChance(comp, captive);
            bool canSlip = comp.heat < 80;
            if (Widgets.ButtonText(sb, "PSC_Btn_Slip".Translate(Mathf.RoundToInt(chance * 100f)), active: canSlip) && canSlip)
            {
                if (Rand.Value < chance) CaptivityDirector.EscapeToWilderness(comp);
                else CaptivityDirector.CaughtEscaping(comp);
            }
            if (!canSlip) TooltipHandler.TipRegion(sb, "PSC_Tip_Watched".Translate());
        }

        private static void OpenRestraintStruggle(GameComponent_Captivity comp, bool shackled)
        {
            float diff = shackled ? 0.7f : 0.5f;
            Find.WindowStack.Add(new Dialog_Struggle(
                "PSC_Restraint_Title".Translate(), "PSC_Restraint_Desc".Translate(), diff,
                win =>
                {
                    if (win) CaptivityDirector.RemoveOneRestraint(comp);
                    else
                    {
                        comp.heat = CaptivityUtility.Clamp0100(comp.heat + 6);
                        Messages.Message("PSC_Msg_RestraintFail".Translate(), comp.captive, MessageTypeDefOf.NeutralEvent, false);
                    }
                }));
        }

        private float DrawBar(Rect inner, float cy, string label, float pct, Color col)
        {
            pct = Mathf.Clamp01(pct);
            GUI.color = Stat;
            Widgets.Label(new Rect(inner.x, cy, inner.width, 20f), label + "  " + Mathf.RoundToInt(pct * 100f) + "%");
            GUI.color = Color.white;
            cy += 20f;

            Rect bar = new Rect(inner.x, cy, inner.width, 10f);
            Widgets.DrawBoxSolid(bar, Well);
            Widgets.DrawBoxSolid(new Rect(bar.x, bar.y, bar.width * pct, bar.height), col);
            GUI.color = Border;
            Widgets.DrawBox(bar, 1);
            GUI.color = Color.white;
            return cy + 16f;
        }

        private static Color Ramp(int value0to100, bool invert)
        {
            // invert=false: high value is Good (defiance). invert=true: high value is Bad (heat).
            int v = invert ? 100 - value0to100 : value0to100;
            if (v >= 66) return Good;
            if (v >= 33) return Warn;
            return Bad;
        }

        private static string StatusLine(GameComponent_Captivity comp)
        {
            string owner = comp.captorFaction?.Name ?? "?";
            switch (comp.status)
            {
                case CaptivityStatus.BeingSold: return "PSC_Hud_BeingSold".Translate();
                default: return "PSC_Hud_Held".Translate(owner);
            }
        }

        private static string RestraintLine(Pawn captive)
        {
            bool shackled = captive.health?.hediffSet?.HasHediff(PSCDefOf.PSC_Shackled) ?? false;
            bool collared = CaptivityCompat.IsCollared(captive);
            if (shackled && collared) return "PSC_Hud_Restraints_Shackled".Translate() + ", " + "PSC_Hud_Restraints_Collared".Translate();
            if (shackled) return "PSC_Hud_Restraints_Shackled".Translate();
            if (collared) return "PSC_Hud_Restraints_Collared".Translate().ToString().CapitalizeFirst();
            return "PSC_Hud_Restraints_None".Translate();
        }

        private static string EscapeHint(int heat)
        {
            if (heat < 25) return "PSC_Hud_Escape_Low".Translate();
            if (heat < 60) return "PSC_Hud_Escape_Mid".Translate();
            return "PSC_Hud_Escape_High".Translate();
        }
    }
}
