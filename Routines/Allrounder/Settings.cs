using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Loki.Game;
using Loki.Utilities;

namespace Allrounder
{
    class Settings : JsonSettings
    {
        #region Variables
        public int PotHealth
        {
            get;
            set;
        }
        public int PotMana
        {
            get;
            set;
        }
        public int FightDistance
        {
            get;
            set;
        }
        public List<Skill> Skills
        {
            get;
            set;
        }
        #endregion
        
        #region privates
        private static Settings instance;
        #endregion
        
        #region Instance
        /// <summary>
        /// Returns the current settings Instance
        /// </summary>
        public static Settings Instance
        {
            get
            {
                return instance ?? (instance = new Settings());
            }
        }
        #endregion

        #region Constructor
        public Settings() : base(GetSettingsFilePath("Character", LokiPoe.CurrentInstance.League, LokiPoe.Me.Name, "Allrounder.json"))
        {
            if (this.Skills == null)
            {
                this.Skills = new List<Skill>();
            }
        }
        #endregion

        #region DefaultSettings
        public static void DefualtSettings()
        {
            if(Instance.PotHealth == 0)
            {
                Instance.PotHealth = 50;
                Instance.Save();
            }
            if (Instance.PotMana == 0)
            {
                Instance.PotMana = 50;
                Instance.Save();
            }
            if (Instance.FightDistance == 0)
            {
                Instance.FightDistance = Convert.ToInt32(Loki.Bot.CharacterSettings.Instance.CombatRange);
                Instance.Save();
            }
            if (Instance.Skills.Count == 0)
            {
                Instance.Skills.Add(new Skill("Default Attack"));
                Instance.Save();
            }
        }
        #endregion


    }
}
