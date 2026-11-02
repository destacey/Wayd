import { IncidentWorkflowAction } from '@/src/services/wayd-api'

export interface IncidentWorkflowActionAvailability {
  canBeginResponding: boolean
  canMarkRecovered: boolean
  canClose: boolean
  canCancel: boolean
}

/**
 * Which lifecycle moves an incident will accept from its current status.
 *
 * The incident workflow has the last word: a restricted workflow can forbid a move, and
 * `workflowActions` lists those it allows from the current status. Left out when unknown,
 * nothing is withheld on its account; the server still refuses a move the workflow does not allow.
 */
export const incidentWorkflowActionAvailability = (
  workflowActions?: readonly IncidentWorkflowAction[],
): IncidentWorkflowActionAvailability => {
  const allows = (action: IncidentWorkflowAction) =>
    workflowActions?.includes(action) ?? true

  return {
    canBeginResponding: allows(IncidentWorkflowAction.BeginResponding),
    canMarkRecovered: allows(IncidentWorkflowAction.MarkRecovered),
    canClose: allows(IncidentWorkflowAction.Close),
    canCancel: allows(IncidentWorkflowAction.Cancel),
  }
}
