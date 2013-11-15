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

            return new PrioritySelector(
                new Sequence(
                SpellManager.CreateSpellCastComposite(spell, reqs, ret => Variables.MainTarget),
                //new WaitContinue(TimeSpan.FromMilliseconds(300), ret => false, new Action(delegate { return RunStatus.Success; })),
                new Action(delegate { Variables.Log.DebugFormat("Allrounder(Cast): Casted {0}", spell); Variables.SkillList.FirstOrDefault(s => s.Name.Equals(spell)).Cooldown.Reset(); return RunStatus.Failure; })));
        }
        public static Composite Cast(string spell, SpellManager.GetSelection<Vector2i> location, SpellManager.GetSelection<bool> reqs = null)
        {
            // Note: this is safe to do. If we pass null to the requirements check, that means we always want to fire
            // as long as CanCast is true.
            if (reqs == null)
            {
                reqs = ret => true;
            }

            return new PrioritySelector(
                new Sequence(
                SpellManager.CreateSpellCastComposite(spell, reqs, location),
                new Action(delegate { Variables.Log.DebugFormat("Allrounder(Cast): Casted {0}", spell); Variables.SkillList.FirstOrDefault(s => s.Name.Equals(spell)).Cooldown.Reset(); return RunStatus.Failure; })));
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
                    if (line.Split('=')[0].Trim().Equals("MinEnemyLifePercent"))
                        Variables.SkillList[Skillnumber].MinEnemyLifePercent = int.Parse(line.Split('=')[1].Trim());
                    if (line.Split('=')[0].Trim().Equals("MobsAroundDistance"))
                        Variables.SkillList[Skillnumber].Mobsarround_Distance = int.Parse(line.Split('=')[1].Trim());
                    if (line.Split('=')[0].Trim().Equals("MobsAroundCount"))
                        Variables.SkillList[Skillnumber].Mobsarround_Count = int.Parse(line.Split('=')[1].Trim());
                    if (line.Split('=')[0].Trim().Equals("MobsAroundTarget"))
                    {
                        if (line.Split('=')[1].Trim().ToLower().Equals("me"))
                        {
                            Variables.SkillList[Skillnumber].Mobsarround_Target = 1;
                            continue;
                        }
                        if (line.Split('=')[1].Trim().ToLower().Equals("maintarget"))
                        {
                            Variables.SkillList[Skillnumber].Mobsarround_Target = 0;
                            continue;
                        }
                        if (line.Split('=')[1].Trim().Equals("1"))
                            Variables.SkillList[Skillnumber].Mobsarround_Target = 1;
                        if (line.Split('=')[1].Trim().Equals("0"))
                            Variables.SkillList[Skillnumber].Mobsarround_Target = 0;
                    }
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
                    if (line.Split('=')[0].Trim().Equals("GeneratesCharges"))
                        Variables.SkillList[Skillnumber].GeneratesCharges = Boolean.Parse(line.Split('=')[1].Trim());
                    if (line.Split('=')[0].Trim().Equals("ChargeType"))
                        Variables.SkillList[Skillnumber].ChargeType = line.Split('=')[1].Trim();
                    if (line.Split('=')[0].Trim().Equals("KeepChargesUp"))
                        Variables.SkillList[Skillnumber].KeepChargesUp = Boolean.Parse(line.Split('=')[1].Trim());
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
            return LokiPoe.EntityManager.OfType<Actor>().Count(a => !a.IsDead && !a.IsFriendly && a.Distance <= distance) >= count;
        }
        public static bool HasAura(Actor actor, string auraName, int minCharges = -1, double minSecondsLeft = -1)
        {
            Aura aura = actor.Auras.FirstOrDefault(a => a.Name == auraName || a.InternalName == auraName);
            if (aura == null)
            {
                Logger.GetLoggerInstanceForType().Debug("Aura == null");
                return false;
            }
            Logger.GetLoggerInstanceForType().DebugFormat("{0}: charges: {1} TimeLeft: {2}", aura.Name, aura.Charges, aura.TimeLeft);
            if (minCharges != -1)
            {
                if (aura.Charges != minCharges)
                {
                    return false;
                }
            }
            if (minSecondsLeft != -1)
            {
                if (aura.TimeLeft.TotalSeconds < minSecondsLeft)
                {
                    return false;
                }
            }
            return true;
        }
        public static TimeSpan GetSpellCooldown(string _Spellname)
        {
            foreach (Spell _spell in LokiPoe.Me.AvailableSpells)
            {
                if (_spell.Name != null && !_spell.Name.Equals("") && _spell.Name.Equals(_Spellname))
                {
                    Variables.Log.Debug(_spell.UsesAvailable);
                    return _spell.Cooldown;
                }
            }
            return new TimeSpan();
        }
        public static Spell GetSpell(string _Spellname)
        {
            foreach (Spell _spell in LokiPoe.Me.AvailableSpells)
            {
                if (_spell.Name != null && !_spell.Name.Equals("") && _spell.Name.Equals(_Spellname))
                {
                    return _spell;
                }
            }
            return null;
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
        public string ChargeType { get; set; }
        
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
        public bool GeneratesCharges { get; set; }
        public bool KeepChargesUp { get; set; }

        public WaitTimer Cooldown;
        public Spell SpellPtr { get { return Functions.GetSpell(this.Name); } }
        public Skill(string _name)
        {
            this.Name = _name;
            this.Cooldown = new WaitTimer(new TimeSpan(Functions.GetSpellCooldown(this.Name).Ticks));
            //this.SpellPtr = Functions.GetSpell(this.Name);
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
        public bool ShouldgenerateCharges()
        {
            if(this.Name.Equals("Viper Strike"))
            {
                Aura ChargesAura = Variables.MainTarget.Auras.FirstOrDefault(aura => aura.InternalName.Equals("viper_strike_orb"));
                if (ChargesAura == null)
                    return true;
                if (ChargesAura.Charges != this.MaxCount)
                    return true;
                if (ChargesAura.TimeLeft.TotalSeconds <= 3 && KeepChargesUp)
                    return true;
            }
            if (this.ChargeType.Equals("Frenzy") || this.ChargeType.Equals("Power") || this.ChargeType.Equals("Endurance"))
            {
                Aura ChargesAura = LokiPoe.Me.Auras.FirstOrDefault(aura => aura.Name.Equals(this.ChargeType + " Charges") || aura.InternalName.Equals(this.ChargeType.ToLower() + "_charge"));
                if (ChargesAura == null)
                {
                    return true;
                }
                if (ChargesAura.Charges != this.MaxCount)
                    return true;
                if (ChargesAura.TimeLeft.TotalSeconds <= 3 && KeepChargesUp)
                    return true;
            }
            return false;
        }
        public bool CanCast()
        {
            Variables.Log.Debug("CanCast(" + this.Name + ") Check for Cast");
            if(this.SpellPtr.UsesAvailable <= 0)
                return false;
            if(!this.Cooldown.IsFinished)
            {
                Variables.Log.DebugFormat("This Skill is on Cooldown. Timeleft {0}", this.Cooldown.TimeLeft);
            }
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
            if (this.MaxCount != 0 && !this.IsTrap && !this.IsSummon && !this.IsTotem && !this.GeneratesCharges)
                Truechecks++;
            //Bools
            if (this.GeneratesCharges)
                Truechecks++;
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
            if (this.GeneratesCharges && ShouldgenerateCharges())
                Trues++;
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
                if (!this.IsTrap && !this.IsSummon && !this.IsTotem && !this.GeneratesCharges && CurrentCount != MaxCount)
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
        public override void OnGuiButtonPress() { new AllrounderSettings().ShowDialog(); }
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