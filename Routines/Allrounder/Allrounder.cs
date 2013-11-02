using System;
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

using Action = Loki.TreeSharp.Action;

namespace Allrounder
{

    public class Helpers
    {
        public static readonly ILog Log = Logger.GetLoggerInstanceForType();
        public static Player Me { get { return LokiPoe.Me; } }
        public static Monster MainTarget { get { return Loki.Bot.Targeting.Combat.Targets.FirstOrDefault() as Monster; } }
        public static readonly WaitTimer _flaskCd = new WaitTimer(TimeSpan.FromSeconds(0.5));
        #region GetFlasks
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
                       where flask != null && item.Name == "Quicksilver Flask" && flask.CanUse
                       select item;
            }
        }
        #endregion
        public static Composite Cast(string spell, SpellManager.GetSelection<bool> reqs = null)
        {
            // Note: this is safe to do. If we pass null to the requirements check, that means we always want to fire
            // as long as CanCast is true.
            if (reqs == null)
            {
                reqs = ret => true;
            }

            return SpellManager.CreateSpellCastComposite(spell, reqs, ret => MainTarget);
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
            return new Decorator(ret => !LokiPoe.MeleeLineOfSight.CanSee(LokiPoe.Me.Position, MainTarget.Position),
                CommonBehaviors.MoveTo(ret => MainTarget.Position, ret => "CreateMoveToLos"));
        }
        /// <summary>
        /// Move into given range for combat
        /// </summary>
        /// <param name="range">range for combat</param>
        /// <returns></returns>
        public static Composite CreateMoveToRange(int range)
        {
            return new Decorator(ret => MainTarget.Distance > range,
                CommonBehaviors.MoveTo(ret => MainTarget.Position, ret => "CreateMoveToRange"));
        }
        /// <summary>
        ///     Returns whether or not the specified count of mobs are near the specified monster, within the defined range.
        /// </summary>
        /// <param name="monster"></param>
        /// <param name="distance"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public static bool NumberOfMobsNear(PoEObject Target, float distance, int count)
        {
            if (Target == null)
            {
                return false;
            }

            Vector2i mpos = Target.Position;

            int curCount = 0;
            foreach (PoEObject mob in Targeting.Combat.Targets)
            {
                if (mob.ID == Target.ID)
                {
                    continue;
                }

                if (mob.Position.Distance(mpos) < distance)
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
        /// <summary>
        ///     Returns the First Corpse found by the Entitiymanager in given Distance
        /// </summary>
        /// <param name="distance"></param>
        /// <returns>Corpse</returns>
        public static Actor GetCorpseNear(int distance)
        {
            return LokiPoe.EntityManager.OfType<Actor>().FirstOrDefault(corpse => corpse.IsValid && corpse.IsDead && corpse.Name != "Raised Zombie" && !corpse.Type.Contains("trap") && corpse.Distance <= distance);
        }
        /// <summary>
        /// returns the number of corpses near player in given distance
        /// </summary>
        /// <param name="distance">max distance to look for corpses</param>
        /// <returns>corpsescount</returns>
        public static int GetCorpsesNear(int distance)
        {
            return LokiPoe.EntityManager.OfType<Actor>().Count(corpse => corpse.IsValid && corpse.IsDead && corpse.Name != "Raised Zombie" && !corpse.Type.Contains("trap") && corpse.Distance <= distance);
        }
        /// <summary>
        /// Check if a Object has got a spell
        /// </summary>
        /// <param name="_obj">Object/Actor</param>
        /// <param name="_spellname">Spellname</param>
        /// <returns>true/false</returns>
        public static bool ObjectHasSpell(PoEObject _obj, string _spellname)
        {
            foreach(Spell _spell in _obj.Components.ActorComponent.AvailableSpells)
            {
                if(_spell.IsValid)
                    if (_spell.Name != null && _spell.Name.Equals(_spellname))
                        return true;
            }
            return false;
        }
    }

    public class Attack
    {
        public string Name = "undefined";
        int MinManaPercent = 0;
        int MinLifePercent = 0;
        int MinEnemyLifePercent = 0;
        int Mobsarround_Distance = 0;
        int Mobsarround_Count = 0;
        int Mobsarround_Target = 0; // 0 -> Main Target / 1 -> Me
        int EnemyDistance = 0;
        int MaxCount = 0;

        bool CheckForMobsarround = false;
        bool OnlyBosses = false;
        bool IsTrap = false;
        bool IsSummon = false;
        bool IsCurse = false;
        /// <summary>
        /// Checks if the current raising skill should be casted
        /// </summary>
        /// <returns></returns>
        public bool ShouldRaiseMinions()
        {
            if (this.Name.Equals("Raise Zombie") && LokiPoe.EntityManager.OfType<Actor>().Count(zombies => zombies.IsValid && !zombies.IsDead && zombies.Reaction == Reaction.Friendly && zombies.Name == "Raised Zombie") < MaxCount && Helpers.GetCorpsesNear(30) > 2)
            {
                return true;
            }

            if (this.Name.Equals("Raise Spectre") && LokiPoe.EntityManager.OfType<Actor>().Count(spectre => spectre.IsValid && !spectre.IsDead && spectre.Reaction == Reaction.Friendly && spectre.HasAura("Spectral")) < MaxCount && Helpers.GetCorpsesNear(30) > 2)
            {
                return true;
            }
            return false;
        }
        /// <summary>
        /// Checks if the current Traptype should be thrown
        /// </summary>
        /// <returns></returns>
        public bool ShouldThrowTrap()
        {
            if(this.Name.Contains("Trap"))
            {
                if (LokiPoe.EntityManager.OfType<Actor>().Count(trap => trap.IsValid && !trap.IsDead && Helpers.ObjectHasSpell(trap as PoEObject, this.Name)) < MaxCount)
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
            if (!Helpers.MainTarget.HasAura(this.Name))
                return true;
            return false;
        }
        /// <summary>
        /// Check if the currentattack could be casted/used
        /// </summary>
        /// <returns></returns>
        public bool CanCast()
        {
            int checks = 0;
            int trues = 0;
            if (MinManaPercent != 0)
                checks++;
            if (MinLifePercent != 0)
                checks++;
            if (MinEnemyLifePercent != 0)
                checks++;
            if (EnemyDistance != 0)
                checks++;
            if (CheckForMobsarround)
                checks++;
            if (OnlyBosses)
                checks++;
            if (IsTrap)
                checks++;
            if (IsSummon)
                checks++;
            if (IsCurse)
                checks++;
            if (checks == 0)
                return true;

            if (MinEnemyLifePercent != 0 && Helpers.MainTarget.HealthPercent <= MinEnemyLifePercent)
                trues++;
            if (Helpers.Me.ManaPercent >= MinManaPercent && MinManaPercent != 0)
                trues++;
            if (Helpers.Me.HealthPercent >= MinLifePercent && MinLifePercent != 0)
                trues++;
            if (Helpers.MainTarget.Distance <= EnemyDistance && EnemyDistance != 0)
                trues++;
            if (Mobsarround_Target == 1)
            {
                if (Helpers.NumberOfMobsNear(LokiPoe.Me, Mobsarround_Distance, Mobsarround_Count) && CheckForMobsarround)
                    trues++;
            }
            if (Mobsarround_Target == 0)
            {
                if (Helpers.NumberOfMobsNear(Helpers.MainTarget, Mobsarround_Distance, Mobsarround_Count) && CheckForMobsarround)
                    trues++;
            }
            if (IsCurse && ShouldCastCurse())
                trues++;
            if (IsSummon && ShouldRaiseMinions())
                trues++;
            if (IsTrap && ShouldThrowTrap())
                trues++;
            if (Helpers.MainTarget.Rarity >= Rarity.Rare && OnlyBosses)
                trues++;

            if (IsTrap && ShouldThrowTrap())
                trues++;

            if (trues >= checks)
            {
                return true;
            }
            return false;
        }
        /// <summary>
        /// Create a new AttackSkill with given params
        /// </summary>
        /// <param name="strName">Skillname</param>
        /// <param name="iMinManaPercent">Minimal Mana Percent for using this Skill</param>
        /// <param name="iLifePercent">Minimal Life Percent for using this Skill</param>
        /// <param name="iMobsarround_Distance">Check for Mobsarround DISTANCE</param>
        /// <param name="iMobsarround_Count">Minimal Mobs for Mobsarroundcheck for true</param>
        /// <param name="iMobsarround_Target">Desired Target 0-> MainTarget / 1-> Me</param>
        /// <param name="iEnemyDistance">Minimal Distance for using this Skill</param>
        /// <param name="iMaxCount">Maximal Count of Minions/Traps/Totems for this Skill</param>
        /// <param name="bCheckForMobsarround">Check for Mobs arround</param>
        /// <param name="bOnlyBosses">Use this Skill only on Bosses</param>
        /// <param name="bIsTrap">Is Skill a Trap</param>
        /// <param name="bIsSummon">Is Skill a Summon</param>
        /// <param name="bIsCurse">Is Skill a Curse</param>
        public Attack(string strName, int iMinManaPercent = 0, int iLifePercent = 0, int iMobsarround_Distance = 0, int iMobsarround_Count = 0, int iMobsarround_Target = 0, int iEnemyDistance = 0, int iMaxCount = 0, int iEnemyHPPercent = 0, bool bCheckForMobsarround = false, bool bOnlyBosses = false, bool bIsTrap = false, bool bIsSummon = false, bool bIsCurse = false)
        {
            this.Name = strName;
            this.MinManaPercent = iMinManaPercent;
            this.Mobsarround_Distance = iMobsarround_Distance;
            this.Mobsarround_Count = iMobsarround_Count;
            this.Mobsarround_Target = iMobsarround_Target; // 0 -> Main Target / 1 -> Me
            this.EnemyDistance = iEnemyDistance;
            this.MaxCount = iMaxCount;
            this.MinEnemyLifePercent = iEnemyHPPercent;

            this.CheckForMobsarround = bCheckForMobsarround;
            this.OnlyBosses = bOnlyBosses;
            this.IsTrap = bIsTrap;
            this.IsSummon = bIsSummon;
            this.IsCurse = bIsCurse;

            Helpers.Log.Debug("CustomCR: " + this.Name + " Added to AttackList");
        }
        /// <summary>
        /// Reads the characterconfig for allrounder
        /// </summary>
        /// <param name="file">Filename default: charname.cfg</param>
        public static void ReadConfigFile(string file)
        {
            TextReader tr = new StreamReader(file);
            string line = null;
            string tmpname = "undefined";
            int manap = 0, lifep = 0, mad = 0, mac = 0, mat = 0, ed = 0, mc = 0, enemylife = 0;
            bool cfm = false, ob = false, it = false, _is = false, _ic = false;
            while((line = tr.ReadLine()) != null)
            {
                if(!line.Contains('#'))
                {
                    line = line.Replace("\"", "");
                    if (line.Split('=')[0].Trim().Equals("MinEnemyLifePercent"))
                        if (!line.Split('=')[1].Trim().Equals("0"))
                    if (line.Split('=')[0].Trim().Equals("FightDistance"))
                        if (!line.Split('=')[1].Trim().Equals("0"))
                            Allrounder.FightDistance = int.Parse(line.Split('=')[1].Trim());
                    if (line.Split('=')[0].Trim().Equals("PotHealth"))
                        if (!line.Split('=')[1].Trim().Equals("0"))
                            Allrounder.MinLifePercent = int.Parse(line.Split('=')[1].Trim());
                    if (line.Split('=')[0].Trim().Equals("PotMana"))
                        if(!line.Split('=')[1].Trim().Equals("0"))
                            Allrounder.MinManaPercent = int.Parse(line.Split('=')[1].Trim());
                    if (line.Split('=')[0].Trim().Equals("Name"))
                        tmpname = line.Split('=')[1].Trim();
                    if (line.Split('=')[0].Trim().Equals("MinManaPercent"))
                        manap = Convert.ToInt32(line.Split('=')[1].Trim());
                    if (line.Split('=')[0].Trim().Equals("MinLifePercent"))
                        lifep = Convert.ToInt32(line.Split('=')[1].Trim());
                    if (line.Split('=')[0].Trim().Equals("Mobsarround_Distance"))
                        mad = Convert.ToInt32(line.Split('=')[1].Trim());
                    if (line.Split('=')[0].Trim().Equals("Mobsarround_Count"))
                        mac = int.Parse(line.Split('=')[1].Trim());
                    if (line.Split('=')[0].Trim().Equals("Mobsarround_Target"))
                        mat = Convert.ToInt32(line.Split('=')[1].Trim());
                    if (line.Split('=')[0].Trim().Equals("EnemyDistance"))
                        ed = Convert.ToInt32(line.Split('=')[1].Trim());
                    if (line.Split('=')[0].Trim().Equals("MaxCount"))
                        mc = Convert.ToInt32(line.Split('=')[1].Trim());
                    if (line.Split('=')[0].Trim().Equals("CheckForMobsarround"))
                    {
                        if (line.Split('=')[1].Trim().Equals("true"))
                        {
                            cfm = true;
                        }
                        else
                        {
                            cfm = false;
                        }
                    }
                    if (line.Split('=')[0].Trim().Equals("OnlyBosses"))
                    {
                        if (line.Split('=')[1].Trim().Equals("true"))
                        {
                            ob = true;
                        }
                        else
                        {
                            ob = false;
                        }
                    }
                    if (line.Split('=')[0].Trim().Equals("IsTrap"))
                    {
                        if (line.Split('=')[1].Trim().Equals("true"))
                        {
                            it = true;
                        }
                        else
                        {
                            it = false;
                        }
                    }
                    if (line.Split('=')[0].Trim().Equals("IsSummon"))
                    {
                        if (line.Split('=')[1].Trim().Equals("true"))
                        {
                            _is = true;
                        }
                        else
                        {
                            _is = false;
                        }
                    }
                    if (line.Split('=')[0].Trim().Equals("IsCurse"))
                    {
                        if (line.Split('=')[1].Trim().Equals("true"))
                        {
                            _ic = true;
                        }
                        else
                        {
                            _ic = false;
                        }
                    }
                    if (line.Equals("CastEnd"))
                    {
                        if (!tmpname.Equals("undefined"))
                            Allrounder.SpellList.Add(new Attack(tmpname, manap, lifep, mad, mac, mat, ed, mc, enemylife, cfm, ob, it, _is, _ic));
                        tmpname = "undefined";
                        manap = 0; lifep = 0; mad = 0; mac = 0; mat = 0; ed = 0; mc = 0; enemylife = 0;
                        cfm = false; ob = false; it = false; _is = false; _ic = false;
                    }
                }
            }
            tr.Close();
        }
        /// <summary>
        /// Creates the characterconfigfile default:firsttimeuse
        /// </summary>
        /// <param name="file">Name of the characterfile default:charname.cfg</param>
        public static void CreateConfigFile(string file)
        {
            TextWriter tw = new StreamWriter(file);
            tw.WriteLine("#Basic Settings");
            tw.WriteLine("#In this area you could setup your potionusing");
            tw.WriteLine("#FightDistance = 0");
            tw.WriteLine("#PotHealth = 0");
            tw.WriteLine("#PotMana = 0");
            tw.WriteLine("#Use Quicksilver Flask = false // Work in Progress\n");

            tw.WriteLine("#AttackFields");
            tw.WriteLine("#Name = \"undefined\"");
            tw.WriteLine("#MinManaPercent = 0");
            tw.WriteLine("#MinLifePercent = 0");
            tw.WriteLine("#MinEnemyLifePercent = 0");
            tw.WriteLine("#Mobsarround_Distance = 0");
            tw.WriteLine("#Mobsarround_Count = 0");
            tw.WriteLine("#Mobsarround_Target = 0 // 0 -> Main Target / 1 -> Me");
            tw.WriteLine("#EnemyDistance = 0");
            tw.WriteLine("#MaxCount = 0 // For Raising/Trap Skills -> MaxCount of Minions/Traps");
            tw.WriteLine("#CheckForMobsarround = false");
            tw.WriteLine("#OnlyBosses = false");
            tw.WriteLine("#IsSummon = false");
            tw.WriteLine("#IsTrap = false");
            tw.WriteLine("#IsCurse = false");
            tw.WriteLine("After a Skill you must Set 'CastEnd' then the CR knows a new Skill begins\n");

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
    }
    [UsedImplicitly]
    public partial class Allrounder : CombatRoutine
    {
        #region Settings
        //Please only use 1 Setting -> Actually Percent works only
        bool _started = false;
        public static int MinLifePercent = 70;
        public static int MinManaPercent = 50;
        public static int FightDistance = 50;
        public static bool UseQuicksilverFlask = false;
        public int MinLife = 0;
        public int MinMana = 0;
        #endregion

        #region RoutineBasics
        public static List<Attack> SpellList = new List<Attack>();
        
        public override string Name { get { return "Allrounder by xTenshiSanx"; } }
        public override string ToString() { return Name; }
        public override void Dispose() { }
        public override void OnGuiButtonPress() { }
        public static PrioritySelector Fight;
        public override Composite Combat { get { return CombatBot(); } }
        public override Composite Buff { get { return BuffBot(); } }
        public override void Initialize()
        {
            Fight = new PrioritySelector();
            Loki.Bot.BotMain.OnStart += OnStart;
            BotMain.OnStop += OnStop;
            Log.Debug("Allrounder by xTenshiSanx has been loaded");
        }
        void OnStart(IBot bot)
        {
            if (!_started)
            {
                CheckForSettingsfile();
                foreach(Attack atk in SpellList)
                {
                    if (atk.Name.Contains("Raise"))
                        Fight.AddChild(Helpers.Cast(atk.Name, ret => Helpers.GetCorpseNear(30).Position, ret => atk.CanCast()));
                    else
                        Fight.AddChild(Helpers.Cast(atk.Name, ret => atk.CanCast()));
                }
                _started = true;
            }
        }
        void OnStop(IBot bot)
        {
            Fight = new PrioritySelector();
            SpellList = new List<Attack>();
            _started = false;
        }
        private void CheckForSettingsfile()
        {
            string Filename = Helpers.Me.Name + ".cfg";
            string Folder = Loki.Bot.GlobalSettings.SettingsPath + "\\Allrounder\\";
            if(File.Exists(Folder + Filename))
            {
                Log.Debug("File " + Folder + Filename + " Exists");
                Attack.ReadConfigFile(Folder + Filename);
            }
            else
            {
                Log.Debug("File " + Folder + Filename + " dosnt exists");
                Attack.CreateConfigFile(Folder + Filename);
                Log.Debug("Please Check your Character-Settingsfile in " + Folder);
                Log.Debug("And add your spells like in the example");
                BotMain.Stop("First time use");
            }
        }
        #endregion

        #region Combat
        private Composite FlaskBot()
        {
            return new PrioritySelector(
                new Decorator(ret => Helpers._flaskCd.IsFinished && Helpers.Me.HealthPercent < MinLifePercent && Helpers.LifeFlasks.Count() != 0 && !Helpers.Me.HasAura("flask_effect_life"),
                    new Action(ret =>
                    {
                        Helpers.LifeFlasks.First().Use();
                        Helpers._flaskCd.Reset();
                    })),
                new Decorator(ret => Helpers._flaskCd.IsFinished && Helpers.Me.ManaPercent < MinManaPercent && Helpers.ManaFlasks.Count() != 0 && !Helpers.Me.HasAura("flask_effect_mana"),
                    new Action(ret =>
                    {
                        Helpers.ManaFlasks.First().Use();
                        Helpers._flaskCd.Reset();
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
                Helpers.CreateMoveToLos(),
                Helpers.CreateMoveToRange(FightDistance),
                Allrounder.Fight
                );
        }
        #endregion
    }
}
