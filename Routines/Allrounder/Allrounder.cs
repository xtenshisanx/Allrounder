using System;
using System.Windows.Forms;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml;
using System.IO;

using JetBrains.Annotations;

using log4net;
using Loki.Bot;
using Loki.Bot.Logic.Behaviors;
using Loki.Game;
using Loki.Game.Inventory;
using Loki.Game.Objects;
using Loki.Game.Objects.Components;
using Loki.TreeSharp;
using Loki.Utilities;

//using Allrounder.SettingsWindow;

using Action = Loki.TreeSharp.Action;

namespace Allrounder
{
    public class Variables
    {
        public static bool IsStarted = false;
        public static readonly ILog Log = Logger.GetLoggerInstanceForType();
        public static PrioritySelector Fight;
        public static LocalPlayer Me { get { return LokiPoe.Me; } }
        public static Monster MainTarget { get { return Targeting.Combat.Targets.FirstOrDefault() as Monster; } }
        public static Monster LastTarget;
        public static List<Skill> SkillList = new List<Skill>();
        public static readonly WaitTimer _flaskCd = new WaitTimer(TimeSpan.FromSeconds(0.5));
        public static IEnumerable<InventoryItem> LifeFlasks
        {
            get
            {
                IEnumerable<InventoryItem> inv = LokiPoe.Me.Inventory.Flasks.Items;
                return from item in inv
                       let flask = item.Flask
                       where flask != null && flask.HealthRecover > 0 && flask.CanUse
                       orderby flask.IsInstantRecovery ? flask.HealthRecover : flask.HealthRecoveredPerSecond descending
                       select item;
            }
        }
        public static IEnumerable<InventoryItem> ManaFlasks
        {
            get
            {
                IEnumerable<InventoryItem> inv = LokiPoe.Me.Inventory.Flasks.Items;
                return from item in inv
                       let flask = item.Flask
                       where flask != null && flask.ManaRecover > 0 && flask.CanUse
                       orderby flask.IsInstantRecovery ? flask.ManaRecover : flask.ManaRecoveredPerSecond descending
                       select item;
            }
        }
        public static IEnumerable<InventoryItem> GraniteFlasks
        {
            get
            {
                IEnumerable<InventoryItem> inv = LokiPoe.Me.Inventory.Flasks.Items;
                return from item in inv
                       let flask = item.Flask
                       where flask != null && item.Name == "Granite Flask" && flask.CanUse
                       select item;
            }
        }
        public static IEnumerable<InventoryItem> QuicksilverFlasks
        {
            get
            {
                //InternalName: flask_utility_sprint, BuffType: 24, CasterId: 13848, OwnerId: 0, TimeLeft: 00:00:05.0710000, Charges: 1, Description: You have greatly increased Movement Speed
                IEnumerable<InventoryItem> inv = LokiPoe.Me.Inventory.Flasks.Items;
                return from item in inv
                       let flask = item.Flask
                       where flask != null && item.Name == "Quicksilver Flask" && flask.CurrentCharges > 0
                       select item;
            }
        }
        public static InventoryItem GetaQuicksilver()
        {
            foreach (InventoryItem _item in LokiPoe.Me.Inventory.Flasks.Items)
                if (_item != null && _item.Name == "Quicksilver Flask" && _item.Flask.CurrentCharges > 0)
                    return _item;
            return null;
        }
    }
    public class Functions
    {
        //Composites
        public static Composite Cast(string spell, SpellManager.GetSelection<bool> reqs = null)
        {
            // Note: this is safe to do. If we pass null to the requirements check, that means we always want to fire
            // as long as CanCast is true.
            if (reqs == null)
            {
                reqs = ret => true;
            }

            return SpellManager.CreateSpellCastComposite(spell, reqs, ret => Variables.MainTarget);
        }
        public static Composite Cast(string spell, SpellManager.GetSelection<Vector2i> location, SpellManager.GetSelection<bool> reqs = null)
        {
            // Note: this is safe to do. If we pass null to the requirements check, that means we always want to fire
            // as long as CanCast is true.
            if (reqs == null)
            {
                reqs = ret => true;
            }

            return SpellManager.CreateSpellCastComposite(spell, reqs, location);
        }
        /// <summary>
        /// Move into LineofSight
        /// </summary>
        /// <returns></returns>
        public static Composite CreateMoveToLos()
        {
            return new Decorator(ret => !LokiPoe.MeleeLineOfSight.CanSee(LokiPoe.Me.Position, Variables.MainTarget.Position),
                CommonBehaviors.MoveTo(ret => Variables.MainTarget.Position, ret => "CreateMoveToLos"));
        }
        /// <summary>
        /// Move into given range for combat
        /// </summary>
        /// <param name="range">range for combat</param>
        /// <returns></returns>
        public static Composite CreateMoveToRange(int range)
        {
            return new Decorator(ret => Variables.MainTarget.Distance > range,
                CommonBehaviors.MoveTo(ret => Variables.MainTarget.Position, ret => "CreateMoveToRange"));
        }
        //Actors
        public static Actor GetCorpseNear(int distance)
        {
            return LokiPoe.EntityManager.OfType<Actor>().FirstOrDefault(corpse => corpse.IsValid && corpse.IsDead && corpse.Name != "Raised Zombie" && !corpse.Type.Contains("trap") && corpse.Distance <= distance);
        }
        //Ints
        public static int GetCorpsesNear(int distance)
        {
            return LokiPoe.EntityManager.OfType<Actor>().Count(corpse => /*corpse.IsValid && */corpse.IsDead && corpse.Name != "Raised Zombie" && !corpse.Type.Contains("trap") && corpse.Distance <= distance);
        }
        public static int NumberOfEnemysNearMe(int distance)
        {
            return LokiPoe.EntityManager.OfType<Actor>().Count(enemy => enemy.IsValid && enemy.Reaction == Reaction.Enemy && enemy.Distance <= distance && !enemy.IsDead);
        }
        //Voids
        public static void CreateConfig(string file)
        {
            TextWriter tw = new StreamWriter(file);
            tw.WriteLine("#Basic Settings");
            tw.WriteLine("#FightDistance = 0");
            tw.WriteLine("#PotHealth = 0");
            tw.WriteLine("#PotMana = 0");
            tw.WriteLine("#UseQuicksilverFlask_EnemyDistance = 0 //Distance to check for Enemys");
            tw.WriteLine("#UseQuicksilverFlask = false");
            //tw.WriteLine("#UseAura = AURANAME");

            tw.WriteLine("#AttackFields");
            tw.WriteLine("#Name = \"undefined\"");
            tw.WriteLine("#MinManaPercent = 0");
            tw.WriteLine("#MinLifePercent = 0");
            tw.WriteLine("#MinEnemyLifePercent = 0");
            tw.WriteLine("#Mobsarround_Distance = 0");
            tw.WriteLine("#Mobsarround_Count = 0");
            tw.WriteLine("#Mobsarround_Target = 0 // 0 -> Main Target / 1 -> Me");
            tw.WriteLine("#EnemyDistance = 0");
            tw.WriteLine("#EnemyinDistance = 0");
            tw.WriteLine("#MaxCount = 0 // For Raising/Trap Skills -> MaxCount of Minions/Traps");
            tw.WriteLine("#OnlyBosses = false");
            tw.WriteLine("#IsSummon = false");
            tw.WriteLine("#IsTrap = false");
            tw.WriteLine("#IsCurse = false");
            tw.WriteLine("#IsTotem = false");
            tw.WriteLine("#IsRanged = false // Set to True for nonmelee attacks so it checks for proximity");
            tw.WriteLine("#OnlyMobWithoutShield = false //Use this Attack only if Target has no Shield up");
            tw.WriteLine("#After a Skill you must Set 'CastEnd' then the CR knows a new Skill begins\n");

            tw.WriteLine("#Examples");
            tw.WriteLine("#//Unlimited Attack");
            tw.WriteLine("#Name = \"Fireball\"");
            tw.WriteLine("#CastEnd\n");
            tw.WriteLine("#//SummonSkills");
            tw.WriteLine("#Name = \"Raise Zombie\"");
            tw.WriteLine("#MinManaPercent = 50");
            tw.WriteLine("#MaxCount = 5");
            tw.WriteLine("#IsSummon = True");
            tw.WriteLine("#CastEnd\n");
            tw.WriteLine("#Name = \"Raise Spectre\"");
            tw.WriteLine("#MinManaPercent = 50");
            tw.WriteLine("#MaxCount = 1");
            tw.WriteLine("#IsSummon = True");
            tw.WriteLine("#CastEnd\n");
            tw.WriteLine("#//TrapSkill");
            tw.WriteLine("#Name = \"Lightning Trap\"");
            tw.WriteLine("#Mobsarround_Distance = 10");
            tw.WriteLine("#Mobsarround_Count = 3");
            tw.WriteLine("#Mobsarround_Target = 0");
            tw.WriteLine("#MaxCount = 3");
            tw.WriteLine("#IsTrap = True");
            tw.WriteLine("#CastEnd\n");
            tw.WriteLine("#//AOE/Defense AOE");
            tw.WriteLine("#Name = \"Ice Nova\"");
            tw.WriteLine("#Mobsarround_Distance = 20");
            tw.WriteLine("#Mobsarround_Count = 2");
            tw.WriteLine("#Mobsarround_Target = 1");
            tw.WriteLine("#CastEnd\n");

            tw.Close();
        }
        public static void ReadConfig(string file)
        {
            Variables.SkillList = new List<Skill>();
            TextReader tr = new StreamReader(file);
            int Skillnumber = 0;
            string line = null;

            while ((line = tr.ReadLine()) != null)
            {
                if (!line.Contains('#'))
                {
                    line = line.Replace("\"", "");
                    if (line.Split('=')[0].Trim().Equals("PotHealth"))
                        Settings.PotHealth = int.Parse(line.Split('=')[1].Trim());
                    if (line.Split('=')[0].Trim().Equals("PotMana"))
                        Settings.PotMana = int.Parse(line.Split('=')[1].Trim());
                    if (line.Split('=')[0].Trim().Equals("FightDistance"))
                        Settings.FightDistance = int.Parse(line.Split('=')[1].Trim());
                    if (line.Split('=')[0].Trim().Equals("UseQuicksilverFlask_EnemyDistance"))
                        Settings.UseQuicksilverFlask_EnemyDistance = int.Parse(line.Split('=')[1].Trim());
                    if (line.Split('=')[0].Trim().Equals("UseQuicksilverFlask"))
                        Settings.UseQuicksilverFlask = Boolean.Parse(line.Split('=')[1].Trim());
                    if (line.Split('=')[0].Trim().Equals("Name"))
                        Variables.SkillList.Add(new Skill(line.Split('=')[1].Trim()));
                    if (line.Split('=')[0].Trim().Equals("MinManaPercent"))
                        Variables.SkillList[Skillnumber].MinManaPercent = int.Parse(line.Split('=')[1].Trim());
                    if (line.Split('=')[0].Trim().Equals("MinLifePercent"))
                        Variables.SkillList[Skillnumber].MinLifePercent = int.Parse(line.Split('=')[1].Trim());
                    if (line.Split('=')[0].Trim().Equals("MinEnemylifePercent"))
                        Variables.SkillList[Skillnumber].MinEnemyLifePercent = int.Parse(line.Split('=')[1].Trim());
                    if (line.Split('=')[0].Trim().Equals("Mobsarround_Distance"))
                        Variables.SkillList[Skillnumber].Mobsarround_Distance = int.Parse(line.Split('=')[1].Trim());
                    if (line.Split('=')[0].Trim().Equals("Mobsarround_Count"))
                        Variables.SkillList[Skillnumber].Mobsarround_Count = int.Parse(line.Split('=')[1].Trim());
                    if (line.Split('=')[0].Trim().Equals("Mobsarround_Target"))
                        Variables.SkillList[Skillnumber].Mobsarround_Target = int.Parse(line.Split('=')[1].Trim());
                    if (line.Split('=')[0].Trim().Equals("EnemyDistance"))
                        Variables.SkillList[Skillnumber].EnemyDistance = int.Parse(line.Split('=')[1].Trim());
                    if (line.Split('=')[0].Trim().Equals("EnemyinDistance"))
                        Variables.SkillList[Skillnumber].EnemyinDistance = int.Parse(line.Split('=')[1].Trim());
                    if (line.Split('=')[0].Trim().Equals("MaxCount"))
                        Variables.SkillList[Skillnumber].MaxCount = int.Parse(line.Split('=')[1].Trim());
                    if (line.Split('=')[0].Trim().Equals("OnlyMobWithoutShield"))
                        Variables.SkillList[Skillnumber].OnlyMobWithoutShield = Boolean.Parse(line.Split('=')[1].Trim());
                    if (line.Split('=')[0].Trim().Equals("OnlyBosses"))
                        Variables.SkillList[Skillnumber].OnlyBosses = Boolean.Parse(line.Split('=')[1].Trim());
                    if (line.Split('=')[0].Trim().Equals("IsTrap"))
                        Variables.SkillList[Skillnumber].IsTrap = Boolean.Parse(line.Split('=')[1].Trim());
                    if (line.Split('=')[0].Trim().Equals("IsSummon"))
                        Variables.SkillList[Skillnumber].IsSummon = Boolean.Parse(line.Split('=')[1].Trim());
                    if (line.Split('=')[0].Trim().Equals("IsCurse"))
                        Variables.SkillList[Skillnumber].IsCurse = Boolean.Parse(line.Split('=')[1].Trim());
                    if (line.Split('=')[0].Trim().Equals("IsTotem"))
                        Variables.SkillList[Skillnumber].IsTotem = Boolean.Parse(line.Split('=')[1].Trim());
                    if (line.Split('=')[0].Trim().Equals("IsRanged"))
                        Variables.SkillList[Skillnumber].IsRanged = Boolean.Parse(line.Split('=')[1].Trim());
                    if (line.Split('=')[0].Trim().Equals("CastEnd"))
                        Skillnumber++;
                }
            }
            tr.Close();
        }
        public static void CheckForConfig()
        {
            string Filename = Variables.Me.Name + ".cfg";
            string Folder = Loki.Bot.GlobalSettings.SettingsPath + "\\Allrounder\\";
            if (File.Exists(Folder + Filename))
            {
                Variables.Log.Debug("File " + Folder + Filename + " Exists");
                ReadConfig(Folder + Filename);
            }
            else
            {
                Variables.Log.Debug("File " + Folder + Filename + " dosnt exists");
                CreateConfig(Folder + Filename);
                Variables.Log.Debug("Please Check your Character-Settingsfile in " + Folder);
                Variables.Log.Debug("And add your spells like in the example");
                BotMain.Stop("First time use");
            }
        }
        //Bools
        /// <summary>
        /// Check if a Object has got a spell
        /// </summary>
        /// <param name="_obj">Object/Actor</param>
        /// <param name="_spellname">Spellname</param>
        /// <returns>true/false</returns>
        public static bool ObjectHasSpell(Actor _obj, string _spellname)
        {
            foreach (Spell _spell in _obj.AvailableSpells)
            {
                if (_spell.IsValid)
                    if (_spell.Name != null && _spell.Name.Equals(_spellname))
                        return true;
            }
            return false;
        }
        public static bool NumberOfMobsNear(PoEObject Target, float distance, int count)
        {
            if (Target == null)
            {
                return false;
            }

            Vector2i mpos = Target.Position;

            int curCount = 0;
            foreach (Monster mob in Targeting.Combat.Targets)
            {
                if (mob.ID == Target.ID)
                {
                    continue;
                }

                if (mob.Position.Distance(mpos) < distance && !mob.IsDead)
                {
                    curCount++;
                }

                if (curCount >= count)
                {
                    return true;
                }
            }

            return false;
        }
        public static bool NumberOfEnemysNear(PoEObject Target, float distance, int count)
        {
            return LokiPoe.EntityManager.OfType<Actor>().Count(a => !a.IsDead && !a.IsFriendly && a.Position.Distance(Target.Position) <= distance) >= count;
        }
    }
    public class Settings
    {
        public static int PotHealth { get; set; }
        public static int PotMana { get; set; }
        public static int FightDistance { get; set; }
        public static int UseQuicksilverFlask_EnemyDistance { get; set; }
        public static bool UseQuicksilverFlask { get; set; }
    }
    public class Skill
    {
        public string Name = "undefined";
        public string Type
        {
            get
            {
                if (this.IsSummon)
                    return "Summon";
                else if (this.IsTotem)
                    return "Totem";
                else if (this.IsCurse)
                    return "Curse";
                else if (this.IsTrap)
                    return "Trap";
                else if (this.IsRanged)
                    return "Ranged";
                return "Melee";
            }
        }
        public int MinManaPercent { get; set; }
        public int MinLifePercent { get; set; }
        public int MinEnemyLifePercent { get; set; }
        public int Mobsarround_Distance { get; set; }
        public int Mobsarround_Count { get; set; }
        public int Mobsarround_Target { get; set; }
        public int EnemyinDistance { get; set; }
        public int EnemyDistance { get; set; }
        public int MaxCount { get; set; }
        public int CurrentCount { get; set; }

