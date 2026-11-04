import { DependencyHealth } from '@/src/components/types'
import { DependencyDto } from '@/src/services/wayd-api'
import { caseInsensitiveCompare } from '@/src/components/common/wayd-grid'

/** The group key for work items with no team; every other group is keyed by team id. */
export const NO_TEAM_KEY = 'no-team'

/** How many dependencies have each health. */
export type DependencyHealthCounts = Record<DependencyHealth, number>

/** Another group this one shares dependencies with, in either direction. */
export interface DependencyWheelPartner {
  key: string
  label: string
  count: number
}

export interface DependencyWheelGroup {
  key: string
  name: string
  /** The short name drawn on the wheel: the team code. */
  label: string
  /** Dependencies where this group owns the predecessor. */
  outgoing: number
  /** Dependencies where this group owns the successor. */
  incoming: number
  healthCounts: DependencyHealthCounts
  /** Most dependencies first, then by label. */
  partners: DependencyWheelPartner[]
}

export interface DependencyWheelFlow {
  sourceKey: string
  targetKey: string
  count: number
  /** The most severe health among the flow's dependencies. */
  health: DependencyHealth
  healthCounts: DependencyHealthCounts
  /** Dependencies whose predecessor is To Do or In Progress. */
  open: number
  /** Dependencies whose predecessor is done. */
  done: number
}

export interface DependencyWheelData {
  /** Sorted by name, with "No team" last. */
  groups: DependencyWheelGroup[]
  /** `matrix[i][j]` counts dependencies from `groups[i]`'s predecessors to `groups[j]`'s successors. */
  matrix: number[][]
  flows: DependencyWheelFlow[]
  /** Dependencies whose two ends fall in the same group, which a wheel cannot draw. */
  undrawn: number
}

/** What the wheel has picked: every dependency touching a team, or one directed team pair. */
export type DependencyWheelSelection =
  | { kind: 'group'; key: string }
  | { kind: 'flow'; sourceKey: string; targetKey: string }

// Higher is more severe. Unknown sits just above Healthy so it is never hidden by one.
const severity: Record<DependencyHealth, number> = {
  [DependencyHealth.Healthy]: 0,
  [DependencyHealth.Unknown]: 1,
  [DependencyHealth.AtRisk]: 2,
  [DependencyHealth.Unhealthy]: 3,
}

// DependencyState ids: To Do and In Progress are open; Done is done.
const OPEN_STATE_IDS = [1, 2]
const DONE_STATE_ID = 3

const noHealth = (): DependencyHealthCounts => ({
  [DependencyHealth.Healthy]: 0,
  [DependencyHealth.AtRisk]: 0,
  [DependencyHealth.Unhealthy]: 0,
  [DependencyHealth.Unknown]: 0,
})

const groupKey = (team: { id: string } | undefined): string =>
  team?.id ?? NO_TEAM_KEY

/** Counts dependencies between teams, the shape a directed chord diagram draws. */
export function buildDependencyWheelData(
  dependencies: DependencyDto[],
): DependencyWheelData {
  const groupsByKey = new Map<string, DependencyWheelGroup>()
  const flowsByKey = new Map<string, DependencyWheelFlow>()
  let undrawn = 0

  const touch = (
    team: { id: string; name: string; code: string } | undefined,
  ) => {
    const key = groupKey(team)
    let group = groupsByKey.get(key)
    if (!group) {
      group = {
        key,
        name: team?.name ?? 'No team',
        label: team?.code ?? 'No team',
        outgoing: 0,
        incoming: 0,
        healthCounts: noHealth(),
        partners: [],
      }
      groupsByKey.set(key, group)
    }
    return group
  }

  for (const dependency of dependencies) {
    const source = touch(dependency.source.team)
    const target = touch(dependency.target.team)
    if (source.key === target.key) {
      undrawn++
      continue
    }

    const health = dependency.health.id as DependencyHealth
    source.outgoing++
    target.incoming++
    source.healthCounts[health]++
    target.healthCounts[health]++

    const flowKey = `${source.key}>${target.key}`
    let flow = flowsByKey.get(flowKey)
    if (!flow) {
      flow = {
        sourceKey: source.key,
        targetKey: target.key,
        count: 0,
        health,
        healthCounts: noHealth(),
        open: 0,
        done: 0,
      }
      flowsByKey.set(flowKey, flow)
    }
    flow.count++
    flow.healthCounts[health]++
    if ((severity[health] ?? 0) > (severity[flow.health] ?? 0))
      flow.health = health
    if (OPEN_STATE_IDS.includes(dependency.state.id)) flow.open++
    else if (dependency.state.id === DONE_STATE_ID) flow.done++
  }

  // A group whose only dependencies are undrawn has no arc to give it.
  const groups = [...groupsByKey.values()]
    .filter((g) => g.outgoing + g.incoming > 0)
    .sort((a, b) => {
      if (a.key === NO_TEAM_KEY) return 1
      if (b.key === NO_TEAM_KEY) return -1
      return caseInsensitiveCompare(a.name, b.name)
    })

  const flows = [...flowsByKey.values()]
  for (const group of groups) {
    const counts = new Map<string, number>()
    for (const flow of flows) {
      const other =
        flow.sourceKey === group.key
          ? flow.targetKey
          : flow.targetKey === group.key
            ? flow.sourceKey
            : null
      if (other) counts.set(other, (counts.get(other) ?? 0) + flow.count)
    }
    group.partners = [...counts.entries()]
      .map(([key, count]) => ({
        key,
        label: groupsByKey.get(key)!.label,
        count,
      }))
      .sort(
        (a, b) => b.count - a.count || caseInsensitiveCompare(a.label, b.label),
      )
  }

  const index = new Map(groups.map((g, i) => [g.key, i]))
  const matrix = groups.map(() => groups.map(() => 0))
  for (const flow of flows) {
    matrix[index.get(flow.sourceKey)!][index.get(flow.targetKey)!] = flow.count
  }

  return { groups, matrix, flows, undrawn }
}

/** Whether a dependency belongs to what the wheel has picked; nothing picked matches everything. */
export function matchesDependencyWheelSelection(
  dependency: DependencyDto,
  selection: DependencyWheelSelection | null,
): boolean {
  if (!selection) return true

  const sourceKey = groupKey(dependency.source.team)
  const targetKey = groupKey(dependency.target.team)

  return selection.kind === 'group'
    ? sourceKey === selection.key || targetKey === selection.key
    : sourceKey === selection.sourceKey && targetKey === selection.targetKey
}
