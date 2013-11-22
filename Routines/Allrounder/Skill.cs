using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Loki.Game;
using Loki.Game.Objects;
using Loki.TreeSharp;
using Loki.Utilities;

namespace Allrounder
{
    class Skill
    {
        #region Variables
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
        public int MobsAroundDistance { get; set; }
        public int MobsAroundCount { get; set; }
        public int MobsAroundTarget { get; set; }
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
        public Spell SpellPtr { get { return GetSpell(this.Name); } }
        #endregion
        #region Constructor
        public Skill(string _name)
        {
            this.Name = _name;
            this.Cooldown = new WaitTimer(new TimeSpan(GetSpellCooldown(this.Name).Ticks));
            //this.SpellPtr = Functions.GetSpell(this.Name);
            this.MinManaPercent = 0;
            this.MinLifePercent = 0;
            this.MinEnemyLifePercent = 0;
            this.MobsAroundCount = 0;
            this.MobsAroundDistance = 0;
            this.MobsAroundTarget = 0;
            this.EnemyinDistance = 0;
            this.EnemyDistance = 0;
            this.MaxCount = 0;
            this.CurrentCount = 0;

            this.OnlyMobWithoutShield = false;
            this.OnlyBosses = false;
            this.IsTrap = false;
            this.IsSummon = false;
            this.IsCurse = false;
            this.IsTotem = false;
            this.IsRanged = false;
        }
        #endregion

        #region GetSpellCooldown
        public static TimeSpan GetSpellCooldown(string _Spellname)
        {
            foreach (Spell _spell in LokiPoe.Me.AvailableSpells)
            {
                if (_spell.Name != null && !_spell.Name.Equals("") && _spell.Name.Equals(_Spellname))
                {
                    return _spell.Cooldown;
                }
            }
            return new TimeSpan();
        }
        #endregion
        #region GetSpell
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
        #endregion

        #region ShouldThrowTrap
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
        #endregion
        #region ShouldRaiseMinions
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
        #endregion
        #region ShouldRaiseTotem
        public bool ShouldRaiseTotem()
        {
            Vector2i targetpos = Variables.MainTarget.Position;
            if (LokiPoe.EntityManager.OfType<Actor>().Count(totem => totem.IsValid && !totem.IsDead && totem.Reaction == Reaction.Friendly && totem.Name.Equals("Totem") && Functions.ObjectHasSpell(totem, this.Name) && (Variables.MainTarget.Position.Distance(totem.Position) <= 30)) < MaxCount)
            {
                return true;
            }
            return false;
        }
        #endregion
        #region ShouldCastCurse
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
        #endregion
        #region ShouldGenerateCharges
        public bool ShouldgenerateCharges()
        {
            if (this.Name.Equals("Viper Strike"))
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
        #endregion
        
        #region CanCast
        public bool CanCast()
        {
            //Variables.Log.Debug("CanCast(" + this.Name + ") Check for Cast");
            if (this.SpellPtr.UsesAvailable <= 0)
                return false;
            int Truechecks = 0;
            int Trues = 0;
            //Ints
            if (this.MinManaPercent != 0)
                Truechecks++;
            if (this.MinLifePercent != 0)
                Truechecks++;
            if (this.MinEnemyLifePercent != 0)
                Truechecks++;
            if (this.MobsAroundDistance != 0)
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
                //Variables.Log.Debug("Allrounder(Cast): " + this.Name);
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
            if (this.MobsAroundDistance != 0 && (MobsAroundTarget == 1 && Functions.NumberOfEnemysNear(LokiPoe.Me, MobsAroundDistance, MobsAroundCount) || MobsAroundTarget == 0 && Functions.NumberOfEnemysNear(Variables.MainTarget, MobsAroundDistance, MobsAroundCount)))
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
            //Variables.Log.DebugFormat("Allrounder(Cast): Trying to Cast {0} Truechecks {1}:{2}", this.Name, Trues, Truechecks);
            if (Trues >= Truechecks && !Variables.Me.IsAbilityCooldownActive)
            {
                //Variables.Log.Debug("Allrounder(Cast): " + this.Name);
                return true;
            }
            return false;
        }
        #endregion
    }
}
