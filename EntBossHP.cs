using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Timers;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using static CounterStrikeSharp.API.Core.Listeners;

namespace EntBossHP
{
    public class EntBossHP : BasePlugin
    {
        public override string ModuleName => "EntBossHP";
        public override string ModuleVersion => "1.0.1";
        public override string ModuleAuthor => "Oylsister, Credits to Kxrnl, DarkerZ [RUS] / modified by Tsukasa";
        
        public string PluginConfigDirectory => Path.Combine(ModuleDirectory, "..", "..", "configs", "plugins", ModuleName);

        public Dictionary<CCSPlayerController, ClientDisplayData> ClientDisplayDatas { get; set; } = new Dictionary<CCSPlayerController, ClientDisplayData>();
        public Dictionary<CEntityInstance, EntityData> EntityDatas { get; set; } = new Dictionary<CEntityInstance, EntityData>();

        public List<BreakableBoss> breakableBosses = new List<BreakableBoss>();
        public List<MathCounterBoss> mathCounterBosses = new List<MathCounterBoss>();
        public List<HPBarBoss> hpBarBosses = new List<HPBarBoss>();

        public Dictionary<string, BossData> activeBosses;
        bool configLoaded = false;

        public BossConfig BossConfigs;
        
        public FakeConVar<bool> cvarEnableBhud = new FakeConVar<bool>("css_bosshp_enablebhud", "Enable bhud to print all entity that get damaged", true, ConVarFlags.FCVAR_NONE);
        public FakeConVar<bool> cvarMultiBossHP = new FakeConVar<bool>("css_bosshp_multihp", "Showing multi boss hp in single Center text", false, ConVarFlags.FCVAR_NONE);

        public override void Load(bool hotReload)
        {
            HookEntityOutput("math_counter", "OutValue", CounterOut);
            HookEntityOutput("func_physbox_multiplayer", "OnDamaged", BreakableOut);
            HookEntityOutput("func_physbox", "OnHealthChanged", BreakableOut);
            HookEntityOutput("func_breakable", "OnHealthChanged", BreakableOut);
            HookEntityOutput("prop_dynamic", "OnHealthChanged", Hitbox_Hook);

            RegisterEventHandler<EventRoundStart>(OnRoundStart);
            RegisterEventHandler<EventPlayerConnectFull>(OnPlayerConnectFull);
            RegisterListener<OnClientDisconnect>(OnClientDisconnect);
            RegisterListener<OnMapStart>(MapStart);
            RegisterListener<OnEntityCreated>(OnEntityCreated);

            AddCommand("boss_list", "", CommandBossList);

            AddTimer(1.0f, CheckInactiveBosses, TimerFlags.REPEAT);

            if (hotReload)
            {
                foreach(var player in Utilities.GetPlayers())
                    ClientDisplayDatas.Add(player, new());

                MapStart(Server.MapName);
            }
        }
        
        private string SanitizeBossName(string entityName)
        {
            return Regex.Replace(entityName, @"_\d{3,}$", "_");
        }

        public void MapStart(string mapname)
        {
            LoadConfigBasedMap(mapname);
            ExecuteConfigFile();
        }

        public void LoadConfigBasedMap(string mapname)
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
                BossConfigs = new BossConfig();
            }
            else
            {
                BossConfigs = JsonConvert.DeserializeObject<BossConfig>(File.ReadAllText(configPath));
            }
            Logger.LogInformation($"Loaded Boss Config {configPath}");
            configLoaded = true;

            BossDataLoading();
            activeBosses = new();
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
            breakableBosses.Clear();
            mathCounterBosses.Clear();
            hpBarBosses.Clear();

            foreach(var breakable in BossConfigs.BreakableList)
            {
                var boss = new BreakableBoss();
                boss.BossName = breakable.Name;
                boss.Enabled = breakable.Enabled;
                boss.Type = BossType.Breakable;
                boss.BreakableEntityName = breakable.Breakable;
                if (!string.IsNullOrEmpty(breakable.HealthSegmentCounter))
                {
                    boss.IsSegmented = true;
                    boss.HealthSegmentCounterName = breakable.HealthSegmentCounter;
                    boss.HealthSegmentCounterMode = breakable.HealthSegmentCounterMode;
                }
                breakableBosses.Add(boss);
            }

