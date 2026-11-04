using System;
namespace Unknown.Battle
{
    // One authority-owned draw per match. Clearing a match preserves the random stream.
    public sealed class RandomMapSelection
    {
        readonly IRandomSource random;
        public BattleMap? Current { get; private set; }
        public int DrawCount { get; private set; }
        public RandomMapSelection(IRandomSource random) { this.random=random ?? throw new ArgumentNullException(nameof(random)); }
        public BattleMap BeginMatch()
        {
            if(!Current.HasValue) { Current=random.Next(2)==0 ? BattleMap.Normal : BattleMap.JokerGame; DrawCount++; }
            return Current.Value;
        }
        public void ResetMatch() { Current=null; }
    }
}
