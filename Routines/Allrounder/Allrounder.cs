using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Loki.Bot;
using Loki.TreeSharp;
using Loki.Utilities;
using log4net;

using Action = Loki.TreeSharp.Action;
namespace Allrounder
{
    public partial class Allrounder : CombatRoutine
    {
        public static readonly ILog Log = Logger.GetLoggerInstanceForType();
        public override string Name { get { return "Allrounder by xTenshiSanx"; } }
        public override string ToString() { return Name; }
        public override void Dispose() { }
        public override void OnGuiButtonPress() { new AllrounderSettings().ShowDialog(); }
        public override Composite Combat { get { return CombatBot(); } }
        public override Composite Buff { get { return BuffBot(); } }
        public static PrioritySelector Fight;
        public override void Initialize()
        {
            Settings.DefualtSettings();
            Fight = new PrioritySelector();
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
                    Settings.DefualtSettings();
                    Log.Debug("Allrounder by xTenshiSanx has been started");
                    Log.Debug("Loading Skills...");
                    foreach (Skill atk in Settings.Instance.Skills)
                    {
                        if (atk.IsSummon)
                        {
                            Fight.AddChild(Functions.Cast(atk.Name, ret => Functions.GetCorpseNear(60).Position, ret => atk.CanCast()));
                            Log.Debug("Allrounder(OnStart): Added Skill " + atk.Name + " of Type " + atk.Type);
                        }
                        else
                        {
                            Fight.AddChild(Functions.Cast(atk.Name, ret => atk.CanCast()));
                            Log.Debug("Allrounder(OnStart): Added Skill " + atk.Name + " of Type " + atk.Type);
                        }
                    }
                    Variables.IsStarted = true;
                }
            }
        }
        void OnStop(IBot bot)
        {
            Fight = new PrioritySelector();
            Variables.SkillList = new List<Skill>();
            Variables.IsStarted = false;
        }
        void Pulse(IBot bot)
        {
            ///If Bot changes Target
            if (Variables.LastTarget == null || Variables.LastTarget != Variables.MainTarget)
            {
                Variables.LastTarget = Variables.MainTarget;
                foreach (Skill _skill in Variables.SkillList)
                {
                    _skill.CurrentCount = 0;
                }
            }
        }
        #region FlaskBot
        private static readonly WaitTimer _flaskCd = new WaitTimer(TimeSpan.FromSeconds(0.5));
        private Composite FlaskBot()
        {
            return new PrioritySelector(
                new Decorator(ret => _flaskCd.IsFinished && Variables.Me.HealthPercent < Settings.Instance.PotHealth && Variables.LifeFlasks.Count() != 0 && !Variables.Me.HasAura("flask_effect_life"),
                    new Action(ret =>
                    {
                        Variables.LifeFlasks.First().Use();
                        _flaskCd.Reset();
                    })),
                new Decorator(ret => _flaskCd.IsFinished && Variables.Me.ManaPercent < Settings.Instance.PotMana && Variables.ManaFlasks.Count() != 0 && !Variables.Me.HasAura("flask_effect_mana"),
                    new Action(ret =>
                    {
                        Variables.ManaFlasks.First().Use();
                        _flaskCd.Reset();
                    }))
                );
        }
        #endregion
        private Composite BuffBot()
        {
            return new PrioritySelector();
        }
        private Composite CombatBot()
        {
            return new PrioritySelector(
                FlaskBot(),
                Functions.CreateMoveToLos(),
                Functions.CreateMoveToRange(Settings.Instance.FightDistance),
                Fight
            );
        }
    }
}