        public bool OnlyMobWithoutShield { get; set; }
        public bool OnlyBosses { get; set; }
        public bool IsTrap { get; set; }
        public bool IsSummon { get; set; }
        public bool IsCurse { get; set; }
        public bool IsTotem { get; set; }
        public bool IsRanged { get; set; }

        public Skill(string _name)
        {
            this.Name = _name;
            this.MinManaPercent = 0;
            this.MinLifePercent = 0;
            this.MinEnemyLifePercent = 0;
            this.Mobsarround_Distance = 0;
            this.Mobsarround_Count = 0;
            this.Mobsarround_Target = 0;
            this.EnemyinDistance = 0;
            this.EnemyDistance = 0;
            this.MaxCount = 0;
            this.CurrentCount = 0;

            this.OnlyMobWithoutShield= false;
            this.OnlyBosses= false;
            this.IsTrap= false;
            this.IsSummon= false;
            this.IsCurse= false;
            this.IsTotem= false;
            this.IsRanged= false;
        }

        /// <summary>
        /// Checks if the current Traptype should be thrown
        /// </summary>
        /// <returns></returns>
        public bool ShouldThrowTrap()
        {
            if (LokiPoe.EntityManager.OfType<Actor>().Count(trap => trap.IsValid && !trap.IsDead && Functions.ObjectHasSpell(trap, this.Name)) < MaxCount)
                return true;
            return false;
        }
        public bool ShouldRaiseMinions()
        {
            if (this.Name.Equals("Raise Zombie") && LokiPoe.EntityManager.OfType<Actor>().Count(zombies => zombies.IsValid && !zombies.IsDead && zombies.Reaction == Reaction.Friendly && zombies.Name == "Raised Zombie") != MaxCount && Functions.GetCorpsesNear(60) > 0)
            {
                return true;
            }

            if (this.Name.Equals("Raise Spectre") && LokiPoe.EntityManager.OfType<Actor>().Count(spectre => spectre.IsValid && !spectre.IsDead && spectre.Reaction == Reaction.Friendly && spectre.HasAura("Spectral")) != MaxCount && Functions.GetCorpsesNear(60) > 0)
            {
                return true;
            }
            return false;
        }
        public bool ShouldRaiseTotem()
        {
            Vector2i targetpos = Variables.MainTarget.Position;
            if (LokiPoe.EntityManager.OfType<Actor>().Count(totem => totem.IsValid && !totem.IsDead && totem.Reaction == Reaction.Friendly && totem.Name.Equals("Totem") && Functions.ObjectHasSpell(totem, this.Name) && (Variables.MainTarget.Position.Distance(totem.Position) <= 30)) < MaxCount)
            {
                return true;
            }
            return false;
        }
        /// <summary>
        /// Checks if the current Curse should be casted
        /// </summary>
        /// <returns></returns>
        public bool ShouldCastCurse()
        {
            if (!Variables.MainTarget.HasAura(this.Name) && Variables.MainTarget.IsCursable)
                return true;
            return false;
        }
        public bool CanCast()
        {
            Variables.Log.Debug("CanCast(" + this.Name + ") Check for Cast");
            int Truechecks = 0;
            int Trues = 0;
            //Ints
            if (this.MinManaPercent != 0)
                Truechecks++;
            if (this.MinLifePercent != 0)
                Truechecks++;
            if (this.MinEnemyLifePercent != 0)
                Truechecks++;
            if (this.Mobsarround_Distance != 0)
                Truechecks++;
            if (this.EnemyDistance != 0)
                Truechecks++;
            if (this.EnemyinDistance != 0)
                Truechecks++;
            if (this.MaxCount != 0 && !this.IsTrap && !this.IsSummon && !this.IsTotem)
                Truechecks++;
            //Bools
            if (this.OnlyMobWithoutShield)
                Truechecks++;
            if (this.OnlyBosses)
                Truechecks++;
            if (this.IsTrap)
                Truechecks++;
            if (this.IsSummon)
                Truechecks++;
            if (this.IsCurse)
                Truechecks++;
            if (this.IsTotem)
                Truechecks++;
            if (this.IsRanged)
                Truechecks++;
            if (Truechecks == 0)
            {
                Variables.Log.Debug("Allrounder(Cast): " + this.Name);
                return true;
            }
            //Trues
            if (this.MinManaPercent != 0 && Variables.Me.ManaPercent >= this.MinManaPercent)
                Trues++;
            if (this.MinLifePercent != 0 && Variables.Me.HealthPercent >= this.MinLifePercent)
                Trues++;
            if (this.MinEnemyLifePercent != 0 && Variables.MainTarget.HealthPercent >= this.MinEnemyLifePercent)
                Trues++;
            if (this.Mobsarround_Distance != 0 && (Mobsarround_Target == 1 && Functions.NumberOfEnemysNear(LokiPoe.Me, Mobsarround_Distance, Mobsarround_Count) || Mobsarround_Target == 0 && Functions.NumberOfEnemysNear(Variables.MainTarget, Mobsarround_Distance, Mobsarround_Count)))
                Trues++;
            if (this.EnemyinDistance != 0 && Variables.MainTarget.Distance <= this.EnemyinDistance)
                Trues++;
            if (this.EnemyDistance != 0 && Variables.MainTarget.Distance >= this.EnemyDistance)
                Trues++;
            //foreach (Aura _aura in Variables.MainTarget.Auras)
                //Variables.Log.Debug(_aura.Name);
            if (IsRanged && !Variables.MainTarget.HasAura("Richmen's Shield"))
                Trues++;
            if (IsCurse && ShouldCastCurse())
                Trues++;
            if (this.IsTotem && ShouldRaiseTotem())
                Trues++;
            if (this.MaxCount != 0)
            {
                //TrapCheck
                if (this.IsTrap && ShouldThrowTrap())
                    Trues++;
                //SummonCheck
                if (this.IsSummon && ShouldRaiseMinions())
                    Trues++;
                //TotemCheck
                //AttackCheck
                if (!this.IsTrap && !this.IsSummon && !this.IsTotem && CurrentCount != MaxCount)
                    Trues++;
            }
            if (Variables.MainTarget.Rarity >= Rarity.Rare && OnlyBosses)
                Trues++;
            Variables.Log.DebugFormat("Allrounder(Cast): Trying to Cast {0} Truechecks {1}:{2}", this.Name, Trues, Truechecks);
            if (Trues >= Truechecks && !Variables.Me.IsAbilityCooldownActive)
            {
                Variables.Log.Debug("Allrounder(Cast): " + this.Name);
                return true;
            }
            return false;
        }
    }

