using CounterStrikeSharp.API.Core;

namespace EntBossHP
{
    public enum BossType
    {
        Invalid = -1,
        Breakable = 0,
        MathCounter = 1,
        HPBar = 2,
    }

    public class BossData
    {
        public string BossName { get; set; }
        public int Health { get; set; }
        public int MaxHealth { get; set; }
        public int LastHP { get; set; }
        public double LastHit { get; set; }
        public BossType Type { get; set; }
        public bool Enabled { get; set; }

        private Dictionary<CCSPlayerController, int> _hitCounts = new Dictionary<CCSPlayerController, int>();
        private Dictionary<CCSPlayerController, (double TotalDamage, double LastHitTime)> _damagePerSecond = new Dictionary<CCSPlayerController, (double, double)>();

        public int GetHitCount(CCSPlayerController player)
        {
            return _hitCounts.TryGetValue(player, out var hits) ? hits : 0;
        }

        public void IncrementHitCount(CCSPlayerController player, int damage = 1)
        {
            if (_hitCounts.ContainsKey(player))
                _hitCounts[player]++;
            else
                _hitCounts[player] = 1;

            var currentTime = DateTime.UtcNow.Ticks / TimeSpan.TicksPerSecond;
            if (_damagePerSecond.ContainsKey(player))
            {
                var (totalDamage, firstHitTime) = _damagePerSecond[player];
                var elapsedTime = currentTime - firstHitTime;
                
                if (elapsedTime <= 60)
                {
                    _damagePerSecond[player] = (totalDamage + damage, firstHitTime);
                }
                else
                {
                    _damagePerSecond[player] = (damage, currentTime);
                }
            }
            else
            {
                _damagePerSecond[player] = (damage, currentTime);
            }
        }

        public int GetDamagePerSecond()
        {
            var currentTime = DateTime.UtcNow.Ticks / TimeSpan.TicksPerSecond;
            var totalDPS = 0.0;

            foreach (var kvp in _damagePerSecond)
            {
                var (totalDamage, firstHitTime) = kvp.Value;
                var elapsedTime = currentTime - firstHitTime;
                
                if (totalDamage > 1000)
                {
                    continue;
                }
                
                if (elapsedTime >= 0.5 && elapsedTime <= 60)
                {
                    var playerDPS = totalDamage / elapsedTime;
                    totalDPS += playerDPS;
                }
            }

            if (totalDPS >= 0.1 && totalDPS <= 50000)
            {
                return (int)Math.Round(totalDPS);
            }

            return 0;
        }
        
        public void ResetSegmentStats()
        {
            _hitCounts.Clear();
            _damagePerSecond.Clear();
        }
        
        public void CleanupInvalidPlayers()
        {
            var invalidPlayers = new List<CCSPlayerController>();
            
            foreach (var player in _hitCounts.Keys)
            {
                if (player == null || !player.IsValid || player.Connected == PlayerConnectedState.PlayerDisconnected)
                {
                    invalidPlayers.Add(player);
                }
            }
            
            foreach (var player in invalidPlayers)
            {
                _hitCounts.Remove(player);
                _damagePerSecond.Remove(player);
            }
        }

        public string GetSegmentInfo()
        {
            if (this is MathCounterBoss mathBoss && mathBoss.IsSegmented && mathBoss.HealthSegments >= 0)
            {
                return $"({mathBoss.HealthSegments})";
            }
            if (this is BreakableBoss breakableBoss && breakableBoss.IsSegmented && breakableBoss.HealthSegments >= 0)
            {
                return $"({breakableBoss.HealthSegments})";
            }
            return "";
        }
    }

    public class BreakableBoss : BossData
    {
        public CEntityInstance BreakableEntity { get; set; }
        public string BreakableEntityName { get; set; }
        public bool IsSegmented { get; set; } = false;
        public string HealthSegmentCounterName { get; set; }
        public CEntityInstance HealthSegmentCounterEntity { get; set; }
        public int HealthSegments { get; set; }
        public int TotalHealthSegments { get; set; }
        public int HealthSegmentCounterMode { get; set; } = 1;
    }

    public class MathCounterBoss : BossData
    {
        public CEntityInstance MathCounterEntity { get; set; }
        public int MathCounterHitMode { get; set; } = -1;
        public string MathCounterName { get; set; }
        public int MathCounterStartValue { get; set; }
        public int MathCounterMaxValue { get; set; }
        public int MathCounterMinValue { get; set; }
        public bool IsSegmented { get; set; } = false;
        public string HealthSegmentCounterName { get; set; }
        public CEntityInstance HealthSegmentCounterEntity { get; set; }
        public int HealthSegments { get; set; }
        public int TotalHealthSegments { get; set; }
        public int HealthSegmentCounterMode { get; set; } = 1;
    }

    public class HPBarBoss : MathCounterBoss
    {
        public CEntityInstance IteratorEntity { get; set; }
        public string IteratorName { get; set; }
        public int IteratorHitMode { get; set; } = -1;
        public float IteratorValue { get; set; }
        public CEntityInstance BackUpEntity { get; set; }
        public string BackupName { get; set; }
        public float BackupValue { get; set; }
    }
}