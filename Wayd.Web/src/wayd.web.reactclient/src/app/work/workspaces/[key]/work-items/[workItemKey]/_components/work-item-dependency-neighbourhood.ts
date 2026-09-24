import {
  buildDependencyNeighbourhood,
  type DependencyExpansion,
  type DependencyFarSide,
  type DependencyMapLink,
  type DependencyMapLinks,
  type DependencyMapRecord,
  type DependencyNeighbourhood,
} from '@/src/components/common/dependency-map'
import {
  ScopedDependencyDto,
  WorkTeamNavigationDto,
} from '@/src/services/wayd-api'
import { teamUrl } from '@/src/utils/team-url'

/** Which links the map draws: all of them, or only those whose predecessor is not done yet. */
export type WorkItemDependencyFilter = 'all' | 'open'

/**
 * What an edge is drawn as. A done link has stopped blocking anything, so its planning health no longer
 * says anything worth a colour; every other link is drawn as its health.
 */
export const WORK_ITEM_DEPENDENCY_VARIANTS = [
  'Healthy',
  'At Risk',
  'Unhealthy',
  'Unknown',
  'Done',
] as const

export type WorkItemDependencyVariant =
  (typeof WORK_ITEM_DEPENDENCY_VARIANTS)[number]

/** The state name the API sends once the predecessor is done. */
const DONE_STATE = 'Done'

/** Enough of a work item to draw it and to fetch its own dependencies. */
export interface WorkItemMapRef {
  id: string
  key: string
  title: string
  workspaceKey: string
}

/** A work item the reader expanded, with whatever has loaded for it. */
export interface WorkItemDependencyExpansion extends Omit<
  DependencyExpansion,
  'links'
> {
  /** The expanded item itself, which its scoped dependencies are relative to. */
  workItem: WorkItemMapRef
  /** Undefined while loading. */
  dependencies?: ScopedDependencyDto[]
}

export type WorkItemDependencyExpansions = Record<
  DependencyFarSide,
  Record<string, WorkItemDependencyExpansion>
>

export interface BuildWorkItemDependencyNeighbourhoodOptions {
  workItem: WorkItemMapRef
  dependencies: ScopedDependencyDto[] | undefined
  /** Work items drawn per column, per item whose links fill it, before the rest collapse into a count. */
  maxPerSide?: number
  filter?: WorkItemDependencyFilter
  expansions?: WorkItemDependencyExpansions
  /** Sides where the subject's own overflow has been revealed. */
  subjectShowAll?: DependencyFarSide[]
}

export const workItemHref = (item: { workspaceKey: string; key: string }) =>
  `/work/workspaces/${item.workspaceKey}/work-items/${item.key}`

/** The key leads, because two titles can match and a key never does, and it survives the label's clamp. */
export const workItemLabel = (item: { key: string; title: string }) =>
  `${item.key} · ${item.title}`

const record = (item: WorkItemMapRef): DependencyMapRecord => ({
  id: item.id,
  label: workItemLabel(item),
  href: workItemHref(item),
})

const teamRecord = (team: WorkTeamNavigationDto): DependencyMapRecord => ({
  id: team.id,
  label: team.name,
  href: teamUrl(team),
})

export const variantOf = (
  dependency: ScopedDependencyDto,
): WorkItemDependencyVariant => {
  if (dependency.state?.name === DONE_STATE) return 'Done'

  const health = dependency.health?.name
  return WORK_ITEM_DEPENDENCY_VARIANTS.includes(
    health as WorkItemDependencyVariant,
  )
    ? (health as WorkItemDependencyVariant)
    : 'Unknown'
}

/**
 * One item's scoped dependencies as the map's links, with the arrow running from predecessor to
 * successor: what has to finish first sits on the left, what waits for this item on the right.
 *
 * The far item is drawn inside its team, which is what makes a cross-team link visible at a glance. The
 * near item is the one the list is scoped to, so it needs no path.
 */
export const toWorkItemLinks = (
  owner: WorkItemMapRef,
  dependencies: ScopedDependencyDto[],
  filter: WorkItemDependencyFilter = 'all',
): DependencyMapLinks => {
  const near = record(owner)
  const left: DependencyMapLink[] = []
  const right: DependencyMapLink[] = []

  const ordered = [...dependencies]
    .filter((d) => filter === 'all' || d.state?.name !== DONE_STATE)
    .sort((a, b) =>
      a.dependency.key.localeCompare(b.dependency.key, undefined, {
        numeric: true,
      }),
    )

  for (const dependency of ordered) {
    const far = record(dependency.dependency)
    const farPath = dependency.dependency.team
      ? [teamRecord(dependency.dependency.team)]
      : []
    const variant = variantOf(dependency)

    if (dependency.type === 'Predecessor') {
      left.push({
        id: dependency.id,
        source: far,
        target: near,
        sourcePath: farPath,
        targetPath: [],
        variant,
      })
    } else if (dependency.type === 'Successor') {
      right.push({
        id: dependency.id,
        source: near,
        target: far,
        sourcePath: [],
        targetPath: farPath,
        variant,
      })
    }
  }

  return { left, right }
}

/**
 * The map of what a work item waits on and what waits on it: one hop out, and further along any item the
 * reader expanded, so a chain of predecessors can be followed back to where it starts.
 */
export const buildWorkItemDependencyNeighbourhood = ({
  workItem,
  dependencies,
  maxPerSide,
  filter = 'all',
  expansions,
  subjectShowAll,
}: BuildWorkItemDependencyNeighbourhoodOptions): DependencyNeighbourhood => {
  const bySide = (side: DependencyFarSide) =>
    Object.fromEntries(
      Object.entries(expansions?.[side] ?? {}).map(
        ([id, { workItem: owner, dependencies: deps, ...rest }]) => [
          id,
          {
            ...rest,
            links: deps ? toWorkItemLinks(owner, deps, filter) : undefined,
          },
        ],
      ),
    )

  return buildDependencyNeighbourhood({
    subject: record(workItem),
    links: dependencies
      ? toWorkItemLinks(workItem, dependencies, filter)
      : undefined,
    maxPerSide,
    overflowQualifier: filter === 'open' ? 'open' : undefined,
    expansions: { left: bySide('left'), right: bySide('right') },
    subjectShowAll,
  })
}
