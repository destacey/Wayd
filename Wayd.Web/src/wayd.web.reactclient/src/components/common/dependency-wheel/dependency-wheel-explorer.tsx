'use client'

import WaydTooltip from '@/src/components/common/wayd-tooltip'
import DependenciesGrid from '@/src/components/common/work/dependencies-grid'
import { DependencyDto } from '@/src/services/wayd-api'
import { CloseOutlined, InfoCircleOutlined } from '@ant-design/icons'
import { Button, Card, Empty, Flex, Tag, Typography } from 'antd'
import { ReactNode, useState } from 'react'
import DependencyWheel from './dependency-wheel'
import {
  buildDependencyWheelData,
  DependencyWheelSelection,
  matchesDependencyWheelSelection,
} from './dependency-wheel-data'
import styles from './dependency-wheel-explorer.module.css'

const { Text } = Typography

// Under the wheel, the rest of the window is too short for the list, so it gets a fixed height instead.
const GRID_HEIGHT = 560

export interface DependencyWheelExplorerProps {
  /** The dependencies to show, already narrowed by any filter the caller owns. */
  dependencies: DependencyDto[]
  isLoading: boolean
  refetch: () => void
  /** Names both the list's saved column layout and its CSV export. */
  gridKey: string
  /** Names the wheel's downloaded image. */
  exportFileName: string
  /** Shown in place of the wheel, and in the empty list, when there are no dependencies. */
  emptyMessage: string
  /** Controls at the right of the wheel card's header, such as a filter the caller owns. */
  extra?: ReactNode
}

/**
 * Dependencies that cross team lines: a wheel of where they run between teams, over the list of them.
 * Picking a team or a team pair on the wheel narrows the list to it. Give the component a new `key` when
 * the caller's own filter changes, so a pick made on the old set does not carry over.
 */
const DependencyWheelExplorer = ({
  dependencies,
  isLoading,
  refetch,
  gridKey,
  exportFileName,
  emptyMessage,
  extra,
}: DependencyWheelExplorerProps) => {
  const [selection, setSelection] = useState<DependencyWheelSelection | null>(
    null,
  )

  const wheelData = buildDependencyWheelData(dependencies)
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
  const rows = dependencies.filter((d) =>
    matchesDependencyWheelSelection(d, activeSelection),
  )

  const groupName = (key: string) =>
    wheelData.groups.find((g) => g.key === key)?.name ?? key
  const selectionLabel = !activeSelection
    ? null
    : activeSelection.kind === 'group'
      ? groupName(activeSelection.key)
      : `${groupName(activeSelection.sourceKey)} → ${groupName(activeSelection.targetKey)}`

  const undrawnNote =
    wheelData.undrawn === 0
      ? null
      : wheelData.undrawn === 1
        ? '1 dependency between two items with no team is in the list but not on the wheel.'
        : `${wheelData.undrawn} dependencies between items with no team are in the list but not on the wheel.`

  return (
    <div className={styles.layout}>
      <Card
        size="small"
        title={
          <Flex align="center" gap={6}>
            Dependencies Between Teams
            <WaydTooltip title="Each arc is a team. Each ribbon runs from the team that owns the predecessors to the team waiting on them, in the predecessors' team's colour. Hover over a team or ribbon for a summary; pick one to filter the list.">
              <InfoCircleOutlined aria-label="How to read the wheel" />
            </WaydTooltip>
          </Flex>
        }
        extra={extra}
        loading={isLoading}
      >
        {dependencies.length === 0 ? (
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
              exportFileName={exportFileName}
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
          gridKey={gridKey}
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

export default DependencyWheelExplorer