            foreach (var mathcounter in BossConfigs.MathCounterList)
            {
                var boss = new MathCounterBoss();
                boss.BossName = mathcounter.Name;
                boss.Enabled = mathcounter.Enabled;
                boss.Type = BossType.MathCounter;
                boss.MathCounterHitMode = mathcounter.MathCounterMode;
                boss.MathCounterName = mathcounter.MathCounter;
                if (!string.IsNullOrEmpty(mathcounter.HealthSegmentCounter))
                {
                    boss.IsSegmented = true;
                    boss.HealthSegmentCounterName = mathcounter.HealthSegmentCounter;
                    boss.HealthSegmentCounterMode = mathcounter.HealthSegmentCounterMode;
                }
                mathCounterBosses.Add(boss);
            }

            foreach (var hpbar in BossConfigs.HPBarList)
            {
                var boss = new HPBarBoss();
                boss.BossName = hpbar.Name;
                boss.Enabled = hpbar.Enabled;
                boss.Type = BossType.HPBar;
                boss.MathCounterHitMode = hpbar.MathCounterMode;
                boss.MathCounterName = hpbar.MathCounter;
                boss.IteratorHitMode = hpbar.IteratorMode;
                boss.IteratorName = hpbar.Iterator;
                boss.BackupName = hpbar.Backup;
                hpBarBosses.Add(boss);
            }
        }

        public HookResult OnPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
        {
            if (@event.Userid.IsBot || !@event.Userid.IsValid) return HookResult.Continue;
            var player = @event.Userid;
            if (player != null && player.IsValid) ClientDisplayDatas[player] = new ClientDisplayData();
            return HookResult.Continue;
        }

        public void OnClientDisconnect(int playerSlot)
        {
            var player = Utilities.GetPlayerFromSlot(playerSlot);
            if (player != null && ClientDisplayDatas.ContainsKey(player)) ClientDisplayDatas.Remove(player);
        }

        public HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
        {
            EntityDatas.Clear();
            if (configLoaded)
            {
                Server.PrintToChatAll($" {ChatColors.Olive}[{ChatColors.Lime}EntBossHP{ChatColors.Olive}] {ChatColors.White}The current map is supported by this plugin.");
                activeBosses?.Clear();
                ResetBossHP();
            }
            return HookResult.Continue;
        }

        private void ResetBossHP()
        {
            foreach(var boss in breakableBosses.Where(b => b != null))
            {
                boss.Health = 0;
                boss.MaxHealth = 0;
                boss.BreakableEntity = null;
                boss.LastHit = 0f;
            }
            foreach(var boss in mathCounterBosses.Where(b => b != null))
            {
                boss.Health = 0;
                boss.MaxHealth = 0;
                boss.MathCounterEntity = null;
                boss.LastHit = 0f;
            }
            foreach (var boss in hpBarBosses.Where(b => b != null))
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

        public void OnEntityCreated(CEntityInstance entity)
        {
            if (!configLoaded || entity.DesignerName != "math_counter") return;
            AddTimer(0.1f, () => {
                if(entity.IsValid) Timer_MathCounterInitial(entity);
            });
        }

        private void CommandBossList(CCSPlayerController client, CommandInfo info)
        {
            if (client == null) return;
            foreach(var boss in BossConfigs.MathCounterList) client.PrintToConsole($"Name: {boss.Name} | Counter: {boss.MathCounter} | Mode: {boss.MathCounterMode}");
            foreach(var boss in mathCounterBosses) client.PrintToConsole($"Name: {boss.BossName} | Counter: {boss.MathCounterName} | Mode: {boss.MathCounterHitMode}");
        }
        
        public void Timer_MathCounterInitial(CEntityInstance entity)
        {
            if (entity == null || !entity.IsValid || string.IsNullOrWhiteSpace(entity.Entity.Name)) return;
            var entityName = entity.Entity.Name.ToString();
            foreach (var boss in mathCounterBosses.Where(b => b.IsSegmented && entityName.StartsWith(b.HealthSegmentCounterName))) InitializeSegmentCounter(boss, entity);
            foreach (var boss in breakableBosses.Where(b => b.IsSegmented && entityName.StartsWith(b.HealthSegmentCounterName))) InitializeSegmentCounter(boss, entity);
            foreach (var boss in mathCounterBosses.Where(b => entityName.StartsWith(b.MathCounterName))) InitializeMainCounter(boss, entity);
            foreach (var boss in hpBarBosses) InitializeHpBarCounters(boss, entity);
        }

        private void InitializeSegmentCounter(dynamic boss, CEntityInstance entity)
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
            var entityName = entity.Entity.Name.ToString();
            var counter = new CMathCounter(entity.Handle);
            if (entityName.StartsWith(boss.MathCounterName)) InitializeMainCounter(boss, entity);
            if (entityName.StartsWith(boss.IteratorName))
            {
                boss.IteratorEntity = entity;
                if (boss.IteratorHitMode == 0) boss.IteratorHitMode = counter.HitMin ? 2 : 1;
                boss.IteratorValue = (boss.IteratorHitMode == 2) ? counter.Max : GetMathCounterValue(entity.Handle);
            }
            if (entityName.StartsWith(boss.BackupName))
            {
                boss.BackUpEntity = entity;
                boss.BackupValue = GetMathCounterValue(entity.Handle);
            }
        }
        
        public HookResult CounterOut(CEntityIOOutput output, string name, CEntityInstance activator, CEntityInstance caller, CVariant value, float delay)
        {
            if (caller == null || activator == null || activator.DesignerName != "player") return HookResult.Continue;
            var client = player(activator);
            if (client == null) return HookResult.Continue;

            var entityname = caller.Entity.Name.ToString();
            var counterValue = value.Get<float>();

            if (configLoaded && !string.IsNullOrWhiteSpace(entityname))
            {
                if (!BossConfigs.MathCounterList.Any(b => entityname.StartsWith(b.MathCounter)) && !BossConfigs.HPBarList.Any(b => entityname.StartsWith(b.MathCounter)))
                {
                    var sanitizedName = SanitizeBossName(entityname);
                    if (!BossConfigs.MathCounterList.Any(b => b.MathCounter == sanitizedName))
                    {
                        var newBossConfig = new MathCounterConfig { Name = sanitizedName, MathCounter = sanitizedName, MathCounterMode = 1, Enabled = (counterValue > 10) };
                        BossConfigs.MathCounterList.Add(newBossConfig);
                        SaveChanges();
                        var newLiveBoss = new MathCounterBoss { BossName = newBossConfig.Name, Enabled = newBossConfig.Enabled, Type = BossType.MathCounter, MathCounterHitMode = newBossConfig.MathCounterMode, MathCounterName = newBossConfig.MathCounter };
                        InitializeMainCounter(newLiveBoss, caller);
                        mathCounterBosses.Add(newLiveBoss);
                    }
                }
            }

            foreach (var boss in mathCounterBosses.Where(b => entityname.StartsWith(b.MathCounterName)))
            {
                int currentHp = (boss.MathCounterHitMode == 1) ? (int)counterValue : boss.MathCounterMaxValue - (int)counterValue;
                if (boss.IsSegmented && currentHp <= 0 && boss.HealthSegmentCounterEntity != null && boss.HealthSegmentCounterEntity.IsValid)
                {
                    AddTimer(0.1f, () => {
                        if (boss == null || !boss.HealthSegmentCounterEntity.IsValid) return;
                        HandleSegmentEnd(boss, () => {
                            if (boss.MathCounterEntity == null || !boss.MathCounterEntity.IsValid) return;
                            var resetValue = (boss.MathCounterHitMode == 2) ? boss.MathCounterMinValue : boss.MathCounterMaxValue;
                            Server.ExecuteCommand($"ent_fire {entityname} SetValue {resetValue}");
                            boss.Health = boss.MathCounterMaxValue;
                        });
                    });
                    continue; 
                }
                if (currentHp <= 0)
                {
                    activeBosses.Remove(boss.BossName);
                    continue;
                }
                boss.Health = currentHp;
                if (boss.MaxHealth < boss.Health) boss.MaxHealth = boss.Health;
                UpdateAndDisplayBoss(boss, client);
            }
            return HookResult.Continue;
        }

        public HookResult BreakableOut(CEntityIOOutput output, string name, CEntityInstance activator, CEntityInstance caller, CVariant value, float delay)
        {
            if (caller == null || activator == null || activator.DesignerName != "player") return HookResult.Continue;
            var client = player(activator);
            if (client == null) return HookResult.Continue;

            var prop = new CBreakable(caller.Handle);
            if (!prop.IsValid) return HookResult.Continue;
            
            var hp = prop.Health;
            var entityname = caller.Entity.Name.ToString();

            if (configLoaded && !string.IsNullOrWhiteSpace(entityname))
            {
                 if (!BossConfigs.BreakableList.Any(b => entityname.StartsWith(b.Breakable)))
                 {
                    var sanitizedName = SanitizeBossName(entityname);
                    if (!BossConfigs.BreakableList.Any(b => b.Breakable == sanitizedName))
                    {
                        var newBossConfig = new BreakableConfig { Name = sanitizedName, Breakable = sanitizedName, Enabled = (hp > 10) };
                        BossConfigs.BreakableList.Add(newBossConfig);
                        SaveChanges();
                        var newLiveBoss = new BreakableBoss { BossName = newBossConfig.Name, Enabled = newBossConfig.Enabled, Type = BossType.Breakable, BreakableEntityName = newBossConfig.Breakable, BreakableEntity = caller };
                        breakableBosses.Add(newLiveBoss);
                    }
                 }
            }
            
            foreach (var boss in breakableBosses.Where(b => entityname.StartsWith(b.BreakableEntityName)))
            {
                if (boss.IsSegmented && hp <= 0 && boss.HealthSegmentCounterEntity != null && boss.HealthSegmentCounterEntity.IsValid)
                {
                    AddTimer(0.1f, () => {
                        if (boss == null || !boss.HealthSegmentCounterEntity.IsValid) return;
                        HandleSegmentEnd(boss, () => {
                            if (boss.BreakableEntity == null || !boss.BreakableEntity.IsValid) return;
                            if (boss.MaxHealth > 0)
                            {
                                Server.ExecuteCommand($"ent_fire {entityname} SetHealth {boss.MaxHealth}");
                                boss.Health = boss.MaxHealth;
                            }
                        });
                    });
                    continue;
                }
                if (hp <= 0)
                {
                    activeBosses.Remove(boss.BossName);
                    continue;
                }
                boss.Health = hp;
                if (boss.MaxHealth <= 0) boss.MaxHealth = hp;
                UpdateAndDisplayBoss(boss, client);
            }
            return HookResult.Continue;
        }

        private void HandleSegmentEnd(dynamic boss, Action resetAction)
        {
            if (boss.HealthSegmentCounterMode == 2)
            {
                var destroyedSegments = (int)GetMathCounterValue(boss.HealthSegmentCounterEntity.Handle);
                boss.HealthSegments = Math.Max(0, boss.TotalHealthSegments - destroyedSegments);
            }
            else boss.HealthSegments = (int)GetMathCounterValue(boss.HealthSegmentCounterEntity.Handle);
            if (boss.HealthSegments <= 0)
            {
                activeBosses.Remove(boss.BossName);
                return;
            }
            resetAction.Invoke();
            boss.LastHP = boss.Health;
        }

        public HookResult Hitbox_Hook(CEntityIOOutput output, string name, CEntityInstance activator, CEntityInstance caller, CVariant value, float delay)
        {
            return BreakableOut(output, name, activator, caller, value, delay);
        }

        public void CheckInactiveBosses()
        {
            if (activeBosses == null || activeBosses.Count == 0) return;

            var currentTime = Server.EngineTime;
            var bossesToRemove = new List<string>();

            foreach (var boss in activeBosses.Values)
            {
                if (boss.LastHit > 0 && currentTime - boss.LastHit > 10.0f)
                {
                    bossesToRemove.Add(boss.BossName);
                }
            }

            foreach (var bossName in bossesToRemove)
            {
                activeBosses.Remove(bossName);
            }
        }

        private void UpdateAndDisplayBoss(BossData boss, CCSPlayerController client)
        {
            if (boss.LastHP > boss.Health) Print_BossHP();
            boss.LastHP = boss.Health;
            boss.LastHit = Server.EngineTime;
            if (boss.Enabled)
            {
                if (!activeBosses.ContainsKey(boss.BossName)) activeBosses.Add(boss.BossName, boss);
                Print_SingleBossHP(client, boss);
            }
        }

        private void Print_BossHP()
        {
            if (!ShowingMultiBoss()) return;
            var displayableBosses = activeBosses.Values.Where(b => b.Enabled && b.Health >= 0).ToList();
            if (displayableBosses.Count == 0) return;
            string message;
            if (displayableBosses.Count == 1) message = FormatBossMessage(displayableBosses[0]);
            else
            {
                var bossMessages = displayableBosses.Select(b =>
                {
                    var percent = b.MaxHealth > 0 ? (int)Math.Round((double)b.Health / b.MaxHealth * 100) : 100;
                    return $"{b.BossName} : {b.Health} ({percent}%)";
                });
                message = string.Join("\n", bossMessages);
            }
            PrintToCenterAll(message);
        }

        private void Print_SingleBossHP(CCSPlayerController client, BossData boss)
        {
            if (ShowingMultiBoss() || client == null || !client.IsValid || !boss.Enabled || boss.Health < 0) return;
            var message = FormatBossMessage(boss);
            client.PrintToCenter(message);
        }
        
        private string FormatBossMessage(BossData boss)
        {
            if (boss is MathCounterBoss mcBoss && mcBoss.IsSegmented) return $"{mcBoss.BossName} | Segments: {mcBoss.HealthSegments}\n{mcBoss.Health}/{mcBoss.MaxHealth} {CalculateHPBar(mcBoss.Health, mcBoss.MaxHealth)}";
            if (boss is BreakableBoss bBoss && bBoss.IsSegmented) return $"{bBoss.BossName} | Segments: {bBoss.HealthSegments}\n{bBoss.Health}/{bBoss.MaxHealth} {CalculateHPBar(bBoss.Health, bBoss.MaxHealth)}";
            return $"{boss.BossName}\n{boss.Health} {CalculateHPBar(boss.Health, boss.MaxHealth)}";
        }
        
        private string CalculateHPBar(int hp, int maxhp)
        {
            if (maxhp <= 0) return "■■■■■■■■■■";
            if (hp <= 0) return "□□□□□□□□□□";
            float ratio = (float)hp / maxhp;
            int filledBlocks = (int)Math.Round(ratio * 10);
            filledBlocks = Math.Max(0, Math.Min(10, filledBlocks));
            return new string('■', filledBlocks) + new string('□', 10 - filledBlocks);
        }

        public static CCSPlayerController player(CEntityInstance instance)
        {
            if (instance == null || instance.DesignerName != "player") return null;
            var p = instance.As<CCSPlayerPawn>();
            return (p != null && p.IsValid && p.OriginalController.Value != null && p.OriginalController.Value.IsValid) ? p.OriginalController.Value : null;
        }

        void PrintToCenterAll(string text)
        {
            foreach(var p in Utilities.GetPlayers().Where(p => p != null && p.IsValid && !p.IsBot)) p.PrintToCenter(text);
        }

        private unsafe float GetMathCounterValue(nint handle)
        {
            if(handle == IntPtr.Zero) return 0;
            var offset = Schema.GetSchemaOffset("CMathCounter", "m_OutValue");
            return *(float*)IntPtr.Add(handle, offset + 24);
        }

        private bool IsBhudEnabled() => cvarEnableBhud.Value;
        private bool ShowingMultiBoss() => cvarMultiBossHP.Value;

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

public class CEntityOutputTemplate_float : NativeObject
{
    public CEntityOutputTemplate_float(IntPtr pointer) : base(pointer) { }
    public unsafe float OutValue => Unsafe.Add(ref *(float*)Handle, 6);
}
