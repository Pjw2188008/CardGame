using System;
using System.Linq;
namespace Unknown.Battle
{
    // One authority-owned draw per match. Clearing a match preserves the random stream.
    public sealed class RandomMapSelection
    {
        readonly IRandomSource random;
        readonly MapDefinition[] pool;
        public BattleMap? Current { get; private set; }
        public int DrawCount { get; private set; }
        public RandomMapSelection(IRandomSource random,MapDefinition[] definitions=null)
        {
            this.random=random ?? throw new ArgumentNullException(nameof(random));
            pool=(definitions ?? MapDefinition.Defaults()).Where(d=>d.map!=BattleMap.Normal && d.weight>0).Select(d=>d.Copy()).ToArray();
            if(pool.Length!=5 || pool.Select(d=>d.map).Distinct().Count()!=5 || pool.Sum(d=>d.weight)!=10000)throw new ArgumentException("Five map weights must sum to 10000.");
        }
        public BattleMap BeginMatch()
        {
            if(!Current.HasValue) {
                int roll=random.Next(10000);
                foreach(var d in pool) { roll-=d.weight; if(roll<0) { Current=d.map; break; } }
                DrawCount++;
            }
            return Current.Value;
        }
        public void ResetMatch() { Current=null; }
    }
}
