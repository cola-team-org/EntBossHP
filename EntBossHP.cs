using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Timers;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using static CounterStrikeSharp.API.Core.Listeners;

namespace EntBossHP
{
    [MinimumApiVersion(369)]
    public partial class EntBossHP : BasePlugin
    {
        [GeneratedRegex(@"_\d{3,}$")]
        private static partial Regex BossNameSuffixRegex();

        public override string ModuleName => "EntBossHP";
        public override string ModuleVersion => "2.1.0";
        public override string ModuleAuthor => "Oylsister, Credits to Kxrnl, DarkerZ [RUS] / modified by Tsukasa";
        
        public string PluginConfigDirectory => Path.Combine(ModuleDirectory, "..", "..", "configs", "plugins", ModuleName);

        private readonly List<BreakableBoss> _breakableBosses = [];
        private readonly List<MathCounterBoss> _mathCounterBosses = [];
        private readonly List<HPBarBoss> _hpBarBosses = [];

        private readonly Dictionary<string, BossData> _activeBosses = [];
        private bool configLoaded = false;

        internal BossConfig BossConfigs { get; private set; } = new();

        private HitEventDisplay HitEventDisplay { get; set; } = null!;
        
        public FakeConVar<bool> CvarEnableBhud { get; } = new("css_bosshp_enablebhud", "Enable boss HP center HUD output", true, ConVarFlags.FCVAR_NONE);
        

        public override void Load(bool hotReload)
        {
            HitEventDisplay = new(this);

            HookEntityOutput("math_counter", "OutValue", CounterOut);
            HookEntityOutput("func_physbox_multiplayer", "OnDamaged", BreakableOut);
            HookEntityOutput("func_physbox", "OnHealthChanged", BreakableOut);
            HookEntityOutput("func_breakable", "OnHealthChanged", BreakableOut);
            HookEntityOutput("prop_dynamic", "OnHealthChanged", Hitbox_Hook);

            RegisterEventHandler<EventRoundStart>(OnRoundStart);
            RegisterListener<OnMapStart>(MapStart);
            RegisterListener<OnEntityCreated>(OnEntityCreated);

            AddCommand("boss_list", "", CommandBossList);

            AddTimer(5.0f, CheckInactiveBosses, TimerFlags.REPEAT);

            if (hotReload)
            {
                MapStart(Server.MapName);
            }
        }
        
        private string SanitizeBossName(string entityName)
        {
            if (string.IsNullOrEmpty(entityName)) return string.Empty;
            return BossNameSuffixRegex().Replace(entityName, "_");
        }

        private void MapStart(string mapname)
        {
            LoadConfigBasedMap(mapname);
            ExecuteConfigFile();
        }

        private void LoadConfigBasedMap(string mapname)
        {
            var configPath = Path.Combine(PluginConfigDirectory, $"{mapname}.jsonc");
            var configDirectory = Path.GetDirectoryName(configPath);
            if (configDirectory != null && !Directory.Exists(configDirectory))
            {
                Directory.CreateDirectory(configDirectory);
            }

            if (!File.Exists(configPath))
            {
                Logger.LogInformation($"Couldn't Find {configPath}, creating new config.");
                BossConfigs = new();
            }
            else
            {
                try
                {
                    BossConfigs = JsonConvert.DeserializeObject<BossConfig>(File.ReadAllText(configPath)) ?? new();
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Failed to load boss config from {ConfigPath}, using default", configPath);
                    BossConfigs = new();
                }
            }
            Logger.LogInformation($"Loaded Boss Config {configPath}");
            configLoaded = true;

            BossDataLoading();
            _activeBosses.Clear();
        }

        private void ExecuteConfigFile()
        {
            var configFolder = Path.Combine(Server.GameDirectory, "csgo/cfg/entbosshp/");
            if (!Directory.Exists(configFolder)) return;
            var configPath = Path.Combine(configFolder, "entbosshp.cfg");
            if (!File.Exists(configPath)) return;

            Server.ExecuteCommand("exec entbosshp/entbosshp.cfg");
        }

