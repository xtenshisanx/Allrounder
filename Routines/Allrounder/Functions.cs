using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

using Loki;
using Loki.Bot;
using Loki.Bot.Logic;
using Loki.Bot.Logic.Behaviors;

using Loki.Game;
using Loki.Game.Objects;

using Loki.Utilities;
using Loki.TreeSharp;

using Action = Loki.TreeSharp.Action;
namespace Allrounder
{
    class Functions
    {
        #region Cast
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
                new Action(delegate { Allrounder.Log.DebugFormat("Allrounder(Cast): Casted {0}", spell); Variables.SkillList.FirstOrDefault(s => s.Name.Equals(spell)).Cooldown.Reset(); return RunStatus.Failure; })));
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
                new Action(delegate { Allrounder.Log.DebugFormat("Allrounder(Cast): Casted {0}", spell); Variables.SkillList.FirstOrDefault(s => s.Name.Equals(spell)).Cooldown.Reset(); return RunStatus.Failure; })));
        }
        #endregion
        #region CreateMoveToLos
        /// <summary>
        /// Move into LineofSight
        /// </summary>
        /// <returns></returns>
        public static Composite CreateMoveToLos()
        {
            return new Decorator(ret => !LokiPoe.MeleeLineOfSight.CanSee(LokiPoe.Me.Position, Variables.MainTarget.Position),
                CommonBehaviors.MoveTo(ret => Variables.MainTarget.Position, ret => "CreateMoveToLos"));
        }
        #endregion
        #region CreateMoveToRange
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
        #endregion
        #region GetCorpseNear
        public static Actor GetCorpseNear(int distance)
        {
            return LokiPoe.EntityManager.OfType<Actor>().FirstOrDefault(corpse => corpse.IsValid && corpse.IsDead && corpse.Name != "Raised Zombie" && !corpse.Type.Contains("trap") && corpse.Distance <= distance);
        }
        #endregion
        #region GetCorpsesNear
        public static int GetCorpsesNear(int distance)
        {
            return LokiPoe.EntityManager.OfType<Actor>().Count(corpse => /*corpse.IsValid && */corpse.IsDead && corpse.Name != "Raised Zombie" && !corpse.Type.Contains("trap") && corpse.Distance <= distance);
        }
        #endregion
        #region NumberOfEnemysNearMe
        public static int NumberOfEnemysNearMe(int distance)
        {
            return LokiPoe.EntityManager.OfType<Actor>().Count(enemy => enemy.IsValid && enemy.Reaction == Reaction.Enemy && enemy.Distance <= distance && !enemy.IsDead);
        }
        #endregion
        #region ReadConfig
        public static void ReadConfig(string file)
        {
            Variables.SkillList = new List<Skill>();
            TextReader tr = new StreamReader(file);
            Skill currentSkill = null;
            string line = null;
            string left = null;
            string right = null;

            while ((line = tr.ReadLine()) != null)
            {
                if (!line.Contains('#') && !line.Equals("CastEnd"))
                {
                    left = null;
                    right = null;
                    line = line.Replace("\"", "");
                    left = line.Split('=')[0].Trim();
                    right = line.Split('=')[1].Trim();
                    if (left.Equals("PotHealth"))
                    {
                        Settings.PotHealth = Int32.Parse(right);
                    }

                    if (left.Equals("PotMana"))
                    {
                        Settings.PotMana = Int32.Parse(right);
                    }

                    if (left.Equals("FightDistance"))
                    {
                        Settings.FightDistance = Int32.Parse(right);
                    }
                    if(left.Equals("Name"))
                    {
                        currentSkill = new Skill(right);
                        continue;
                    }
                    if(typeof(Skill).GetProperty(left) != null)
                    {
                        if(typeof(Skill).GetProperty(left).PropertyType.Name.Contains("Int32"))
                        {
                            if (typeof(Skill).GetProperty(left).Name.Equals("MobsAroundTarget"))
                            {
                                if (right.Equals("Me"))
                                {
                                    typeof(Skill).GetProperty(left).SetValue(currentSkill, 1);
                                }
                                if (right.Equals("Maintarget"))
                                {
                                    typeof(Skill).GetProperty(left).SetValue(currentSkill, 0);
                                }
                            }
                            else
                            {
                                typeof(Skill).GetProperty(left).SetValue(currentSkill, Int32.Parse(right));
                            }
                            continue;
                        }
                        if (typeof(Skill).GetProperty(left).PropertyType.Name.Contains("String"))
                        {
                            typeof(Skill).GetProperty(left).SetValue(currentSkill, right);
                            continue;
                        }
                        if (typeof(Skill).GetProperty(left).PropertyType.Name.Contains("Boolean"))
                        {
                            typeof(Skill).GetProperty(left).SetValue(currentSkill, Boolean.Parse(right));
                            continue;
                        }
                    }
                }
                if(line.Equals("CastEnd"))
                {
                    Variables.SkillList.Add(currentSkill);
                    currentSkill = null;
                }
            }
            tr.Close();
        }
        #endregion
        #region CheckForConfig
        public static void CheckForConfig()
        {
            string Filename = Variables.Me.Name + ".cfg";
            string Folder = Loki.Bot.GlobalSettings.SettingsPath + "\\Allrounder\\";
            if (File.Exists(Folder + Filename))
            {
                Allrounder.Log.Debug("File " + Folder + Filename + " Exists");
                ReadConfig(Folder + Filename);
            }
            else
            {
                Allrounder.Log.Debug("File " + Folder + Filename + " dosnt exists");
                File.Create(Filename);
                Allrounder.Log.Debug("Please Check your Character-Settingsfile in " + Folder);
                Allrounder.Log.Debug("And add your spells like in the example");
                BotMain.Stop("First time use");
            }
        }
        #endregion
        #region ObjectHasSpell
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
        #endregion
        #region NumberOfMobsNear
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
        #endregion
        #region NumberOfEnemysNear
        public static bool NumberOfEnemysNear(PoEObject Target, float distance, int count)
        {
            return LokiPoe.EntityManager.OfType<Actor>().Count(a => !a.IsDead && !a.IsFriendly && a.Distance <= distance) >= count;
        }
        #endregion
        #region HasAura
        public static bool HasAura(Actor actor, string auraName, int minCharges = -1, double minSecondsLeft = -1)
        {
            Aura aura = actor.Auras.FirstOrDefault(a => a.Name == auraName || a.InternalName == auraName);
            if (aura == null)
            {
                //Logger.GetLoggerInstanceForType().Debug("Aura == null");
                return false;
            }
            //Logger.GetLoggerInstanceForType().DebugFormat("{0}: charges: {1} TimeLeft: {2}", aura.Name, aura.Charges, aura.TimeLeft);
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
        #endregion
    }
}