    [UsedImplicitly]
    public partial class Allrounder : CombatRoutine
    {
        public override string Name { get { return "Allrounder by xTenshiSanx"; } }
        public override string ToString() { return Name; }
        public override void Dispose() { }
        public override void OnGuiButtonPress() { new CRConfig.Window().Show(); }
        public override Composite Combat { get { return CombatBot(); } }
        public override Composite Buff { get { return BuffBot(); } }
        public override void Initialize()
        {
            Variables.Fight = new PrioritySelector();
            Settings.FightDistance = 50;
            BotMain.OnStart += OnStart;
            BotMain.OnStop += OnStop;
            BotMain.OnTick += Pulse;
            Log.Debug("Allrounder by xTenshiSanx has been loaded");
        }
        void OnStart(IBot bot)
        {
            if (RoutineManager.Current.Name == this.Name)
            {
                if (!Variables.IsStarted)
                {
                    Log.Debug("Allrounder by xTenshiSanx has been started");
                    Log.Debug("Loading Skills...");
                    Functions.CheckForConfig();
                    foreach (Skill atk in Variables.SkillList)
                    {
                        if (atk.IsSummon)
                        {
                            Variables.Fight.AddChild(Functions.Cast(atk.Name, ret => Functions.GetCorpseNear(60).Position, ret => atk.CanCast()));
                            Log.Debug("Allrounder(OnStart): Added Skill " + atk.Name + " of Type " + atk.Type);
                        }
                        else
                        {
                            Variables.Fight.AddChild(Functions.Cast(atk.Name, ret => atk.CanCast()));
                            Log.Debug("Allrounder(OnStart): Added Skill " + atk.Name + " of Type " + atk.Type);
                        }
                    }
                    Variables.IsStarted = true;
                }
            }
        }
        void OnStop(IBot bot)
        {
            if (RoutineManager.Current.Name == this.Name)
            {
                Variables.Fight = new PrioritySelector();
                Variables.SkillList = new List<Skill>();
                Variables.IsStarted = false;
            }
        }
        void Pulse(IBot bot)
        {
            ///If Bot changes Target
            if(Variables.LastTarget == null || Variables.LastTarget != Variables.MainTarget)
            {
                Variables.LastTarget = Variables.MainTarget;
                foreach(Skill _skill in Variables.SkillList)
                {
                    _skill.CurrentCount = 0;
                }
            }
        }
        private Composite FlaskBot()
        {
            return new PrioritySelector(
                new Decorator(ret => Variables._flaskCd.IsFinished && Variables.Me.HealthPercent < Settings.PotHealth && Variables.LifeFlasks.Count() != 0 && !Variables.Me.HasAura("flask_effect_life"),
                    new Action(ret =>
                    {
                        Variables.LifeFlasks.First().Use();
                        Variables._flaskCd.Reset();
                    })),
                new Decorator(ret => Variables._flaskCd.IsFinished && Variables.Me.ManaPercent < Settings.PotMana && Variables.ManaFlasks.Count() != 0 && !Variables.Me.HasAura("flask_effect_mana"),
                    new Action(ret =>
                    {
                        Variables.ManaFlasks.First().Use();
                        Variables._flaskCd.Reset();
                    }))
                );
        }
        private Composite BuffBot()
        {
            return new PrioritySelector();
        }
        private Composite CombatBot()
        {
            return new PrioritySelector(
                FlaskBot(),
                Functions.CreateMoveToLos(),
                Functions.CreateMoveToRange(Settings.FightDistance),
                Variables.Fight
            );
        }
    }
}
namespace CRConfig
{
    partial class Window
    {
        /// <summary>
        /// Erforderliche Designervariable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Verwendete Ressourcen bereinigen.
        /// </summary>
        /// <param name="disposing">True, wenn verwaltete Ressourcen gelöscht werden sollen; andernfalls False.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Vom Windows Form-Designer generierter Code

