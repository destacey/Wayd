'use client'

import {
  DependencyMapLegend,
  revealOverflow,
  toggleExpansion,
  useDependencyMapExpansions,
  type DependencyEdgeStyle,
  type DependencyFarSide,
  type DependencyMapExpansionState,
} from '@/src/components/common/dependency-map'
import { DependencyHealth } from '@/src/components/types'
import { getHealthDescription } from '@/src/components/common/work/dependency-health-tooltip'
import {
  ScopedDependencyDto,
  WorkItemDetailsDto,
} from '@/src/services/wayd-api'
import { toFileName } from '@/src/utils'
import { Card, Flex, Segmented, Skeleton, theme, Typography } from 'antd'
import dynamic from 'next/dynamic'
import { useState } from 'react'
import { useExpandedWorkItemDependencies } from './use-expanded-work-item-dependencies'
import { useWorkItemDependencyFilter } from './use-work-item-dependency-filter'
import WorkItemDrawer from './work-item-drawer'
import {
  buildWorkItemDependencyNeighbourhood,
  WORK_ITEM_DEPENDENCY_VARIANTS,
  type WorkItemDependencyExpansions,
  type WorkItemDependencyFilter,
  type WorkItemDependencyVariant,
  type WorkItemMapRef,
} from './work-item-dependency-neighbourhood'

// Loaded on demand: the graph canvas is the heaviest thing on this page and many work items have no
// dependencies at all, so it must not sit in the bundle every work item page pays for.
const DependencyMap = dynamic(
  () => import('@/src/components/common/dependency-map/dependency-map'),
  { ssr: false, loading: () => <Skeleton active paragraph={{ rows: 4 }} /> },
)

const { Text } = Typography

const FAR_SIDES: DependencyFarSide[] = ['left', 'right']

const EXPAND_TOOLTIPS: Record<DependencyFarSide, (label: string) => string> = {
  left: (label) => `Show what ${label} is waiting on`,
  right: (label) => `Show what is waiting on ${label}`,
}

const VARIANT_DESCRIPTIONS: Record<WorkItemDependencyVariant, string> = {
  Healthy: getHealthDescription(DependencyHealth.Healthy),
  'At Risk': getHealthDescription(DependencyHealth.AtRisk),
  Unhealthy: getHealthDescription(DependencyHealth.Unhealthy),
  Unknown: getHealthDescription(DependencyHealth.Unknown),
  Done: 'The predecessor is done, so the link no longer holds anything up.',
}

export interface WorkItemDependencyMapCardProps {
  workItem: WorkItemDetailsDto
  /** This work item's dependencies, already loaded for the Overview's tiles. */
  dependencies?: ScopedDependencyDto[]
  /** Opens the Dependencies section, which the card links to. */
  onViewAll?: () => void
}

/**
 * The Overview's map of what a work item waits on and what waits on it, with whatever the reader expanded
 * outward from it.
 *
 * Absent rather than empty when the work item has no dependencies: a map of one node says less than no
 * map.
 */