        private void BossDataLoading()
        {
            _breakableBosses.Clear();
            _mathCounterBosses.Clear();
            _hpBarBosses.Clear();

            foreach (var breakable in BossConfigs.BreakableList)
            {
                var boss = new BreakableBoss
                {
                    BossName = breakable.Name,
                    Enabled = breakable.Enabled,
                    Type = BossType.Breakable,
                    BreakableEntityName = breakable.Breakable,
                    HpOffset = breakable.HpOffset,
                };
                if (!string.IsNullOrEmpty(breakable.HealthSegmentCounter))
                {
                    boss.IsSegmented = true;
                    boss.HealthSegmentCounterName = breakable.HealthSegmentCounter;
                    boss.HealthSegmentCounterMode = breakable.HealthSegmentCounterMode;
                }
                _breakableBosses.Add(boss);
            }

            foreach (var mathcounter in BossConfigs.MathCounterList)
            {
                var boss = new MathCounterBoss
                {
                    BossName = mathcounter.Name,
                    Enabled = mathcounter.Enabled,
                    Type = BossType.MathCounter,
                    MathCounterHitMode = mathcounter.MathCounterMode,
                    MathCounterName = mathcounter.MathCounter,
                    HpOffset = mathcounter.HpOffset,
                };
                if (!string.IsNullOrEmpty(mathcounter.HealthSegmentCounter))
                {
                    boss.IsSegmented = true;
                    boss.HealthSegmentCounterName = mathcounter.HealthSegmentCounter;
                    boss.HealthSegmentCounterMode = mathcounter.HealthSegmentCounterMode;
                }
                _mathCounterBosses.Add(boss);
            }

            foreach (var hpbar in BossConfigs.HPBarList)
            {
                var boss = new HPBarBoss
                {
                    BossName = hpbar.Name,
                    Enabled = hpbar.Enabled,
                    Type = BossType.HPBar,
                    MathCounterHitMode = hpbar.MathCounterMode,
                    MathCounterName = hpbar.MathCounter,
                    IteratorHitMode = hpbar.IteratorMode,
                    IteratorName = hpbar.Iterator,
                    BackupName = hpbar.Backup,
                    HpOffset = hpbar.HpOffset,
                };
                _hpBarBosses.Add(boss);
            }
        }

        private HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
        {
            if (configLoaded)
            {
                Server.PrintToChatAll($" {ChatColors.Olive}[{ChatColors.Lime}EntBossHP{ChatColors.Olive}] {ChatColors.White}The current map is supported by this plugin.");
                _activeBosses.Clear();
                ResetBossHP();
            }
            return HookResult.Continue;
        }

        private void ResetBossHP()
        {
            foreach (var boss in _breakableBosses)
            {
                boss.Health = 0;
                boss.MaxHealth = 0;
                boss.BreakableEntity = null;
                boss.LastHit = 0f;
            }
            foreach (var boss in _mathCounterBosses)
            {
                boss.Health = 0;
                boss.MaxHealth = 0;
                boss.MathCounterEntity = null;
                boss.LastHit = 0f;
            }
            foreach (var boss in _hpBarBosses)
            {
                boss.Health = 0;
                boss.MaxHealth = 0;
                boss.MathCounterEntity = null;
                boss.BackUpEntity = null;
                boss.BackupValue = 0f;
                boss.IteratorValue = 0f;
                boss.IteratorEntity = null;
                boss.LastHit = 0f;
            }
        }

        private void OnEntityCreated(CEntityInstance entity)
        {
            if (!configLoaded || entity == null || !entity.IsValid || entity.DesignerName != "math_counter") return;
            AddTimer(0.1f, () => {
                if (entity.IsValid) Timer_MathCounterInitial(entity);
            });
        }

        private void CommandBossList(CCSPlayerController? client, CommandInfo info)
        {
            if (client == null || !client.IsValid) return;
            foreach (var boss in BossConfigs.MathCounterList) client.PrintToConsole($"Name: {boss.Name} | Counter: {boss.MathCounter} | Mode: {boss.MathCounterMode}");
            foreach (var boss in _mathCounterBosses) client.PrintToConsole($"Name: {boss.BossName} | Counter: {boss.MathCounterName} | Mode: {boss.MathCounterHitMode}");
        }
        
