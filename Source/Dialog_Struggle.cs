using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace PerspectiveShiftCaptivity
{
    // Real-time struggle minigame shown when captors seize the captive. Mash Space (or click
    // Struggle) to fill the bar before the timer runs out. Winning breaks you free (which
    // escalates); losing lets the captors do what they seized you for. Pauses the game.
    public class Dialog_Struggle : Window
    {
        private readonly string title;
        private readonly string desc;
        private readonly float clickPower;
        private readonly float decayPerSec;
        private readonly Action<bool> onResolve;

        private float progress;
        private float timeLeft;
        private bool resolved;
        private int lastFrame = -1;

        public override Vector2 InitialSize => new Vector2(470f, 250f);

        public Dialog_Struggle(string title, string desc, float difficulty, Action<bool> onResolve)
        {
            this.title = title;
            this.desc = desc;
            this.onResolve = onResolve;
            difficulty = Mathf.Clamp(difficulty, 0.15f, 0.95f);
            clickPower = Mathf.Lerp(0.14f, 0.05f, difficulty);
            decayPerSec = Mathf.Lerp(0.06f, 0.38f, difficulty);
            timeLeft = 8f;
            progress = 0f;

            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
            closeOnAccept = false;
            closeOnCancel = true;
            draggable = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            // Advance timer / grip decay once per real frame (OnGUI fires several passes/frame).
            if (!resolved && Time.frameCount != lastFrame)
            {
                lastFrame = Time.frameCount;
                float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
                timeLeft -= dt;
                progress = Mathf.Max(0f, progress - decayPerSec * dt);
                if (progress >= 1f) { Resolve(true); return; }
                if (timeLeft <= 0f) { Resolve(false); return; }
            }

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Space)
            {
                Struggle();
                Event.current.Use();
            }

            float y = inRect.y;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, y, inRect.width, 34f), title);
            Text.Font = GameFont.Small;
            y += 38f;
            Widgets.Label(new Rect(inRect.x, y, inRect.width, 46f), desc);
            y += 52f;

            Rect bar = new Rect(inRect.x, y, inRect.width, 26f);
            Widgets.FillableBar(bar, Mathf.Clamp01(progress));
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(bar, Mathf.RoundToInt(progress * 100f) + "%");
            Text.Anchor = TextAnchor.UpperLeft;
            y += 30f;

            Widgets.Label(new Rect(inRect.x, y, inRect.width, 22f), "PSC_Struggle_Time".Translate(timeLeft.ToString("0.0")));

            Rect btn = new Rect(inRect.x, inRect.yMax - 66f, inRect.width, 38f);
            if (Widgets.ButtonText(btn, "PSC_Struggle_Button".Translate()))
                Struggle();

            Rect give = new Rect(inRect.x, btn.yMax + 4f, inRect.width, 24f);
            if (Widgets.ButtonText(give, "PSC_Struggle_GiveIn".Translate()))
                Resolve(false);
        }

        private void Struggle()
        {
            if (resolved) return;
            progress = Mathf.Min(1f, progress + clickPower);
            if (progress >= 1f) Resolve(true);
        }

        private void Resolve(bool brokeFree)
        {
            if (resolved) return;
            resolved = true;
            try { onResolve?.Invoke(brokeFree); }
            catch (Exception e) { CaptivityLog.ErrorOnce("struggle resolve failed: " + e, 74310301); }
            Close(false);
        }

        public override void PostClose()
        {
            base.PostClose();
            if (!resolved) { resolved = true; try { onResolve?.Invoke(false); } catch { } }
        }
    }
}