const WorkItemDependencyMapCard = ({
  workItem,
  dependencies,
  onViewAll,
}: WorkItemDependencyMapCardProps) => {
  const { token } = theme.useToken()
  const [filter, setFilter] = useWorkItemDependencyFilter()
  const [state, setState] = useDependencyMapExpansions(workItem.id)
  const [opened, setOpened] = useState<WorkItemMapRef | null>(null)

  const expandedIds = FAR_SIDES.flatMap((side) => Object.keys(state[side]))
  const loaded = useExpandedWorkItemDependencies(dependencies, expandedIds)

  const subject: WorkItemMapRef = {
    id: workItem.id,
    key: workItem.key,
    title: workItem.title,
    workspaceKey: workItem.workspace.key,
  }

  // An expansion whose item is not yet known is left out rather than shown as loading: it sits beyond one
  // that has not loaded, so it has no node to mark yet.
  const expansionsFor = (
    from: DependencyMapExpansionState,
  ): WorkItemDependencyExpansions => {
    const bySide = (side: DependencyFarSide) =>
      Object.fromEntries(
        Object.entries(from[side])
          .filter(([id]) => loaded[id])
          .map(([id, options]) => [
            id,
            { ...loaded[id], showAll: options.showAll },
          ]),
      )

    return { left: bySide('left'), right: bySide('right') }
  }

  const build = (from: DependencyMapExpansionState) =>
    buildWorkItemDependencyNeighbourhood({
      workItem: subject,
      dependencies,
      filter,
      expansions: expansionsFor(from),
      subjectShowAll: from.subjectShowAll,
    })

  // Whether the card shows is decided on every link, not the filtered ones: a work item whose links are
  // all done would otherwise lose the map, and with it the control that would turn the filter back off.
  const hasDependencies =
    buildWorkItemDependencyNeighbourhood({ workItem: subject, dependencies })
      .edges.length > 0
  const neighbourhood = build(state)

  if (!hasDependencies) return null

  // Pruning waits while anything is loading: an item not yet reached is not one no longer reachable.
  const stillLoading = expandedIds.some(
    (id) => !loaded[id]?.dependencies && !loaded[id]?.isError,
  )

  const onToggleExpansion = (side: DependencyFarSide, workItemId: string) =>
    setState(
      toggleExpansion(
        state,
        side,
        workItemId,
        stillLoading ? null : (next) => build(next).placed,
      ),
    )

  const onShowAll = (side: DependencyFarSide, ownerId: string | null) =>
    setState(revealOverflow(state, side, ownerId))

  // Every work item the map can draw, by id, since a node carries only its id and label.
  const drawable = new Map<string, WorkItemMapRef>(
    [
      ...(dependencies ?? []),
      ...Object.values(loaded).flatMap((entry) => entry.dependencies ?? []),
    ].map(({ dependency }) => [dependency.id, dependency]),
  )
  const onOpenRecord = ({ recordId }: { recordId: string }) =>
    setOpened(drawable.get(recordId) ?? null)

  // Health in the colours its tags use everywhere else, so a red line and a red Unhealthy tag mean the
  // same thing. Done is dashed and faint: it is history, not a risk, and must not compete with them.
  const edgeStyles: Record<WorkItemDependencyVariant, DependencyEdgeStyle> = {
    Healthy: { stroke: token.colorSuccess, width: 2 },
    'At Risk': { stroke: token.colorWarning, width: 2 },
    Unhealthy: { stroke: token.colorError, width: 2 },
    Unknown: { stroke: token.colorTextSecondary, width: 1.5 },
    Done: { stroke: token.colorTextQuaternary, width: 1.5, dashed: true },
  }

  const legend = WORK_ITEM_DEPENDENCY_VARIANTS.filter(
    (variant) => filter === 'all' || variant !== 'Done',
  ).map((variant) => ({
    label: variant,
    style: edgeStyles[variant],
    description: VARIANT_DESCRIPTIONS[variant],
  }))

  return (
    <Card
      size="small"
      title="Dependency Map"
      extra={onViewAll && <a onClick={onViewAll}>View all</a>}
    >
      <DependencyMap
        nodes={neighbourhood.nodes}
        edges={neighbourhood.edges}
        edgeStyles={edgeStyles}
        expandTooltips={EXPAND_TOOLTIPS}
        height={
          neighbourhood.edges.length > 0 ? neighbourhood.height : undefined
        }
        fileStem={`${toFileName(workItem.key)}-dependency-map`}
        filters={
          <Segmented<WorkItemDependencyFilter>
            size="small"
            aria-label="Dependency state"
            value={filter}
            onChange={setFilter}
            options={[
              { label: 'All', value: 'all' },
              { label: 'Open only', value: 'open' },
            ]}
          />
        }
        emptyText="No open dependencies in either direction."
        onToggleExpansion={onToggleExpansion}
        onShowAll={onShowAll}
        onOpenRecord={onOpenRecord}
      />
      <Flex vertical gap={4}>
        <DependencyMapLegend items={legend} />
        <Text type="secondary">
          Arrows run from predecessor to successor: what this item waits on is
          on the left, what waits on it on the right. Boxes are teams.
          {filter === 'open' &&
            ' Showing open dependencies only: those whose predecessor is not done.'}
          {neighbourhood.edges.length > 0 &&
            ' Use + on a work item to follow the chain further out.'}
        </Text>
      </Flex>
      {opened && (
        <WorkItemDrawer
          workspaceKey={opened.workspaceKey}
          workItemKey={opened.key}
          open
          onClose={() => setOpened(null)}
        />
      )}
    </Card>
  )
}

export default WorkItemDependencyMapCard