        private void Timer_MathCounterInitial(CEntityInstance entity)
        {
            var entityName = GetEntityName(entity);
            if (string.IsNullOrWhiteSpace(entityName)) return;
            foreach (var boss in _mathCounterBosses.Where(b => b.IsSegmented && MatchesEntityName(entityName, b.HealthSegmentCounterName))) InitializeSegmentCounter(boss, entity);
            foreach (var boss in _breakableBosses.Where(b => b.IsSegmented && MatchesEntityName(entityName, b.HealthSegmentCounterName))) InitializeSegmentCounter(boss, entity);
            foreach (var boss in _mathCounterBosses.Where(b => MatchesEntityName(entityName, b.MathCounterName))) InitializeMainCounter(boss, entity);
            foreach (var boss in _hpBarBosses) InitializeHpBarCounters(boss, entity);
        }

        private void InitializeSegmentCounter(SegmentedBossData boss, CEntityInstance entity)
        {
            boss.HealthSegmentCounterEntity = entity;
            var segmentCounter = new CMathCounter(entity.Handle);
            boss.TotalHealthSegments = (int)Math.Round(segmentCounter.Max);
            if (boss.HealthSegmentCounterMode == 2)
            {
                var destroyedSegments = (int)GetMathCounterValue(entity.Handle);
                boss.HealthSegments = Math.Max(0, boss.TotalHealthSegments - destroyedSegments);
            }
            else boss.HealthSegments = (int)GetMathCounterValue(entity.Handle);
        }

        private void InitializeMainCounter(MathCounterBoss boss, CEntityInstance entity)
        {
            boss.MathCounterEntity = entity;
            var counter = new CMathCounter(entity.Handle);
            boss.MathCounterStartValue = (int)Math.Round(GetMathCounterValue(entity.Handle));
            boss.MathCounterMaxValue = (int)Math.Round(counter.Max);
            boss.MathCounterMinValue = (int)Math.Round(counter.Min);
            if (boss.MathCounterHitMode == 0) boss.MathCounterHitMode = counter.HitMin ? 2 : 1;
        }
        
        private void InitializeHpBarCounters(HPBarBoss boss, CEntityInstance entity)
        {
            var entityName = GetEntityName(entity);
            if (string.IsNullOrWhiteSpace(entityName)) return;
            var counter = new CMathCounter(entity.Handle);
            if (MatchesEntityName(entityName, boss.MathCounterName)) InitializeMainCounter(boss, entity);
            if (MatchesEntityName(entityName, boss.IteratorName))
            {
                boss.IteratorEntity = entity;
                if (boss.IteratorHitMode == 0) boss.IteratorHitMode = counter.HitMin ? 2 : 1;
                boss.IteratorValue = (boss.IteratorHitMode == 2) ? counter.Max : GetMathCounterValue(entity.Handle);
            }
            if (MatchesEntityName(entityName, boss.BackupName))
            {
                boss.BackUpEntity = entity;
                boss.BackupValue = GetMathCounterValue(entity.Handle);
            }
        }
        
