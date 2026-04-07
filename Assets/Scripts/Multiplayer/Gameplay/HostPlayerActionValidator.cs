using Core.Player;

namespace Core.Multiplayer
{
    public sealed class HostPlayerActionValidator
    {
        public bool TryValidate(in ClientToHostPlayerActionIntent actionIntent, PlayerController controller, out string rejectionReason)
        {
            if (controller == null)
            {
                rejectionReason = "missing-controller";
                return false;
            }

            if (!actionIntent.IsValid)
            {
                rejectionReason = "invalid-action-intent";
                return false;
            }

            if (controller.StateMachine == null || controller.StateMachine.CurrentState == null)
            {
                rejectionReason = "missing-state";
                return false;
            }

            if (controller.StateMachine.CurrentState == controller.DeadState)
            {
                rejectionReason = "dead";
                return false;
            }

            if (controller.StateMachine.CurrentState == controller.StunState)
            {
                rejectionReason = "stunned";
                return false;
            }

            if (controller.StateMachine.CurrentState == controller.HitState)
            {
                rejectionReason = "in-hit";
                return false;
            }

            switch (actionIntent.RequestedFlag)
            {
                case InputFlag.Dash:
                    if (controller.StateMachine.CurrentState == controller.AttackState)
                    {
                        return controller.CanCancelAttackIntoAuthoritativeDash(out rejectionReason);
                    }

                    if (!controller.CanDash)
                    {
                        rejectionReason = "dash-cooldown";
                        return false;
                    }

                    rejectionReason = null;
                    return true;

                case InputFlag.Attack:
                    if (controller.AttackCombos == null || controller.AttackCombos.Length <= 0)
                    {
                        rejectionReason = "missing-attack1";
                        return false;
                    }

                    if (controller.StateMachine.CurrentState == controller.AttackState)
                    {
                        return controller.CanQueueAuthoritativeAttackCombo(out rejectionReason);
                    }

                    if (controller.StateMachine.CurrentState == controller.DashState)
                    {
                        rejectionReason = "dash-active";
                        return false;
                    }

                    rejectionReason = null;
                    return true;

                default:
                    rejectionReason = "unsupported-action";
                    return false;
            }
        }
    }
}
