using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace PerspectiveShiftCaptivity
{
    public enum LockpickOutcome { Success, Caught, Abort }

    // Oblivion-style lockpicking. Each pin has a sweeping slider and a sweet spot; set the pin
    // when the slider is over it. If a captor is watching, a suspicion meter climbs (with time
    // and on misses) and fills = caught. Game pauses while picking.
    public class Dialog_Lockpick : Window
    {
        private static readonly Color Dark = new Color(0.06f, 0.06f, 0.07f, 0.95f);
        private static readonly Color Zone = new Color(0.30f, 0.70f, 0.35f, 0.75f);
        private static readonly Color BorderC = new Color(0.30f, 0.30f, 0.33f);
        private static readonly Color Warn = new Color(0.95f, 0.65f, 0.20f);
        private static readonly Color Bad = new Color(0.90f, 0.35f, 0.35f);
        private static readonly Color Good = new Color(0.40f, 0.85f, 0.40f);

        private readonly int pinCount;
        private readonly bool watched;
        private readonly Action<LockpickOutcome> onResolve;

        private int pinsSet;
        private float marker;
        private int dir = 1;
        private float sweepSpeed;
        private float spotCenter;
        private float spotHalf;
        private float suspicion;
        private bool resolved;
        private int lastFrame = -1;
        private string flash;
        private Color flashColor = Color.white;
        private float flashUntil;

        public override Vector2 InitialSize => new Vector2(520f, 300f);

        public Dialog_Lockpick(int pinCount, bool watched, Action<LockpickOutcome> onResolve)
        {
            this.pinCount = Mathf.Max(1, pinCount);
            this.watched = watched;
            this.onResolve = onResolve;
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
            closeOnAccept = false;
            closeOnCancel = true;
            draggable = false;
            NewPin(0);
        }

        private void NewPin(int index)
        {
            sweepSpeed = 0.75f + 0.12f * index;
            spotHalf = Mathf.Max(0.045f, 0.11f - 0.012f * index);
            spotCenter = Rand.Range(spotHalf, 1f - spotHalf);
            marker = Rand.Value;
            dir = Rand.Bool ? 1 : -1;
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (!resolved && Time.frameCount != lastFrame)
            {
                lastFrame = Time.frameCount;
                float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
                marker += dir * sweepSpeed * dt;
                if (marker >= 1f) { marker = 1f; dir = -1; }
                if (marker <= 0f) { marker = 0f; dir = 1; }
                if (watched)
                {
                    suspicion += 0.05f * dt;
                    if (suspicion >= 1f) { Resolve(LockpickOutcome.Caught); return; }
                }
            }

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Space)
            {
                TrySetPin();
                Event.current.Use();
            }

            float y = inRect.y;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, y, inRect.width, 34f), "PSC_Lock_Title".Translate());
            Text.Font = GameFont.Small;
            y += 38f;
            Widgets.Label(new Rect(inRect.x, y, inRect.width, 44f), (watched ? "PSC_Lock_DescWatched" : "PSC_Lock_Desc").Translate());
            y += 46f;
            Widgets.Label(new Rect(inRect.x, y, inRect.width, 24f), "PSC_Lock_Pins".Translate(pinsSet, pinCount));
            y += 26f;

            Rect bar = new Rect(inRect.x, y, inRect.width, 42f);
            Widgets.DrawBoxSolid(bar, Dark);
            Widgets.DrawBoxSolid(new Rect(bar.x + (spotCenter - spotHalf) * bar.width, bar.y + 3f, 2f * spotHalf * bar.width, bar.height - 6f), Zone);
            float mx = bar.x + marker * bar.width;
            Widgets.DrawBoxSolid(new Rect(mx - 2f, bar.y - 3f, 4f, bar.height + 6f), Color.white);
            GUI.color = BorderC;
            Widgets.DrawBox(bar, 1);
            GUI.color = Color.white;
            y += 50f;

            if (watched)
            {
                Widgets.Label(new Rect(inRect.x, y, 100f, 18f), "PSC_Lock_Suspicion".Translate());
                Rect sr = new Rect(inRect.x + 104f, y + 2f, inRect.width - 104f, 14f);
                Widgets.DrawBoxSolid(sr, Dark);
                Widgets.DrawBoxSolid(new Rect(sr.x, sr.y, sr.width * Mathf.Clamp01(suspicion), sr.height), Color.Lerp(Warn, Bad, suspicion));
                y += 22f;
            }

            if (Time.unscaledTime < flashUntil && flash != null)
            {
                GUI.color = flashColor;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(new Rect(inRect.x, y, inRect.width, 20f), flash);
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }

            Rect setBtn = new Rect(inRect.x, inRect.yMax - 40f, inRect.width * 0.66f - 4f, 36f);
            if (Widgets.ButtonText(setBtn, "PSC_Lock_Button".Translate()))
                TrySetPin();
            Rect stopBtn = new Rect(setBtn.xMax + 8f, inRect.yMax - 40f, inRect.width - setBtn.width - 8f, 36f);
            if (Widgets.ButtonText(stopBtn, "PSC_Lock_Stop".Translate()))
                Resolve(LockpickOutcome.Abort);
        }

        private void TrySetPin()
        {
            if (resolved) return;
            if (marker >= spotCenter - spotHalf && marker <= spotCenter + spotHalf)
            {
                pinsSet++;
                Flash("PSC_Lock_Set".Translate(), Good);
                if (pinsSet >= pinCount) { Resolve(LockpickOutcome.Success); return; }
                NewPin(pinsSet);
            }
            else
            {
                Flash("PSC_Lock_Miss".Translate(), Bad);
                if (watched)
                {
                    suspicion = Mathf.Min(1f, suspicion + 0.16f);
                    if (suspicion >= 1f) { Resolve(LockpickOutcome.Caught); return; }
                }
                spotCenter = Rand.Range(spotHalf, 1f - spotHalf);
            }
        }

        private void Flash(string t, Color c)
        {
            flash = t;
            flashColor = c;
            flashUntil = Time.unscaledTime + 0.5f;
        }

        private void Resolve(LockpickOutcome o)
        {
            if (resolved) return;
            resolved = true;
            try { onResolve?.Invoke(o); }
            catch (Exception e) { CaptivityLog.ErrorOnce("lockpick resolve failed: " + e, 74310601); }
            Close(false);
        }

        public override void PostClose()
        {
            base.PostClose();
            if (!resolved) { resolved = true; try { onResolve?.Invoke(LockpickOutcome.Abort); } catch { } }
        }
    }
}
