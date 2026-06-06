using Newtonsoft.Json;

namespace EntBossHP
{
    public class BossConfig
    {
        [JsonProperty(PropertyName = "Breakable")]
        public List<BreakableConfig> BreakableList { get; set; } = [];

        [JsonProperty(PropertyName = "MathCounter")]
        public List<MathCounterConfig> MathCounterList { get; set; } = [];

        [JsonProperty(PropertyName = "HPBar")]
        public List<HPBarConfig> HPBarList { get; set; } = [];
    }

    public class BreakableConfig
    {
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty(PropertyName = "enabled")]
        public bool Enabled { get; set; } = true;

        [JsonProperty(PropertyName = "breakable")]
        public string Breakable { get; set; } = string.Empty;

        [JsonProperty(PropertyName = "health_segment_counter")]
        public string HealthSegmentCounter { get; set; } = string.Empty;

        [JsonProperty(PropertyName = "health_segment_counter_mode")]
        public int HealthSegmentCounterMode { get; set; } = 1;

        [JsonProperty(PropertyName = "hp_offset")]
        public int HpOffset { get; set; } = 0;
    }

    public class MathCounterConfig
    {
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty(PropertyName = "enabled")]
        public bool Enabled { get; set; } = true;

        [JsonProperty(PropertyName = "mathcounter")]
        public string MathCounter { get; set; } = string.Empty;

        [JsonProperty(PropertyName = "mathcounter_mode")]
        public int MathCounterMode { get; set; } = 1;

        [JsonProperty(PropertyName = "health_segment_counter")]
        public string HealthSegmentCounter { get; set; } = string.Empty;

        [JsonProperty(PropertyName = "health_segment_counter_mode")]
        public int HealthSegmentCounterMode { get; set; } = 1;

        [JsonProperty(PropertyName = "hp_offset")]
        public int HpOffset { get; set; } = 0;
    }

    public class HPBarConfig
    {
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty(PropertyName = "enabled")]
        public bool Enabled { get; set; } = true;

        [JsonProperty(PropertyName = "mathcounter")]
        public string MathCounter { get; set; } = string.Empty;

        [JsonProperty(PropertyName = "mathcounter_mode")]
        public int MathCounterMode { get; set; } = 1;

        [JsonProperty(PropertyName = "iterator")]
        public string Iterator { get; set; } = string.Empty;

        [JsonProperty(PropertyName = "iterator_mode")]
        public int IteratorMode { get; set; } = 1;

        [JsonProperty(PropertyName = "backup")]
        public string Backup { get; set; } = string.Empty;

        [JsonProperty(PropertyName = "hp_offset")]
        public int HpOffset { get; set; } = 0;
    }
}
