'use client'

import {
  StrategicInitiativeKpiCheckpointDetailsDto,
  StrategicInitiativeKpiListDto,
} from '@/src/services/wayd-api'
import { useGetStrategicInitiativeKpiCheckpointPlanQuery } from '@/src/store/features/ppm/strategic-initiatives-api'
import { AppstoreOutlined, MenuOutlined } from '@ant-design/icons'
import { Flex, Segmented, Spin } from 'antd'
import styles from './strategic-initiative-kpi-view-manager.module.css'
import { FC, memo, useState } from 'react'

import {
  StrategicInitiativeKpisGrid,
  StrategicInitiativeKpiDetailsDrawer,
} from '.'
import {
  AddKpiCard,
  KpiCard,
  KpiCardCheckpoint,
} from '@/src/components/common/kpi'

export interface StrategicInitiativeKpiViewManagerProps {
  strategicInitiativeId: string
  kpis: StrategicInitiativeKpiListDto[] | undefined
  canManageKpis: boolean
  isLoading: boolean
  refetch: () => void
  gridHeight?: number
  isReadOnly?: boolean
  onCreateKpi?: () => void
}

// ─── Checkpoint mapping ───────────────────────────────────────────────────────

function toKpiCardCheckpoint(
  cp: StrategicInitiativeKpiCheckpointDetailsDto,
): KpiCardCheckpoint {
  return {
    label: cp.dateLabel,
    date: new Date(cp.checkpointDate).toISOString(),
    targetValue: cp.targetValue,
    actualValue: cp.measurement?.actualValue,
    health: cp.health,
    trend: cp.trend,
  }
}

// ─── Per-card wrapper that fetches its own checkpoint plan ────────────────────

interface KpiCardWithCheckpointsProps {
  strategicInitiativeId: string
  kpi: StrategicInitiativeKpiListDto
  onPress: (id: string) => void
}

const KpiCardWithCheckpoints: FC<KpiCardWithCheckpointsProps> = ({
  strategicInitiativeId,
  kpi,
  onPress,
}) => {
  const { data: checkpointPlan, isLoading: checkpointLoading } =
    useGetStrategicInitiativeKpiCheckpointPlanQuery({
      strategicInitiativeId,
      kpiId: kpi.id,
    })

  const checkpoints = checkpointPlan?.map(toKpiCardCheckpoint)

  return (
    <KpiCard
      data={kpi}
      checkpoints={checkpoints}
      checkpointLoading={checkpointLoading}
      onPress={onPress}
    />
  )
}

// ─── View manager ─────────────────────────────────────────────────────────────

const viewOptions = [
  {
    value: 'Card',
    icon: <AppstoreOutlined title="Card view" />,
  },
  {
    value: 'List',
    icon: <MenuOutlined title="List view" />,
  },
]

const StrategicInitiativeKpiViewManager: FC<
  StrategicInitiativeKpiViewManagerProps
> = ({
  strategicInitiativeId,
  kpis,
  canManageKpis,
  isLoading,
  refetch,
  gridHeight = 400,
  isReadOnly,
  onCreateKpi,
}) => {
  const [currentView, setCurrentView] = useState<string | number>('Card')
  const [selectedKpiId, setSelectedKpiId] = useState<string | null>(null)
  const [openKpiDetailsDrawer, setOpenKpiDetailsDrawer] = useState(false)

  const onViewDetails = (id: string) => {
    setSelectedKpiId(id)
    setOpenKpiDetailsDrawer(true)
  }

  const viewSelector = (
      <Segmented
        options={viewOptions}
        value={currentView}
        onChange={setCurrentView}
      />
    )

  return (
    <>
      {currentView === 'Card' && (
        <Flex vertical gap="small">
          <Flex justify="flex-end">{viewSelector}</Flex>
          {isLoading ? (
            <Flex justify="center" style={{ padding: 24 }}>
              <Spin />
            </Flex>
          ) : (
            <div className={styles.grid}>
              {(kpis ?? []).map((kpi) => (
                <KpiCardWithCheckpoints
                  key={kpi.id}
                  strategicInitiativeId={strategicInitiativeId}
                  kpi={kpi}
                  onPress={onViewDetails}
                />
              ))}
              {!isReadOnly &&
                canManageKpis &&
                onCreateKpi &&
                (kpis?.length ?? 0) === 0 && (
                  <AddKpiCard onClick={onCreateKpi} />
                )}
            </div>
          )}
        </Flex>
      )}

      {currentView === 'List' && (
        <StrategicInitiativeKpisGrid
          strategicInitiativeId={strategicInitiativeId}
          kpis={kpis ?? []}
          canManageKpis={canManageKpis}
          isLoading={isLoading}
          refetch={refetch}
          gridHeight={gridHeight}
          isReadOnly={isReadOnly}
          viewSelector={viewSelector}
        />
      )}

      {selectedKpiId && (
        <StrategicInitiativeKpiDetailsDrawer
          strategicInitiativeId={strategicInitiativeId}
          kpiId={selectedKpiId}
          drawerOpen={openKpiDetailsDrawer}
          onDrawerClose={() => {
            setOpenKpiDetailsDrawer(false)
            setSelectedKpiId(null)
          }}
          canManageKpis={canManageKpis && !isReadOnly}
          onRefresh={refetch}
        />
      )}
    </>
  )
}

export default memo(StrategicInitiativeKpiViewManager)
