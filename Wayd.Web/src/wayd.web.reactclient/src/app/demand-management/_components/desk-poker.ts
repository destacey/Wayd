import {
  IntakeDeskDto,
  IntakeRequestSummaryDto,
  StatusCategory,
} from '@/src/services/wayd-api'

/** The permission Planning asks of anyone opening a poker session. */
export const POKER_VIEW_PERMISSION = 'Permissions.PokerSessions.View'

export const pokerSessionHref = (key: number) =>
  `/planning/poker-sessions/${key}`

const isOpen = (request: IntakeRequestSummaryDto) =>
  request.status.category === StatusCategory.Proposed ||
  request.status.category === StatusCategory.Active

/**
 * Whether the employee can start a poker session on the desk: they work it,
 * and it estimates its requests. The server decides; this only keeps the
 * action from being offered where it would be refused.
 */
export const canRunDeskPoker = (
  desk: IntakeDeskDto | undefined,
  employeeId: string | null | undefined,
): boolean =>
  !!desk &&
  !!employeeId &&
  !!desk.estimationScale &&
  desk.members.some((m) => m.employee.id === employeeId)

export type PokerSelection =
  { desk: IntakeDeskDto; requestIds: string[] } | { refusal: string }

/**
 * The desk a selection of requests can be estimated on, or why it cannot be:
 * a session belongs to one desk, takes only open requests, and is started by
 * someone who works that desk.
 */
export const pokerSelection = (
  selected: IntakeRequestSummaryDto[],
  desks: IntakeDeskDto[] | undefined,
  employeeId: string | null | undefined,
): PokerSelection => {
  if (selected.length === 0)
    return { refusal: 'Select the requests to estimate.' }

  const deskIds = new Set(selected.map((r) => r.desk.id))
  if (deskIds.size > 1)
    return {
      refusal: 'Select requests from one desk: a session belongs to a desk.',
    }

  if (!selected.every(isOpen))
    return { refusal: 'Only open requests can be estimated.' }

  const desk = desks?.find((d) => d.id === selected[0].desk.id)
  if (!desk || !desk.members.some((m) => m.employee.id === employeeId))
    return { refusal: 'Only agents on the desk can start its poker sessions.' }

  if (!desk.estimationScale)
    return { refusal: `${desk.name} has no estimation scale.` }

  return { desk, requestIds: selected.map((r) => r.id) }
}
