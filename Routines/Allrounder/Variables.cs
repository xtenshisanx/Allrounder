using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Loki;
using Loki.Bot;
using Loki.Game;
using Loki.Game.Inventory;
using Loki.Game.Objects;

namespace Allrounder
{
    class Variables
    {
        public static bool IsStarted = false;
        public static LocalPlayer Me { get { return LokiPoe.Me; } }
        public static Monster MainTarget { get { return Targeting.Combat.Targets.FirstOrDefault() as Monster; } }
        public static Monster LastTarget;
        public static List<Skill> SkillList = new List<Skill>();
        #region Flasks
        #region LifeFlasks
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
        #endregion
        #region ManaFlasks
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
        #endregion
        #region GraniteFlasks
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
        #endregion
        #region QuicksilverFlaks
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
        #endregion
        #region GetAQuicksilver
        public static InventoryItem GetaQuicksilver()
        {
            foreach (InventoryItem _item in LokiPoe.Me.Inventory.Flasks.Items)
                if (_item != null && _item.Name == "Quicksilver Flask" && _item.Flask.CurrentCharges > 0)
                    return _item;
            return null;
        }
        #endregion
        #endregion
    }
}