        /// <summary>
        /// Erforderliche Methode für die Designerunterstützung.
        /// Der Inhalt der Methode darf nicht mit dem Code-Editor geändert werden.
        /// </summary>
        private void InitializeComponent()
        {

            this.RemoveCurrentSkillButton = new System.Windows.Forms.Button();
            this.Body = new System.Windows.Forms.TabControl();
            this.Charactersettings = new System.Windows.Forms.TabPage();
            this.Skillsettings = new System.Windows.Forms.TabPage();
            this.MoveCurrentSkillRight = new System.Windows.Forms.Button();
            this.MoveCurrentSkillLeft = new System.Windows.Forms.Button();
            this.NewSkillName = new System.Windows.Forms.TextBox();
            this.NewSkillButton = new System.Windows.Forms.Button();
            this.Skillbody = new System.Windows.Forms.TabControl();
            this.label1 = new System.Windows.Forms.Label();
            this.Savebutton = new System.Windows.Forms.Button();
            this.PotHealthBox = new System.Windows.Forms.TextBox();
            this.GeneralSettingsLabel = new System.Windows.Forms.Label();
            this.PotHealthLabel = new System.Windows.Forms.Label();
            this.PotManaLabel = new System.Windows.Forms.Label();
            this.PotManaBox = new System.Windows.Forms.TextBox();
            this.FightDistanceLabel = new System.Windows.Forms.Label();
            this.FightDistanceBox = new System.Windows.Forms.TextBox();
            this.Body.SuspendLayout();
            this.Charactersettings.SuspendLayout();
            this.Skillsettings.SuspendLayout();
            this.SuspendLayout();
            // 
            // Body
            // 
            this.Body.AllowDrop = true;
            this.Body.Controls.Add(this.Charactersettings);
            this.Body.Controls.Add(this.Skillsettings);
            this.Body.Location = new System.Drawing.Point(12, 39);
            this.Body.Name = "Body";
            this.Body.SelectedIndex = 0;
            this.Body.Size = new System.Drawing.Size(537, 425);
            this.Body.TabIndex = 0;
            // 
            // Charactersettings
            // 
            this.Charactersettings.AllowDrop = true;
            this.Charactersettings.Controls.Add(this.FightDistanceLabel);
            this.Charactersettings.Controls.Add(this.FightDistanceBox);
            this.Charactersettings.Controls.Add(this.PotManaLabel);
            this.Charactersettings.Controls.Add(this.PotManaBox);
            this.Charactersettings.Controls.Add(this.PotHealthLabel);
            this.Charactersettings.Controls.Add(this.GeneralSettingsLabel);
            this.Charactersettings.Controls.Add(this.PotHealthBox);
            this.Charactersettings.Location = new System.Drawing.Point(4, 22);
            this.Charactersettings.Name = "Charactersettings";
            this.Charactersettings.Padding = new System.Windows.Forms.Padding(3);
            this.Charactersettings.Size = new System.Drawing.Size(529, 399);
            this.Charactersettings.TabIndex = 0;
            this.Charactersettings.Text = "Charactersettings";
            this.Charactersettings.UseVisualStyleBackColor = true;
            // 
            // Skillsettings
            // 
            this.Skillsettings.Controls.Add(this.MoveCurrentSkillRight);
            this.Skillsettings.Controls.Add(this.MoveCurrentSkillLeft);
            this.Skillsettings.Controls.Add(this.RemoveCurrentSkillButton);
            this.Skillsettings.Controls.Add(this.NewSkillName);
            this.Skillsettings.Controls.Add(this.NewSkillButton);
            this.Skillsettings.Controls.Add(this.Skillbody);
            this.Skillsettings.Location = new System.Drawing.Point(4, 22);
            this.Skillsettings.Name = "Skillsettings";
            this.Skillsettings.Padding = new System.Windows.Forms.Padding(3);
            this.Skillsettings.Size = new System.Drawing.Size(529, 399);
            this.Skillsettings.TabIndex = 1;
            this.Skillsettings.Text = "Skillsettings";
            this.Skillsettings.UseVisualStyleBackColor = true;
            // 
            // MoveCurrentSkillRight
            // 
            this.MoveCurrentSkillRight.Location = new System.Drawing.Point(73, 333);
            this.MoveCurrentSkillRight.Name = "MoveCurrentSkillRight";
            this.MoveCurrentSkillRight.Size = new System.Drawing.Size(60, 60);
            this.MoveCurrentSkillRight.TabIndex = 32;
            this.MoveCurrentSkillRight.Text = "Move Current Skill Right";
            this.MoveCurrentSkillRight.UseVisualStyleBackColor = true;
            this.MoveCurrentSkillRight.Click += new System.EventHandler(this.MoveSkills);
            // 
            // MoveCurrentSkillLeft
            // 
            this.MoveCurrentSkillLeft.Location = new System.Drawing.Point(7, 333);
            this.MoveCurrentSkillLeft.Name = "MoveCurrentSkillLeft";
            this.MoveCurrentSkillLeft.Size = new System.Drawing.Size(60, 60);
            this.MoveCurrentSkillLeft.TabIndex = 31;
            this.MoveCurrentSkillLeft.Text = "Move Current Skill Left";
            this.MoveCurrentSkillLeft.UseVisualStyleBackColor = true;
            this.MoveCurrentSkillLeft.Click += new System.EventHandler(this.MoveSkills);
            // 
            // NewSkillName
            // 
            this.NewSkillName.Location = new System.Drawing.Point(205, 337);
            this.NewSkillName.Name = "NewSkillName";
            this.NewSkillName.Size = new System.Drawing.Size(317, 20);
            this.NewSkillName.TabIndex = 30;
            // 
            // NewSkillButton
            // 
            this.NewSkillButton.Location = new System.Drawing.Point(205, 363);
            this.NewSkillButton.Name = "NewSkillButton";
            this.NewSkillButton.Size = new System.Drawing.Size(317, 30);
            this.NewSkillButton.TabIndex = 29;
            this.NewSkillButton.Text = "NewSkill";
            this.NewSkillButton.UseVisualStyleBackColor = true;
            this.NewSkillButton.Click += new System.EventHandler(this.NewSkillButton_Click);
            // 
            // Skillbody
            // 
            this.Skillbody.HotTrack = true;
            this.Skillbody.Location = new System.Drawing.Point(3, 3);
            this.Skillbody.Name = "Skillbody";
            this.Skillbody.SelectedIndex = 0;
            this.Skillbody.Size = new System.Drawing.Size(523, 315);
            this.Skillbody.TabIndex = 0;
            // 
            // label1
            // 
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(12, 16);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(531, 23);
            this.label1.TabIndex = 1;
            this.label1.Text = "Charactername";
            this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // Savebutton
            // 
            this.Savebutton.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Savebutton.Location = new System.Drawing.Point(12, 470);
            this.Savebutton.Name = "Savebutton";
            this.Savebutton.Size = new System.Drawing.Size(537, 30);
            this.Savebutton.TabIndex = 2;
            this.Savebutton.Text = "Save";
            this.Savebutton.UseVisualStyleBackColor = true;
            this.Savebutton.Click += new System.EventHandler(this.Save);
            // 
            // RemoveCurrentSkillButton
            // 
            this.RemoveCurrentSkillButton.Location = new System.Drawing.Point(139, 333);
            this.RemoveCurrentSkillButton.Name = "RemoveCurrentSkillButton";
            this.RemoveCurrentSkillButton.Size = new System.Drawing.Size(60, 60);
            this.RemoveCurrentSkillButton.TabIndex = 33;
            this.RemoveCurrentSkillButton.Text = "Remove Current Skill";
            this.RemoveCurrentSkillButton.UseVisualStyleBackColor = true;
            this.RemoveCurrentSkillButton.Click += new System.EventHandler(this.RemoveCurrentSkillButton_Click);
            // 
            // PotHealthBox
            // 
            this.PotHealthBox.Location = new System.Drawing.Point(92, 40);
            this.PotHealthBox.Name = "PotHealthBox";
            this.PotHealthBox.Size = new System.Drawing.Size(100, 20);
            this.PotHealthBox.TabIndex = 0;
            // 
            // GeneralSettingsLabel
            // 
            this.GeneralSettingsLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.GeneralSettingsLabel.Location = new System.Drawing.Point(6, 14);
            this.GeneralSettingsLabel.Name = "GeneralSettingsLabel";
            this.GeneralSettingsLabel.Size = new System.Drawing.Size(186, 23);
            this.GeneralSettingsLabel.TabIndex = 1;
            this.GeneralSettingsLabel.Text = "General Settings";
            this.GeneralSettingsLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // PotHealthLabel
            // 
            this.PotHealthLabel.AutoSize = true;
            this.PotHealthLabel.Location = new System.Drawing.Point(6, 43);
            this.PotHealthLabel.Name = "PotHealthLabel";
            this.PotHealthLabel.Size = new System.Drawing.Size(62, 13);
            this.PotHealthLabel.TabIndex = 2;
            this.PotHealthLabel.Text = "PotHealth%";
            // 
            // PotManaLabel
            // 
            this.PotManaLabel.AutoSize = true;
            this.PotManaLabel.Location = new System.Drawing.Point(6, 69);
            this.PotManaLabel.Name = "PotManaLabel";
            this.PotManaLabel.Size = new System.Drawing.Size(58, 13);
            this.PotManaLabel.TabIndex = 4;
            this.PotManaLabel.Text = "PotMana%";
            // 
            // PotManaBox
            // 
            this.PotManaBox.Location = new System.Drawing.Point(92, 66);
            this.PotManaBox.Name = "PotManaBox";
            this.PotManaBox.Size = new System.Drawing.Size(100, 20);
            this.PotManaBox.TabIndex = 3;
            // 
            // FightDistanceLabel
            // 
            this.FightDistanceLabel.AutoSize = true;
            this.FightDistanceLabel.Location = new System.Drawing.Point(6, 95);
            this.FightDistanceLabel.Name = "FightDistanceLabel";
            this.FightDistanceLabel.Size = new System.Drawing.Size(72, 13);
            this.FightDistanceLabel.TabIndex = 6;
            this.FightDistanceLabel.Text = "FightDistance";
            // 
            // FightDistanceBox
            // 
            this.FightDistanceBox.Location = new System.Drawing.Point(92, 92);
            this.FightDistanceBox.Name = "FightDistanceBox";
            this.FightDistanceBox.Size = new System.Drawing.Size(100, 20);
            this.FightDistanceBox.TabIndex = 5;
            // 
            // AllrounderSettings
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(561, 512);
            this.Controls.Add(this.Savebutton);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.Body);
            this.Name = "AllrounderSettings";
            this.Text = "Allrounder Settings";
            this.Body.ResumeLayout(false);
            this.Charactersettings.ResumeLayout(false);
            this.Charactersettings.PerformLayout();
            this.Skillsettings.ResumeLayout(false);
            this.Skillsettings.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button RemoveCurrentSkillButton;
        private System.Windows.Forms.TabControl Body;
        private System.Windows.Forms.TabPage Charactersettings;
        private System.Windows.Forms.TabPage Skillsettings;
        private System.Windows.Forms.TabControl Skillbody;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button Savebutton;
        private System.Windows.Forms.TextBox NewSkillName;
        private System.Windows.Forms.Button NewSkillButton;
        private System.Windows.Forms.Button MoveCurrentSkillRight;
        private System.Windows.Forms.Button MoveCurrentSkillLeft;
        private System.Windows.Forms.Label FightDistanceLabel;
        private System.Windows.Forms.TextBox FightDistanceBox;
        private System.Windows.Forms.Label PotManaLabel;
        private System.Windows.Forms.TextBox PotManaBox;
        private System.Windows.Forms.Label PotHealthLabel;
        private System.Windows.Forms.Label GeneralSettingsLabel;
        private System.Windows.Forms.TextBox PotHealthBox;
    }
    public partial class Window : Form
    {
        public Window()
        {
            InitializeComponent();
            Load();
        }

