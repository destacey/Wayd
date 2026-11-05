'use client'

import DependenciesGrid from '@/src/components/common/work/dependencies-grid'
import { DependencyDto } from '@/src/services/wayd-api'
import { CloseOutlined } from '@ant-design/icons'
import { Button, Card, Flex, Tag, Typography } from 'antd'
import { FC, useState } from 'react'
import DependencyPartnersChart from './dependency-partners-chart'
import {
  buildDependencyPartnersData,
  matchesDependencyPartner,
} from './dependency-partners-data'
import styles from './dependency-partners-explorer.module.css'

const { Text } = Typography

// Under the chart, the rest of the window is too short for the list, so it gets a fixed height instead.
const GRID_HEIGHT = 560

export interface DependencyPartnersExplorerProps {
  /** The team the chart is drawn from: every other team on it is a partner. */
  teamId: string
  dependencies: DependencyDto[]
  isLoading: boolean
  refetch: () => void
  /** Names both the list's saved column layout and its CSV export. */
  gridKey: string
  /** Shown in the empty list. */
  emptyMessage?: string
}

/**
 * A team's dependencies: a chart of the other teams it shares them with, over the list of them. Picking a
 * team on the chart narrows the list to the dependencies with that team. Give the component a new `key`
 * when the dependencies are a different set, so a pick made on the old set does not carry over.
 */
const DependencyPartnersExplorer: FC<DependencyPartnersExplorerProps> = ({
  teamId,
  dependencies,
  isLoading,
  refetch,
  gridKey,
  emptyMessage,
}) => {
  const [partner, setPartner] = useState<string | null>(null)

  const partnersData = buildDependencyPartnersData(teamId, dependencies)
  // A pick can outlive the data it was made on, when a refresh drops its dependencies.
  const activePartner = partnersData.partners.find((p) => p.key === partner)
  const rows = dependencies.filter((d) =>
    matchesDependencyPartner(d, teamId, activePartner?.key ?? null),
  )

  const withinTeamNote =
    partnersData.withinTeam === 0
      ? null
      : partnersData.withinTeam === 1
        ? '1 dependency is within the team, so it is in the list but not on the chart.'
        : `${partnersData.withinTeam} dependencies are within the team, so they are in the list but not on the chart.`

  return (
    <div className={styles.layout}>
      {partnersData.partners.length > 0 && (
        <Card size="small" title="Dependencies With Other Teams">
          <Flex vertical gap="small">
            <DependencyPartnersChart
              data={partnersData}
              selection={activePartner?.key ?? null}
              onSelectionChange={setPartner}
            />
            {withinTeamNote && <Text type="secondary">{withinTeamNote}</Text>}
          </Flex>
        </Card>
      )}
      {/* A plain block, so the grid is never a flex item: as one it collapses to no rows. */}
      <div>
        <DependenciesGrid
          dependencies={rows}
          isLoading={isLoading}
          refetch={refetch}
          gridKey={gridKey}
          emptyMessage={emptyMessage}
          height={partnersData.partners.length > 0 ? GRID_HEIGHT : undefined}
          leftSlot={
            activePartner && (
              <Flex align="center" gap={4}>
                <Tag>{activePartner.name}</Tag>
                <Button
                  type="text"
                  size="small"
                  icon={<CloseOutlined />}
                  aria-label="Clear the team filter"
                  onClick={() => setPartner(null)}
                />
              </Flex>
            )
          }
        />
      </div>
    </div>
  )
}

export default DependencyPartnersExplorer
