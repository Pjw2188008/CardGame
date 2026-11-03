using System;

namespace Unknown.Battle
{
    /// <summary>Monotonic real-time clock driven by the host. UI frame rate and timeScale do not scale it.</summary>
    public sealed class TurnClock
    {
        double previousTime;
        bool previouslyPaused;
        public double Duration { get; private set; }
        public double Remaining { get; private set; }
        public bool Running { get; private set; }
        public bool Expired => Remaining <= 0;
        public void Begin(double seconds, double now)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds <= 0) throw new ArgumentOutOfRangeException(nameof(seconds));
            Duration = Remaining = seconds; previousTime = now; previouslyPaused = false; Running = true;
        }
        public void Tick(double now, bool paused)
        {
            double elapsed = Math.Max(0, now - previousTime); previousTime = now;
            bool skip = paused || previouslyPaused; previouslyPaused = paused;
            if (!Running || skip) return;
            Remaining = Math.Max(0, Remaining - elapsed);
            if (Expired) Running = false;
        }
        public void Stop() { Running = false; }
    }
}
