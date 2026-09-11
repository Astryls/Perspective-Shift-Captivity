using System.Collections.Generic;
using RimWorld;
using Verse;

namespace PerspectiveShiftCaptivity
{
    public enum CaptivityStatus : byte { Inactive, Held, BeingSold, InTransit, Escaped, Free }

    // Central captivity state for the run. Auto-instantiated by the engine (GameComponents
    // with a (Game) ctor are discovered automatically; no XML needed).
    public class GameComponent_Captivity : GameComponent
    {
        public bool captivityRun;
        public CaptivityStatus status = CaptivityStatus.Inactive;
        public Faction captorFaction;
        public Pawn captive;

        // 0..100. Obedience high = compliant/low heat; defiance = 100 - obedience.
        public int obedience = 40;
        // 0..100. How closely the captive is watched; gates escape opportunities.
        public int heat = 80;
        public bool permadeath;

        // Scribed per-pillar cadence timers (elapsed-time scheduling).
        public int lastDisciplineAt = -1;
        public int lastHarassAt = -1;
        public int lastSellAt = -1;
        public int lastHarvestAt = -1;
        public int lastHeatDecayAt = -1;
        public int lastFeedAt = -1;
        public int resistStreak;
        public bool directorBroken;
        public HashSet<int> pickedDoorIds = new HashSet<int>();

        public void MarkDoorPicked(int id) => pickedDoorIds.Add(id);
        public bool IsDoorPicked(int id) => pickedDoorIds != null && pickedDoorIds.Contains(id);

        public GameComponent_Captivity(Game game) { }

        public static GameComponent_Captivity Comp => Current.Game?.GetComponent<GameComponent_Captivity>();

        public bool Active => captivityRun
            && status != CaptivityStatus.Inactive
            && status != CaptivityStatus.Escaped
            && status != CaptivityStatus.Free;

        public bool IsCaptive(Pawn p) => p != null && p == captive && Active;

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            // Start orchestration is triggered from ScenPart_Captivity.PostGameStart.
        }

        public override void GameComponentTick()
        {
            if (!captivityRun || directorBroken) return;
            try
            {
                CaptivityDirector.Tick(this);
            }
            catch (System.Exception e)
            {
                directorBroken = true;
                CaptivityLog.ErrorOnce("captor director disabled after exception: " + e, 74310021);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref captivityRun, "captivityRun", false);
            Scribe_Values.Look(ref status, "status", CaptivityStatus.Inactive);
            Scribe_Values.Look(ref obedience, "obedience", 40);
            Scribe_Values.Look(ref heat, "heat", 80);
            Scribe_Values.Look(ref permadeath, "permadeath", false);
            Scribe_Values.Look(ref lastDisciplineAt, "lastDisciplineAt", -1);
            Scribe_Values.Look(ref lastHarassAt, "lastHarassAt", -1);
            Scribe_Values.Look(ref lastSellAt, "lastSellAt", -1);
            Scribe_Values.Look(ref lastHarvestAt, "lastHarvestAt", -1);
            Scribe_Values.Look(ref lastHeatDecayAt, "lastHeatDecayAt", -1);
            Scribe_Values.Look(ref lastFeedAt, "lastFeedAt", -1);
            Scribe_Values.Look(ref resistStreak, "resistStreak", 0);
            Scribe_Values.Look(ref directorBroken, "directorBroken", false);
            Scribe_Collections.Look(ref pickedDoorIds, "pickedDoorIds", LookMode.Value);
            if (pickedDoorIds == null) pickedDoorIds = new HashSet<int>();
            Scribe_References.Look(ref captorFaction, "captorFaction");
            Scribe_References.Look(ref captive, "captive");
        }
    }
}