        private HookResult CounterOut(CEntityIOOutput output, string name, CEntityInstance activator, CEntityInstance caller, CVariant value, float delay)
        {
            if (caller == null || activator == null || activator.DesignerName != "player") return HookResult.Continue;
            var client = GetPlayerFromEntity(activator);
            if (client == null) return HookResult.Continue;

            var entityname = GetEntityName(caller);
            if (string.IsNullOrWhiteSpace(entityname)) return HookResult.Continue;
            var counterValue = value.Get<float>();

            if (configLoaded)
            {
                if (!BossConfigs.MathCounterList.Any(b => MatchesEntityName(entityname, b.MathCounter)) && !BossConfigs.HPBarList.Any(b => MatchesEntityName(entityname, b.MathCounter)))
                {
                    var sanitizedName = SanitizeBossName(entityname);
                    if (!BossConfigs.MathCounterList.Any(b => b.MathCounter == sanitizedName))
                    {
                        var newBossConfig = new MathCounterConfig { Name = sanitizedName, MathCounter = sanitizedName, MathCounterMode = 1, Enabled = (counterValue > 10), HpOffset = 0 };
                        BossConfigs.MathCounterList.Add(newBossConfig);
                        SaveChanges();
                        var newLiveBoss = new MathCounterBoss { BossName = newBossConfig.Name, Enabled = newBossConfig.Enabled, Type = BossType.MathCounter, MathCounterHitMode = newBossConfig.MathCounterMode, MathCounterName = newBossConfig.MathCounter, HpOffset = newBossConfig.HpOffset };
                        InitializeMainCounter(newLiveBoss, caller);
                        _mathCounterBosses.Add(newLiveBoss);
                    }
                }
            }

            foreach (var boss in _mathCounterBosses.Where(b => MatchesEntityName(entityname, b.MathCounterName)))
            {
                int currentHp = (boss.MathCounterHitMode == 1) ? (int)counterValue : boss.MathCounterMaxValue - (int)counterValue;
                if (boss.IsSegmented && currentHp <= 0 && boss.HealthSegmentCounterEntity is { IsValid: true })
                {
                    AddTimer(0.1f, () => {
                        if (boss.HealthSegmentCounterEntity is not { IsValid: true }) return;
                        HandleSegmentEnd(boss, client, () => {
                            boss.MaxHealth = 0;
                            boss.Health = 0;
                        });
                    });
                    continue; 
                }
                if (currentHp <= 0)
                {
                    _activeBosses.Remove(boss.BossName);
                    continue;
                }
                boss.Health = currentHp;
                if (boss.MaxHealth < boss.Health) boss.MaxHealth = boss.Health;
                
                if (boss.Enabled)
                {
                    UpdateAndDisplayBoss(boss, client);
                }
            }
            return HookResult.Continue;
        }

        private HookResult BreakableOut(CEntityIOOutput output, string name, CEntityInstance activator, CEntityInstance caller, CVariant value, float delay)
        {
            if (caller == null || activator == null || activator.DesignerName != "player") return HookResult.Continue;
            var client = GetPlayerFromEntity(activator);
            if (client == null) return HookResult.Continue;

            var prop = new CBreakable(caller.Handle);
            if (!prop.IsValid) return HookResult.Continue;
            
            var hp = prop.Health;
            var entityname = GetEntityName(caller);
            if (string.IsNullOrWhiteSpace(entityname)) return HookResult.Continue;

            if (configLoaded)
            {
                 if (!BossConfigs.BreakableList.Any(b => MatchesEntityName(entityname, b.Breakable)))
                 {
                    var sanitizedName = SanitizeBossName(entityname);
                    if (!BossConfigs.BreakableList.Any(b => b.Breakable == sanitizedName))
                    {
                        var newBossConfig = new BreakableConfig { Name = sanitizedName, Breakable = sanitizedName, Enabled = (hp > 10), HpOffset = 0 };
                        BossConfigs.BreakableList.Add(newBossConfig);
                        SaveChanges();
                        var newLiveBoss = new BreakableBoss { BossName = newBossConfig.Name, Enabled = newBossConfig.Enabled, Type = BossType.Breakable, BreakableEntityName = newBossConfig.Breakable, BreakableEntity = caller, HpOffset = newBossConfig.HpOffset };
                        _breakableBosses.Add(newLiveBoss);
                    }
                 }
            }
            
            foreach (var boss in _breakableBosses.Where(b => MatchesEntityName(entityname, b.BreakableEntityName)))
            {
                if (boss.IsSegmented && hp <= 0 && boss.HealthSegmentCounterEntity is { IsValid: true })
                {
                    AddTimer(0.1f, () => {
                        if (boss.HealthSegmentCounterEntity is not { IsValid: true }) return;
                        HandleSegmentEnd(boss, client, () => {
                            boss.MaxHealth = 0;
                            boss.Health = 0;
                        });
                    });
                    continue;
                }
                if (hp <= 0)
                {
                    _activeBosses.Remove(boss.BossName);
                    continue;
                }
                boss.Health = hp;
                if (boss.MaxHealth <= 0) boss.MaxHealth = hp;
                
                if (boss.Enabled)
                {
                    UpdateAndDisplayBoss(boss, client);
                }
            }
            return HookResult.Continue;
        }

