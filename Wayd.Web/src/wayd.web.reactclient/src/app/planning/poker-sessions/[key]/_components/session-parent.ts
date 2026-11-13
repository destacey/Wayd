import {
  IntakeDeskDto,
  PokerSessionHostDto,
  PokerSessionHostKind,
} from '@/src/services/wayd-api'

export interface SessionParentLink {
  label: string
  /** Absent for a hop the reader is only told about, which renders as text. */
  href?: string
}

const PLANNING_POKER: SessionParentLink = {
  label: 'Planning Poker',
  href: '/planning/poker-sessions',
}

/**
 * Whether the reader works the desk, and so gets links back to it: one of its
 * members, or someone who runs it without being named on it.
 */
export const worksDesk = (
  desk: IntakeDeskDto | undefined,
  employeeId: string | null | undefined,
): boolean =>
  !!desk &&
  (desk.canManage ||
    (!!employeeId && desk.members.some((m) => m.employee.id === employeeId)))

/**
 * Where a session's breadcrumb leads back to. A session an intake desk hosts
 * is listed on that desk and never on the Planning Poker page, so its trail
 * names the desk; any other session leads to the Planning Poker page.
 *
 * Only someone who works the desk gets links: the desk is where they came
 * from. An invited voter reads the same trail as text, since the desk is not
 * theirs to work.
 *
 * @param desk The hosting desk once it has loaded. Until then the trail is
 * text under a plain label.
 * @param linked Whether the reader works the desk (`worksDesk`).
 */
export const sessionParent = (
  host: PokerSessionHostDto | null | undefined,
  desk: { key: number; name: string } | undefined,
  linked: boolean,
): SessionParentLink | SessionParentLink[] => {
  if (host?.kind !== PokerSessionHostKind.IntakeDesk) return PLANNING_POKER

  const link = linked && !!desk
  return [
    {
      label: 'Desks',
      href: link ? '/demand-management/desks' : undefined,
    },
    {
      label: desk?.name ?? 'Desk',
      href: link
        ? `/demand-management/desks/${desk.key}?section=poker-sessions`
        : undefined,
    },
  ]
}
