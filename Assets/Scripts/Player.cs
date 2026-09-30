using UnityEngine;

namespace RollingSteel
{
    /// Everything that belongs to one racer: their marble, their camera, their
    /// clock, and their own private disasters.
    ///
    /// Single player is simply a list of one of these, so the two-player mode is
    /// not a special case bolted onto the side - it is the same code with a
    /// second entry.
    public class Player
    {
        public int Index;
        public MarbleController Marble;
        public Camera Cam;
        public IsoCamera Rig;
        public AudioSource Roll;

        public float TimeLeft;
        public float CourseTime;
        public float RunTime;
        public int Deaths;
        public int Wins;                 // courses taken, in a two-player match

        public bool Finished;
        public float FinishTime;
        public bool OutOfTime;

        public bool Dying;
        public float DyingTimer;
        public string DeathReason = "";

        public float Flash;
        public Color FlashColor = Color.white;
        public bool FallWhistle;
        public float LastWarnBeep;

        public int DemoWp;

        /// Racing right now: not dead, not finished, not out of clock.
        public bool Racing => !Dying && !Finished && !OutOfTime;

        public Color Tint => Index == 0
            ? new Color(0.55f, 0.85f, 1f)
            : new Color(1f, 0.62f, 0.35f);

        public string Label => Index == 0 ? "P1" : "P2";

        public void ResetForCourse(float timeBonus, bool resetClock)
        {
            CourseTime = 0f;
            Finished = false;
            FinishTime = 0f;
            OutOfTime = false;
            Dying = false;
            DyingTimer = 0f;
            FallWhistle = false;
            DemoWp = 0;
            if (resetClock) TimeLeft = 0f;
            TimeLeft += timeBonus;
        }
    }
}