        public void Load()
        {
            System.IO.StreamReader SR = new System.IO.StreamReader(Loki.Bot.CharacterSettings.SettingsPath + "\\Allrounder\\" + LokiPoe.Me.Name + ".cfg");
            string line = null;
            string left = null;
            string right = null;
            TabPage current = null;
            while ((line = SR.ReadLine()) != null)
            {
                left = null;
                right = null;
                if (line.Split('=')[0].Trim() != null)
                    left = line.Split('=')[0].Trim();
                if (left != null && !left.Equals("CastEnd") && !left.Equals("") && !left.Contains('#'))
                {
                    
                    if (line.Split('=')[1].Trim() != null)
                        right = line.Split('=')[1].Trim();
                    
                    if (left.Equals("Name"))
                    {
                        this.Skillbody.TabPages.Add(new Attack(right));
                        current = this.Skillbody.TabPages[right];
                        continue;
                    }
                    if (left.Equals("FightDistance"))
                    {
                        this.Charactersettings.Controls["FightDistanceBox"].Text = right;
                        continue;
                    }

                    if (left.Equals("PotHealth"))
                    {
                        this.PotHealthBox.Text = right;
                        continue;
                    }
                    if (left.Equals("PotMana"))
                    {
                        this.Charactersettings.Controls["PotManaBox"].Text = right;
                        continue;
                    }
                    if (!Left.Equals("FightDistance")&&!Left.Equals("PotHealth") && !Left.Equals("PotMana") && this.Skillbody.TabPages[this.Skillbody.TabPages.IndexOf(current)].Controls.Find(left + "Box", true).FirstOrDefault() != null)
                    {
                        object temp = null;
                        if (this.Skillbody.TabPages[this.Skillbody.TabPages.IndexOf(current)].Controls.Find(left + "Box", true).FirstOrDefault().GetType().Name == "TextBox")
                        {
                            temp = (TextBox)this.Skillbody.TabPages[this.Skillbody.TabPages.IndexOf(current)].Controls[left + "Box"];
                            (temp as TextBox).Text = line.Split('=')[1].Trim();
                            continue;
                        }
                        if (this.Skillbody.TabPages[this.Skillbody.TabPages.IndexOf(current)].Controls.Find(left + "Box", true).FirstOrDefault().GetType().Name == "ComboBox")
                        {
                            temp = (ComboBox)this.Skillbody.TabPages[this.Skillbody.TabPages.IndexOf(current)].Controls[left + "Box"];
                            (temp as ComboBox).Text = line.Split('=')[1].Trim();
                            continue;
                        }
                    }
                    if (right != null && Boolean.Parse(line.Split('=')[1].Trim()))
                    {
                        CheckedListBox temp = (CheckedListBox)this.Skillbody.TabPages[this.Skillbody.TabPages.IndexOf(current)].Controls["SkilltypeBox"];
                        int buttonindex = temp.Items.IndexOf(left);
                        temp.SetItemChecked(buttonindex, true);
                        continue;
                    }
                }
            }
            SR.Close();
        }
        public void Save(object sender, EventArgs e)
        {
            System.IO.StreamWriter SW = new System.IO.StreamWriter(Loki.Bot.CharacterSettings.SettingsPath + "\\Allrounder\\" + LokiPoe.Me.Name + ".cfg");
            //SaveSkills
            foreach (Control _control in Charactersettings.Controls)
            {
                if (_control.Name.ToLower().Contains("box") && !_control.Text.Equals("") && !_control.Name.Equals("SkilltypeBox"))
                    SW.WriteLine("{0} = {1}", _control.Name.Replace("Box", ""), _control.Text);
            }
            foreach (TabPage page in this.Skillbody.TabPages)
            {
                SW.WriteLine("Name = {0}", page.Text);
                foreach (Control _control in page.Controls)
                {
                    if (_control.Name.ToLower().Contains("box") && !_control.Text.Equals("") && !_control.Name.Equals("SkilltypeBox"))
                        SW.WriteLine("{0} = {1}", _control.Name.Replace("Box", ""), _control.Text);
                    if (_control.Name.Equals("SkilltypeBox"))
                    {
                        foreach (object item in (_control as CheckedListBox).CheckedItems)
                        {
                            SW.WriteLine("{0} = {1}", item, true);
                        }
                    }
                }
                SW.WriteLine("CastEnd");
            }
            SW.Close();
            if(BotMain.IsRunning)
            {
                BotMain.Stop("Allrounder(): Refreshing Skilllist");
                BotMain.Start();
            }
        }
        private void RemoveCurrentSkillButton_Click(object sender, EventArgs e)
        {
            this.Skillbody.TabPages.Remove(this.Skillbody.SelectedTab);
        }
        public void NewSkillButton_Click(object sender, EventArgs e)
        {
            this.Skillbody.TabPages.Add(new Attack(this.NewSkillName.Text));
        }
        public void MoveSkills(object sender, EventArgs e)
        {
            int position = this.Skillbody.TabPages.IndexOf(this.Skillbody.SelectedTab);
            if (position == 0)
                return;
            if ((sender as Button).Name.ToLower().Contains("left"))
            {
                TabPage Active = this.Skillbody.SelectedTab;
                TabPage Old = this.Skillbody.TabPages[position - 1];
                this.Skillbody.TabPages[position - 1] = Active;
                this.Skillbody.TabPages[position] = Old;
                this.Skillbody.SelectedTab = this.Skillbody.TabPages[position - 1];
                this.Update();
                return;
            }
            if ((sender as Button).Name.ToLower().Contains("right"))
            {
                if (position + 1 >= this.Skillbody.TabPages.Count)
                    return;
                TabPage Active = this.Skillbody.SelectedTab;
                TabPage Old = this.Skillbody.TabPages[position + 1];
                this.Skillbody.TabPages[position + 1] = Active;
                this.Skillbody.TabPages[position] = Old;
                this.Skillbody.SelectedTab = this.Skillbody.TabPages[position + 1];
                this.Update();
                return;
            }
        }
    }
    partial class Attack : System.Windows.Forms.TabPage
    {
        // 
        // DefaultAttack
        // 
        private System.Windows.Forms.ComboBox Mobsarround_TargetBox;
        private System.Windows.Forms.Label MobCheckTagetLabel;
        private System.Windows.Forms.Label MobcheckLabel;
        private System.Windows.Forms.Label CountLabel;
        private System.Windows.Forms.Label DistancesLabel;
        private System.Windows.Forms.Label LifeManaSpecificLabel;
        private System.Windows.Forms.TextBox Mobsarround_CountBox;
        private System.Windows.Forms.Label MobCheckCountLabel;
        private System.Windows.Forms.TextBox Mobsarround_DistanceBox;
        private System.Windows.Forms.Label MobCheckDistanceLabel;
        private System.Windows.Forms.TextBox MaxCountBox;
        private System.Windows.Forms.Label MaxCountLabel;
        private System.Windows.Forms.TextBox EnemyDistanceBox;
        private System.Windows.Forms.Label EnemyDistanceLabel;
        private System.Windows.Forms.TextBox EnemyinDistanceBox;
        private System.Windows.Forms.Label EnemyInDistanceLabel;
        private System.Windows.Forms.TextBox MinEnemyLifePercentBox;
        private System.Windows.Forms.Label MinEnemyLifeLabel;
        private System.Windows.Forms.TextBox MinLifePercentBox;
        private System.Windows.Forms.Label MinLifeLabel;
        private System.Windows.Forms.TextBox MinManaPercentBox;
        private System.Windows.Forms.Label MinManaLabel;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button Savebutton;
        private System.Windows.Forms.CheckedListBox SkilltypeBox;
        private System.Windows.Forms.Label SkilltypeLabel;
        public Attack(string _Title)
        {

            this.SkilltypeBox = new System.Windows.Forms.CheckedListBox();
            this.SkilltypeLabel = new System.Windows.Forms.Label();
            this.Mobsarround_TargetBox = new System.Windows.Forms.ComboBox();
            this.MobCheckTagetLabel = new System.Windows.Forms.Label();
            this.MobcheckLabel = new System.Windows.Forms.Label();
            this.CountLabel = new System.Windows.Forms.Label();
            this.DistancesLabel = new System.Windows.Forms.Label();
            this.LifeManaSpecificLabel = new System.Windows.Forms.Label();
            this.Mobsarround_CountBox = new System.Windows.Forms.TextBox();
            this.MobCheckCountLabel = new System.Windows.Forms.Label();
            this.Mobsarround_DistanceBox = new System.Windows.Forms.TextBox();
            this.MobCheckDistanceLabel = new System.Windows.Forms.Label();
            this.MaxCountBox = new System.Windows.Forms.TextBox();
            this.MaxCountLabel = new System.Windows.Forms.Label();
            this.EnemyDistanceBox = new System.Windows.Forms.TextBox();
            this.EnemyDistanceLabel = new System.Windows.Forms.Label();
            this.EnemyinDistanceBox = new System.Windows.Forms.TextBox();
            this.EnemyInDistanceLabel = new System.Windows.Forms.Label();
            this.MinEnemyLifePercentBox = new System.Windows.Forms.TextBox();
            this.MinEnemyLifeLabel = new System.Windows.Forms.Label();
            this.MinLifePercentBox = new System.Windows.Forms.TextBox();
            this.MinLifeLabel = new System.Windows.Forms.Label();
            this.MinManaPercentBox = new System.Windows.Forms.TextBox();
            this.MinManaLabel = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.Savebutton = new System.Windows.Forms.Button();

            this.Controls.Add(this.SkilltypeBox);
            this.Controls.Add(this.SkilltypeLabel);
            this.Controls.Add(this.Mobsarround_TargetBox);
            this.Controls.Add(this.MobCheckTagetLabel);
            this.Controls.Add(this.MobcheckLabel);
            this.Controls.Add(this.CountLabel);
            this.Controls.Add(this.DistancesLabel);
            this.Controls.Add(this.LifeManaSpecificLabel);
            this.Controls.Add(this.Mobsarround_CountBox);
            this.Controls.Add(this.MobCheckCountLabel);
            this.Controls.Add(this.Mobsarround_DistanceBox);
            this.Controls.Add(this.MobCheckDistanceLabel);
            this.Controls.Add(this.MaxCountBox);
            this.Controls.Add(this.MaxCountLabel);
            this.Controls.Add(this.EnemyDistanceBox);
            this.Controls.Add(this.EnemyDistanceLabel);
            this.Controls.Add(this.EnemyinDistanceBox);
            this.Controls.Add(this.EnemyInDistanceLabel);
            this.Controls.Add(this.MinEnemyLifePercentBox);
            this.Controls.Add(this.MinEnemyLifeLabel);
            this.Controls.Add(this.MinLifePercentBox);
            this.Controls.Add(this.MinLifeLabel);
            this.Controls.Add(this.MinManaPercentBox);
            this.Controls.Add(this.MinManaLabel);

            // 
            // SkilltypeBox
            // 
            this.SkilltypeBox.FormattingEnabled = true;
            this.SkilltypeBox.Items.AddRange(new object[] {
            "IsTrap",
            "IsSummon",
            "IsCurse",
            "IsTotem",
            "IsRanged"});
            this.SkilltypeBox.Location = new System.Drawing.Point(271, 167);
            this.SkilltypeBox.Name = "SkilltypeBox";
            this.SkilltypeBox.Size = new System.Drawing.Size(215, 79);
            this.SkilltypeBox.TabIndex = 24;
            // 
            // SkilltypeLabel
            // 
            this.SkilltypeLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.SkilltypeLabel.Location = new System.Drawing.Point(268, 146);
            this.SkilltypeLabel.Name = "SkilltypeLabel";
            this.SkilltypeLabel.Size = new System.Drawing.Size(218, 18);
            this.SkilltypeLabel.TabIndex = 23;
            this.SkilltypeLabel.Text = "Skilltype";
            this.SkilltypeLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // Mobsarround_TargetBox
            // 
            this.Mobsarround_TargetBox.AutoCompleteCustomSource.AddRange(new string[] {
            "Me",
            "Maintarget"});
            this.Mobsarround_TargetBox.FormattingEnabled = true;
            this.Mobsarround_TargetBox.Items.AddRange(new object[] {
            "Me",
            "Maintarget"});
            this.Mobsarround_TargetBox.Location = new System.Drawing.Point(386, 92);
            this.Mobsarround_TargetBox.Name = "Mobsarround_TargetBox";
            this.Mobsarround_TargetBox.Size = new System.Drawing.Size(100, 21);
            this.Mobsarround_TargetBox.TabIndex = 22;
            // 
            // MobCheckTagetLabel
            // 
            this.MobCheckTagetLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.MobCheckTagetLabel.Location = new System.Drawing.Point(268, 92);
            this.MobCheckTagetLabel.Name = "MobCheckTagetLabel";
            this.MobCheckTagetLabel.Size = new System.Drawing.Size(112, 20);
            this.MobCheckTagetLabel.TabIndex = 20;
            this.MobCheckTagetLabel.Text = "Target";
            // 
            // MobcheckLabel
            // 
            this.MobcheckLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.MobcheckLabel.Location = new System.Drawing.Point(268, 21);
            this.MobcheckLabel.Name = "MobcheckLabel";
            this.MobcheckLabel.Size = new System.Drawing.Size(218, 18);
            this.MobcheckLabel.TabIndex = 19;
            this.MobcheckLabel.Text = "Mobcheck";
            this.MobcheckLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // CountLabel
            // 
            this.CountLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.CountLabel.Location = new System.Drawing.Point(6, 232);
            this.CountLabel.Name = "CountLabel";
            this.CountLabel.Size = new System.Drawing.Size(193, 18);
            this.CountLabel.TabIndex = 18;
            this.CountLabel.Text = "Count";
            this.CountLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // DistancesLabel
            // 
            this.DistancesLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.DistancesLabel.Location = new System.Drawing.Point(6, 146);
            this.DistancesLabel.Name = "DistancesLabel";
            this.DistancesLabel.Size = new System.Drawing.Size(193, 18);
            this.DistancesLabel.TabIndex = 17;
            this.DistancesLabel.Text = "Distances";
            this.DistancesLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // LifeManaSpecificLabel
            // 
            this.LifeManaSpecificLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.LifeManaSpecificLabel.Location = new System.Drawing.Point(6, 16);
            this.LifeManaSpecificLabel.Name = "LifeManaSpecificLabel";
            this.LifeManaSpecificLabel.Size = new System.Drawing.Size(193, 23);
            this.LifeManaSpecificLabel.TabIndex = 16;
            this.LifeManaSpecificLabel.Text = "Life/ManaSpecific";
            this.LifeManaSpecificLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // Mobsarround_CountBox
            // 
            this.Mobsarround_CountBox.Location = new System.Drawing.Point(436, 66);
            this.Mobsarround_CountBox.Name = "Mobsarround_CountBox";
            this.Mobsarround_CountBox.Size = new System.Drawing.Size(50, 20);
            this.Mobsarround_CountBox.TabIndex = 15;
            // 
            // MobCheckCountLabel
            // 
            this.MobCheckCountLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.MobCheckCountLabel.Location = new System.Drawing.Point(268, 66);
            this.MobCheckCountLabel.Name = "MobCheckCountLabel";
            this.MobCheckCountLabel.Size = new System.Drawing.Size(162, 20);
            this.MobCheckCountLabel.TabIndex = 14;
            this.MobCheckCountLabel.Text = "Count";
            // 
            // Mobsarround_DistanceBox
            // 
            this.Mobsarround_DistanceBox.Location = new System.Drawing.Point(436, 40);
            this.Mobsarround_DistanceBox.Name = "Mobsarround_DistanceBox";
            this.Mobsarround_DistanceBox.Size = new System.Drawing.Size(50, 20);
            this.Mobsarround_DistanceBox.TabIndex = 13;
            // 
            // MobCheckDistanceLabel
            // 
            this.MobCheckDistanceLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.MobCheckDistanceLabel.Location = new System.Drawing.Point(268, 40);
            this.MobCheckDistanceLabel.Name = "MobCheckDistanceLabel";
            this.MobCheckDistanceLabel.Size = new System.Drawing.Size(162, 20);
            this.MobCheckDistanceLabel.TabIndex = 12;
            this.MobCheckDistanceLabel.Text = "Distance";
            // 
            // MaxCountBox
            // 
            this.MaxCountBox.Location = new System.Drawing.Point(149, 253);
            this.MaxCountBox.Name = "MaxCountBox";
            this.MaxCountBox.Size = new System.Drawing.Size(50, 20);
            this.MaxCountBox.TabIndex = 11;
            // 
            // MaxCountLabel
            // 
            this.MaxCountLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.MaxCountLabel.Location = new System.Drawing.Point(6, 253);
            this.MaxCountLabel.Name = "MaxCountLabel";
            this.MaxCountLabel.Size = new System.Drawing.Size(137, 20);
            this.MaxCountLabel.TabIndex = 10;
            this.MaxCountLabel.Text = "MaxCount";
            // 
            // EnemyDistanceBox
            // 
            this.EnemyDistanceBox.Location = new System.Drawing.Point(149, 193);
            this.EnemyDistanceBox.Name = "EnemyDistanceBox";
            this.EnemyDistanceBox.Size = new System.Drawing.Size(50, 20);
            this.EnemyDistanceBox.TabIndex = 9;
            // 
            // EnemyDistanceLabel
            // 
            this.EnemyDistanceLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.EnemyDistanceLabel.Location = new System.Drawing.Point(6, 193);
            this.EnemyDistanceLabel.Name = "EnemyDistanceLabel";
            this.EnemyDistanceLabel.Size = new System.Drawing.Size(137, 20);
            this.EnemyDistanceLabel.TabIndex = 8;
            this.EnemyDistanceLabel.Text = "EnemyDistance";
            // 
            // EnemyinDistanceBox
            // 
            this.EnemyinDistanceBox.Location = new System.Drawing.Point(149, 167);
            this.EnemyinDistanceBox.Name = "EnemyinDistanceBox";
            this.EnemyinDistanceBox.Size = new System.Drawing.Size(50, 20);
            this.EnemyinDistanceBox.TabIndex = 7;
            // 
            // EnemyInDistanceLabel
            // 
            this.EnemyInDistanceLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.EnemyInDistanceLabel.Location = new System.Drawing.Point(6, 167);
            this.EnemyInDistanceLabel.Name = "EnemyInDistanceLabel";
            this.EnemyInDistanceLabel.Size = new System.Drawing.Size(137, 20);
            this.EnemyInDistanceLabel.TabIndex = 6;
            this.EnemyInDistanceLabel.Text = "EnemyInDistance";
            // 
            // MinEnemyLifePercentBox
            // 
            this.MinEnemyLifePercentBox.Location = new System.Drawing.Point(149, 91);
            this.MinEnemyLifePercentBox.Name = "MinEnemyLifePercentBox";
            this.MinEnemyLifePercentBox.Size = new System.Drawing.Size(50, 20);
            this.MinEnemyLifePercentBox.TabIndex = 5;
            // 
            // MinEnemyLifeLabel
            // 
            this.MinEnemyLifeLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.MinEnemyLifeLabel.Location = new System.Drawing.Point(6, 91);
            this.MinEnemyLifeLabel.Name = "MinEnemyLifeLabel";
            this.MinEnemyLifeLabel.Size = new System.Drawing.Size(137, 20);
            this.MinEnemyLifeLabel.TabIndex = 4;
            this.MinEnemyLifeLabel.Text = "MinEnemyLife%";
            // 
            // MinLifePercentBox
            // 
            this.MinLifePercentBox.Location = new System.Drawing.Point(149, 65);
            this.MinLifePercentBox.Name = "MinLifePercentBox";
            this.MinLifePercentBox.Size = new System.Drawing.Size(50, 20);
            this.MinLifePercentBox.TabIndex = 3;
            // 
            // MinLifeLabel
            // 
            this.MinLifeLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.MinLifeLabel.Location = new System.Drawing.Point(6, 65);
            this.MinLifeLabel.Name = "MinLifeLabel";
            this.MinLifeLabel.Size = new System.Drawing.Size(137, 20);
            this.MinLifeLabel.TabIndex = 2;
            this.MinLifeLabel.Text = "MinLife%";
            // 
            // MinManaPercentBox
            // 
            this.MinManaPercentBox.Location = new System.Drawing.Point(149, 39);
            this.MinManaPercentBox.Name = "MinManaPercentBox";
            this.MinManaPercentBox.Size = new System.Drawing.Size(50, 20);
            this.MinManaPercentBox.TabIndex = 1;
            // 
            // MinManaLabel
            // 
            this.MinManaLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.MinManaLabel.Location = new System.Drawing.Point(6, 39);
            this.MinManaLabel.Name = "MinManaLabel";
            this.MinManaLabel.Size = new System.Drawing.Size(137, 20);
            this.MinManaLabel.TabIndex = 0;
            this.MinManaLabel.Text = "MinMana%";

            this.Location = new System.Drawing.Point(4, 22);
            this.Name = _Title;
            this.Padding = new System.Windows.Forms.Padding(3);
            this.Size = new System.Drawing.Size(515, 367);
            this.TabIndex = 0;
            this.Text = _Title;
            this.UseVisualStyleBackColor = true;
        }
    }
}