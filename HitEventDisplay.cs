using CounterStrikeSharp.API.Core;
using System.Text;
using System.Text.RegularExpressions;

namespace EntBossHP
{
    public class HitEventDisplay
    {
        private readonly EntBossHP _plugin;

        public HitEventDisplay(EntBossHP plugin)
        {
            _plugin = plugin;
        }

        public bool IsBossActive(BossData boss)
        {
            if (boss.Enabled)
            {
                return true;
            }
            return false;
        }

        public string SanitizeBossName(string entityName)
        {
            return Regex.Replace(entityName, @"_\d{3,}$", "_");
        }

        public string GetBossDisplayName(string entityName)
        {
            if (_plugin.BossConfigs == null) return SanitizeBossName(entityName);

            var sanitizedName = SanitizeBossName(entityName);

            if (_plugin.BossConfigs.BreakableList != null)
            {
                foreach (var breakable in _plugin.BossConfigs.BreakableList)
                {
                    if (breakable != null && !string.IsNullOrEmpty(breakable.Breakable) && breakable.Breakable.StartsWith(sanitizedName))
                        return breakable.Name ?? sanitizedName;
                }
            }

            if (_plugin.BossConfigs.MathCounterList != null)
            {
                foreach (var mathCounter in _plugin.BossConfigs.MathCounterList)
                {
                    if (mathCounter != null && !string.IsNullOrEmpty(mathCounter.MathCounter) && mathCounter.MathCounter.StartsWith(sanitizedName))
                        return mathCounter.Name ?? sanitizedName;
                }
            }

            if (_plugin.BossConfigs.HPBarList != null)
            {
                foreach (var hpBar in _plugin.BossConfigs.HPBarList)
                {
                    if (hpBar != null && (
                        (!string.IsNullOrEmpty(hpBar.MathCounter) && hpBar.MathCounter.StartsWith(sanitizedName)) ||
                        (!string.IsNullOrEmpty(hpBar.Iterator) && hpBar.Iterator.StartsWith(sanitizedName)) ||
                        (!string.IsNullOrEmpty(hpBar.Backup) && hpBar.Backup.StartsWith(sanitizedName))))
                        return hpBar.Name ?? sanitizedName;
                }
            }

            return sanitizedName;
        }

        public void ShowHitEvent(CCSPlayerController controller, BossData boss, int health, int damage = 1)
        {
            if (controller == null || !controller.IsValid || boss == null) return;

            var playerHitCount = boss.GetHitCount(controller);

            if (!boss.Enabled && playerHitCount == 0) return;

            var eta = 0.0;
            var hit = playerHitCount;
            var dps = boss.GetDamagePerSecond();


            if (dps > 0)
            {
                eta = (health / dps) + 1;
            }

            var maxHealth = boss.MaxHealth > 0 ? boss.MaxHealth : health;
            if (maxHealth <= 0) maxHealth = 1;

            var p = Math.Clamp((int) Math.Ceiling(((double)health / maxHealth) * 100), 0, 100);
            var r = Math.Clamp(p <= 50 ? 255 - (100 - p) * 5 : 0, 0, 255);
            var g = Math.Clamp(p >= 50 ? 255 - (p - 50) * 5 : p * 5, 0, 255);
            var color = $"#{r:X2}{g:X2}00";

            const int maxSplits = 20;
            var healthBarCount = Math.Min(p / (100 / maxSplits) + 1, maxSplits);

            // 사진과 동일하게 ▰ 문자만 사용 (사용자 지적사항 반영)
            var filledChar = "▰";  // 검은색 평행사변형 (Black Parallelogram)
            var emptyChar = "▰";   // 같은 문자 사용 (색상으로만 구분)

            var healthBarText = healthBarCount switch
            {
                > 0 => $"<font color='#00FF00'>{new string(filledChar[0], healthBarCount)}</font><font color='#363636'>{new string(emptyChar[0], maxSplits - healthBarCount)}</font>",
                _ => $"<font color='#363636'>{new string(emptyChar[0], maxSplits)}</font>"
            };

            var builder = new StringBuilder();

            var displayName = GetBossDisplayName(boss.BossName);
            var segmentInfo = boss.GetSegmentInfo();

            builder.Append($"<font class='fontSize-lg' color='#FFFFFF'>{displayName}</font> : ");
            builder.Append($"<font color='#31700'>{health}</font>");

            if (!string.IsNullOrEmpty(segmentInfo))
            {
                var segmentMatch = System.Text.RegularExpressions.Regex.Match(segmentInfo, @"\((-?\d+)\)");
                if (segmentMatch.Success && int.TryParse(segmentMatch.Groups[1].Value, out var segmentCount))
                {
                    if (segmentCount >= 1)
                    {
                        builder.Append($" <font color='#FFFF00'>{segmentInfo}</font>");
                    }
                }
            }

            builder.Append("<br>");
            builder.Append(healthBarText);

            var stats = new List<string>();

            stats.Add($"Hits: <font color='#5C5C'>{hit}</font>");

            if (eta > 0 && eta < 3600)
                stats.Add($"ETA: <font color='#7F7F'>{Math.Round(eta)}</font>");
            else
                stats.Add($"ETA: <font color='#7F7F'>-</font>");

            if (dps > 0)
                stats.Add($"DPS: <font color='#5C5C'>{dps}</font>");
            else
                stats.Add($"DPS: <font color='#5C5C'>-</font>");

            builder.Append($"<br>{string.Join(" | ", stats)}");

            // HTML로 중앙에 표시 (CounterStrikeSharp의 PrintToCenterHtml 사용)
            controller.PrintToCenterHtml(builder.ToString(), 3);
        }
    }
}
