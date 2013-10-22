// Behavior originally contributed by mastahg.
//
// DOCUMENTATION:
//     
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CommonBehaviors.Actions;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.ScoutingReportLikeJinyuInABarrel
{
    [CustomBehaviorFileName(@"SpecificQuests\29824-HordeJadeForest-ScoutingReportLikeJinyuInABarrel")]
    public class LikeJinyuinaBarrel : CustomForcedBehavior
    {
        private bool _isBehaviorDone;
        private bool _isDisposed;
        private Composite _root;

        public LikeJinyuinaBarrel(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                // QuestRequirement* attributes are explained here...
                //    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
                // ...and also used for IsDone processing.
                //Location = GetAttributeAsNullable<WoWPoint>("", true, ConstrainAs.WoWPointNonEmpty, null) ??WoWPoint.Empty;
                QuestId = 29824; //GetAttributeAsNullable<int>("QuestId", true, ConstrainAs.QuestId(this), null) ?? 0;
                //MobIds = GetAttributeAsNullable<int>("MobId", true, ConstrainAs.MobId, null) ?? 0;
                QuestRequirementComplete = QuestCompleteRequirement.NotComplete;
                QuestRequirementInLog = QuestInLogRequirement.InLog;
            }

            catch (Exception except)
            {
                // Maintenance problems occur for a number of reasons.  The primary two are...
                // * Changes were made to the behavior, and boundary conditions weren't properly tested.
                // * The Honorbuddy core was changed, and the behavior wasn't adjusted for the new changes.
                // In any case, we pinpoint the source of the problem area here, and hopefully it
                // can be quickly resolved.
                LogMessage("error", "BEHAVIOR MAINTENANCE PROBLEM: " + except.Message + "\nFROM HERE:\n" + except.StackTrace + "\n");
                IsAttributeProblem = true;
            }
        }


        // Attributes provided by caller
        public int QuestId { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement QuestRequirementInLog { get; private set; }

        // Private variables for internal state


        // Private properties
        private LocalPlayer Me
        {
            get { return (StyxWoW.Me); }
        }

        ~LikeJinyuinaBarrel()
        {
            Dispose(false);
        }


        public void Dispose(bool isExplicitlyInitiatedDispose)
        {
            if (!_isDisposed)
            {
                // NOTE: we should call any Dispose() method for any managed or unmanaged
                // resource, if that resource provides a Dispose() method.

                // Clean up managed resources, if explicit disposal...
                if (isExplicitlyInitiatedDispose)
                {
                    TreeHooks.Instance.RemoveHook("Combat_Main", CreateBehavior_MainCombat());
                    CharacterSettings.Instance.UseMount = _mount;
                }

                // Clean up unmanaged resources (if any) here...
                TreeRoot.GoalText = string.Empty;
                TreeRoot.StatusText = string.Empty;

                // Call parent Dispose() (if it exists) here ...
                base.Dispose();
            }

            _isDisposed = true;
        }

        #region Overrides of CustomForcedBehavior

        private bool _mount;
        private uint[] jinyu = new uint[] { 55793, 56701, 55791, 55711, 55709, 55710 };
        private bool spoke = false;
        WoWPoint _phase2RelativePos = new WoWPoint(0, 0, -30);
        public Composite DoneYet
        {
            get
            {
                return new Decorator(
                    ret => IsQuestComplete() || !Me.InVehicle,
                    new Action(
                        delegate
                        {
                            TreeRoot.StatusText = "Finished!";
                            CharacterSettings.Instance.UseMount = true;
                            _isBehaviorDone = true;
                            return RunStatus.Success;
                        }));
            }
        }


        //<Vendor Name="Pearlfin Poolwatcher" Entry="55709" Type="Repair" X="-100.9809" Y="-2631.66" Z="2.150823" />
        //<Vendor Name="Pearlfin Poolwatcher" Entry="55711" Type="Repair" X="-130.8297" Y="-2636.422" Z="1.639656" />

        //209691 - sniper rifle
        public WoWGameObject Rifle
        {
            get { return ObjectManager.GetObjectsOfType<WoWGameObject>().FirstOrDefault(r => r.Entry == 209691); }
        }


        public List<WoWUnit> Enemy
        {
            get { return ObjectManager.GetObjectsOfType<WoWUnit>().Where(r => jinyu.Contains(r.Entry)).ToList(); }
        }


        public WoWUnit Barrel
        {
            get { return ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(r => r.Entry == 55784); }
        }


        public Composite PhaseOne
        {
            get
            {
                WoWGameObject rifle = null;
                return new Decorator(
                    r => Me.RelativeLocation != _phase2RelativePos,
                    new PrioritySelector(ctx => rifle = Rifle,
                        new Decorator(r => !rifle.WithinInteractRange, new Action(r => WoWMovement.ClickToMove(rifle.Location))),
                        new Decorator(
                            r => rifle.WithinInteractRange,
                            new Sequence(
                                new Sleep(450),
                                new Action(
                                    r =>
                                    {
                                        Navigator.PlayerMover.MoveStop();
                                        rifle.Interact();
                                    })))));
            }
        }


        public Composite PhaseTwo
        {
            get
            {
                return new PrioritySelector(
                    new Decorator(r => Barrel != null, new Action(r => Barrel.Interact())),
                    new Decorator(
                        r => Enemy.Count > 0,
                        new Action(
                            r =>
                            {
                                var barrel = Barrel;
                                if (Barrel != null)
                                {
                                    Barrel.Interact();
                                }

                                foreach (var unit in Enemy)
                                {
                                    unit.Interact(true);
                                }

                                if (IsQuestComplete() || !Me.InVehicle)
                                {
                                    return RunStatus.Success;
                                }

                                return RunStatus.Running;
                            })));
            }
        }

        public override bool IsDone
        {
            get
            {
                return (_isBehaviorDone // normal completion
                        || !UtilIsProgressRequirementsMet(QuestId, QuestRequirementInLog, QuestRequirementComplete));
            }
        }

        public bool IsQuestComplete()
        {
            var quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);
            return quest == null || quest.IsCompleted;
        }

        public void CastSpell(string action)
        {
            var spell = StyxWoW.Me.PetSpells.FirstOrDefault(p => p.ToString() == action);
            if (spell == null)
                return;

            Logging.Write("[Pet] Casting {0}", action);
            Lua.DoString("CastPetAction({0})", spell.ActionBarIndex + 1);
        }


        protected Composite CreateBehavior_MainCombat()
        {
            return _root ?? (_root = new Decorator(ret => !_isBehaviorDone, new PrioritySelector(DoneYet, PhaseOne, PhaseTwo, new ActionAlwaysSucceed())));
        }


        public override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }


        public override void OnStart()
        {
            // This reports problems, and stops BT processing if there was a problem with attributes...
            // We had to defer this action, as the 'profile line number' is not available during the element's
            // constructor call.
            OnStart_HandleAttributeProblem();

            // If the quest is complete, this behavior is already done...
            // So we don't want to falsely inform the user of things that will be skipped.
            if (!IsDone)
            {
                _mount = CharacterSettings.Instance.UseMount;
                CharacterSettings.Instance.UseMount = false;
                TreeHooks.Instance.InsertHook("Combat_Main", 0, CreateBehavior_MainCombat());

                //TreeRoot.TicksPerSecond = 30;
                // Me.QuestLog.GetQuestById(27761).GetObjectives()[2].

                PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);

                TreeRoot.GoalText = GetType().Name + ": " + ((quest != null) ? ("\"" + quest.Name + "\"") : "In Progress");
            }
        }

        #endregion
    }
}