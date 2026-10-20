import { TransitionConditionKind } from '@/src/services/wayd-api'

/** What each condition on a workflow transition asks of a move, as an editor names it. */
export const transitionConditionLabels: Record<
  TransitionConditionKind,
  string
> = {
  [TransitionConditionKind.RequiresReason]: 'Requires a reason',
  [TransitionConditionKind.RequiresPassedApproval]:
    'Requires a passed approval',
}

/** Whether a move with these conditions must carry a reason. */
export const requiresReason = (
  conditions: readonly TransitionConditionKind[] | undefined,
) => conditions?.includes(TransitionConditionKind.RequiresReason) ?? false