        private void HandleSegmentEnd(SegmentedBossData boss, CCSPlayerController client, Action resetAction)
        {
            if (boss.HealthSegmentCounterEntity is not { IsValid: true } segmentCounterEntity) return;

            if (boss.HealthSegmentCounterMode == 2)
            {
                var destroyedSegments = (int)GetMathCounterValue(segmentCounterEntity.Handle);
                boss.HealthSegments = Math.Max(0, boss.TotalHealthSegments - destroyedSegments);
            }
            else boss.HealthSegments = (int)GetMathCounterValue(segmentCounterEntity.Handle);
            if (boss.HealthSegments <= 0)
            {
                _activeBosses.Remove(boss.BossName);
                if (IsBhudEnabled())
                    HitEventDisplay.ShowBossDefeated(client, boss);
                return;
            }
            resetAction.Invoke();
            boss.LastHP = boss.Health;
            
        }

        private HookResult Hitbox_Hook(CEntityIOOutput output, string name, CEntityInstance activator, CEntityInstance caller, CVariant value, float delay)
        {
            return BreakableOut(output, name, activator, caller, value, delay);
        }


        private void CheckInactiveBosses()
        {
            if (_activeBosses.Count == 0) return;

            var currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            List<string> bossesToRemove = [];

            foreach (var boss in _activeBosses.Values)
            {
                if (!boss.Enabled || (boss.LastHit > 0 && currentTime - boss.LastHit > 60.0))
                {
                    bossesToRemove.Add(boss.BossName);
                }
            }

            foreach (var bossName in bossesToRemove)
            {
                _activeBosses.Remove(bossName);
            }
        }

        private void UpdateAndDisplayBoss(BossData boss, CCSPlayerController client)
        {
            boss.LastHP = boss.Health;
            boss.LastHit = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            if (boss.Enabled)
            {
                _activeBosses.TryAdd(boss.BossName, boss);
                
                if (IsBhudEnabled())
                    HitEventDisplay.ShowHitEvent(client, boss, boss.Health);
            }
        }


        private static CCSPlayerController? GetPlayerFromEntity(CEntityInstance? instance)
        {
            if (instance == null || instance.DesignerName != "player") return null;
            var p = instance.As<CCSPlayerPawn>();
            return (p != null && p.IsValid && p.OriginalController.Value != null && p.OriginalController.Value.IsValid) ? p.OriginalController.Value : null;
        }

        private static string GetEntityName(CEntityInstance? entity)
        {
            if (entity == null || !entity.IsValid) return string.Empty;

            try
            {
                return entity.Entity?.Name?.ToString() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static bool MatchesEntityName(string entityName, string configuredName)
        {
            return !string.IsNullOrWhiteSpace(entityName)
                && !string.IsNullOrWhiteSpace(configuredName)
                && entityName.StartsWith(configuredName, StringComparison.Ordinal);
        }

        private unsafe float GetMathCounterValue(nint handle)
        {
            if (handle == IntPtr.Zero) return 0;
            try
            {
                var offset = Schema.GetSchemaOffset("CMathCounter", "m_OutValue");
                return *(float*)IntPtr.Add(handle, offset + 24);
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "Failed to read math_counter value");
                return 0;
            }
        }

        private bool IsBhudEnabled() => CvarEnableBhud.Value;

        private void SaveChanges()
        {
            var configPath = Path.Combine(PluginConfigDirectory, $"{Server.MapName}.jsonc");
            var configDirectory = Path.GetDirectoryName(configPath);
            if (configDirectory != null && !Directory.Exists(configDirectory)) Directory.CreateDirectory(configDirectory);
            var json = JsonConvert.SerializeObject(BossConfigs, Formatting.Indented);
            File.WriteAllText(configPath, json);
            Logger.LogInformation($"Saved updated boss config to {configPath}");
        }
    }
}
