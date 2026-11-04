'use client'

import {
  buildDependencyWheelData,
  DependencyWheel,
  matchesDependencyWheelSelection,
  type DependencyWheelSelection,
} from '@/src/components/common/dependency-wheel'
import DependenciesGrid from '@/src/components/common/work/dependencies-grid'
import { DependencyDto } from '@/src/services/wayd-api'
import { useGetPlanningIntervalDependenciesQuery } from '@/src/store/features/planning/planning-interval-api'
import WaydTooltip from '@/src/components/common/wayd-tooltip'
import { CloseOutlined, InfoCircleOutlined } from '@ant-design/icons'
import { Button, Card, Empty, Flex, Segmented, Tag, Typography } from 'antd'
import { useState } from 'react'
import styles from './planning-interval-dependencies-section.module.css'

const { Text } = Typography

// Under the wheel, the rest of the window is too short for the list, so it gets a fixed height instead.
const GRID_HEIGHT = 560

export type DependencyStateFilter = 'open' | 'all'

// DependencyState ids: To Do and In Progress are open; Done is not.
const OPEN_STATE_IDS = [1, 2]

/** Whether a dependency passes the open/all filter: open means its predecessor is not done. */
export const matchesStateFilter = (
  dependency: DependencyDto,
  filter: DependencyStateFilter,
) => filter === 'all' || OPEN_STATE_IDS.includes(dependency.state.id)

export interface PlanningIntervalDependenciesSectionProps {
  planningIntervalKey: number
}

/**
 * The PI's dependencies that cross team lines: a wheel of where they run between teams, over the grid of
 * them. Picking a team or a team pair on the wheel narrows the grid to it.
 */
const PlanningIntervalDependenciesSection = ({
  planningIntervalKey,
}: PlanningIntervalDependenciesSectionProps) => {
  const [stateFilter, setStateFilter] = useState<DependencyStateFilter>('open')
  const [selection, setSelection] = useState<DependencyWheelSelection | null>(
    null,
  )

  const { data, isLoading, refetch } =
    useGetPlanningIntervalDependenciesQuery(planningIntervalKey)

  const inState = (data ?? []).filter((d) => matchesStateFilter(d, stateFilter))
  const wheelData = buildDependencyWheelData(inState)
  // A pick can outlive the data it was made on, when a refresh drops its dependencies.
  const activeSelection =
    selection &&
    (selection.kind === 'group'
      ? wheelData.groups.some((g) => g.key === selection.key)
      : wheelData.flows.some(
          (f) =>
            f.sourceKey === selection.sourceKey &&
            f.targetKey === selection.targetKey,
        ))
      ? selection
      : null
  const rows = inState.filter((d) =>
    matchesDependencyWheelSelection(d, activeSelection),
  )

  const groupName = (key: string) =>
    wheelData.groups.find((g) => g.key === key)?.name ?? key
  const selectionLabel = !activeSelection
    ? null
    : activeSelection.kind === 'group'
      ? groupName(activeSelection.key)
      : `${groupName(activeSelection.sourceKey)} → ${groupName(activeSelection.targetKey)}`

  const emptyMessage =
    stateFilter === 'open'
      ? 'No open dependencies cross team lines in this PI.'
      : 'No dependencies cross team lines in this PI.'
  const undrawnNote =
    wheelData.undrawn === 0
      ? null
      : wheelData.undrawn === 1
        ? '1 dependency between two items with no team is in the list but not on the wheel.'
        : `${wheelData.undrawn} dependencies between items with no team are in the list but not on the wheel.`

  const stateControl = (
    <Segmented<DependencyStateFilter>
      size="small"
      value={stateFilter}
      onChange={(value) => {
        setStateFilter(value)
        setSelection(null)
      }}
      options={[
        { label: 'Open', value: 'open' },
        { label: 'All', value: 'all' },
      ]}
      aria-label="Dependency state"
    />
  )

  return (
    <div className={styles.layout}>
      <Card
        size="small"
        title={
          <Flex align="center" gap={6}>
            Dependencies Between Teams
            <WaydTooltip title="Each arc is a team. Each ribbon runs from the team that owns the predecessors to the team waiting on them, in the predecessors' team's colour. Hover over a ribbon for its health; pick a team or ribbon to filter the list.">
              <InfoCircleOutlined aria-label="How to read the wheel" />
            </WaydTooltip>
          </Flex>
        }
        extra={stateControl}
        loading={isLoading}
      >
        {inState.length === 0 ? (
          <Empty
            image={Empty.PRESENTED_IMAGE_SIMPLE}
            description={emptyMessage}
          />
        ) : wheelData.groups.length === 0 ? (
          <Text type="secondary">{undrawnNote}</Text>
        ) : (
          <Flex vertical gap="small">
            <DependencyWheel
              data={wheelData}
              selection={activeSelection}
              onSelectionChange={setSelection}
              exportFileName={`planning-interval-${planningIntervalKey}-dependencies`}
            />
            {undrawnNote && <Text type="secondary">{undrawnNote}</Text>}
          </Flex>
        )}
      </Card>
      {/* A plain block, so the grid is never a flex item: as one it collapses to no rows. */}
      <div>
        <DependenciesGrid
          dependencies={rows}
          height={GRID_HEIGHT}
          isLoading={isLoading}
          refetch={refetch}
          gridKey="planning-interval-dependencies"
          emptyMessage={emptyMessage}
          leftSlot={
            selectionLabel && (
              <Flex align="center" gap={4}>
                <Tag>{selectionLabel}</Tag>
                <Button
                  type="text"
                  size="small"
                  icon={<CloseOutlined />}
                  aria-label="Clear the wheel filter"
                  onClick={() => setSelection(null)}
                />
              </Flex>
            )
          }
        />
      </div>
    </div>
  )
}

export default PlanningIntervalDependenciesSection
