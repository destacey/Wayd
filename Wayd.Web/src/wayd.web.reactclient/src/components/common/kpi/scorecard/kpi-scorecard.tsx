'use client'

import { ControlItemSwitch } from '@/src/components/common/control-items-menu'
import WaydEmpty from '@/src/components/common/wayd-empty'
import { KpiSummaryDto } from '@/src/services/api/wayd.schemas'
import {
  KpiSubject,
  useGetClosedKpisQuery,
  useGetKpiScorecardQuery,
} from '@/src/store/features/common/kpis-api'
import { Alert, Button, Col, Flex, Row, Skeleton, Typography } from 'antd'
import { useState } from 'react'
import KpiDetailsDrawer from './kpi-details-drawer'
import KpiForm from './kpi-form'
import ScorecardKpiCard from './scorecard-kpi-card'

const { Text } = Typography

const UNCATEGORIZED = 'uncategorized'

interface KpiGroup {
  id: string
  name: string
  kpis: KpiSummaryDto[]
}

/**
 * The KPIs in scorecard order, grouped by category. The server orders them by
 * category, then position, so a group starts wherever the category changes.
 */
function groupByCategory(kpis: KpiSummaryDto[]): KpiGroup[] {
  const groups: KpiGroup[] = []
  for (const kpi of kpis) {
    const id = kpi.category?.id ?? UNCATEGORIZED
    const last = groups[groups.length - 1]
    if (last?.id === id) last.kpis.push(kpi)
    else
      groups.push({
        id,
        name: kpi.category?.name ?? 'Uncategorized',
        kpis: [kpi],
      })
  }
  return groups
}

export interface KpiScorecardProps {
  subject: KpiSubject
}

/**
 * A record's scorecard: every active KPI on it as a card, grouped by category,
 * with its closed KPIs a switch away. Opening a KPI shows its history.
 */
const KpiScorecard = ({ subject }: KpiScorecardProps) => {
  const [showClosed, setShowClosed] = useState(false)
  const [openKpiId, setOpenKpiId] = useState<string | null>(null)
  const [adding, setAdding] = useState(false)

  const { data, isLoading, error, refetch } = useGetKpiScorecardQuery(subject)
  const { data: closed, isLoading: closedLoading } = useGetClosedKpisQuery(
    subject,
    {
      skip: !showClosed,
    },
  )

  if (isLoading) return <Skeleton active />

  if (error || !data)
    return (
      <Alert
        type="error"
        showIcon
        title="The scorecard could not be loaded."
        action={
          <Button size="small" onClick={refetch}>
            Retry
          </Button>
        }
      />
    )

  const kpis = showClosed ? (closed ?? []) : data.kpis
  const groups = groupByCategory(kpis)

  return (
    <Flex vertical gap={16}>
      <Flex justify="space-between" align="center" gap={12} wrap>
        <ControlItemSwitch
          label="Show closed KPIs"
          checked={showClosed}
          onChange={setShowClosed}
        />
        {data.canManage && (
          <Button type="primary" onClick={() => setAdding(true)}>
            Add KPI
          </Button>
        )}
      </Flex>

      {showClosed && closedLoading ? (
        <Skeleton active />
      ) : groups.length === 0 ? (
        <WaydEmpty
          message={
            showClosed
              ? 'No closed KPIs'
              : data.canManage
                ? 'No KPIs yet. Add one to say what this team is aiming for.'
                : 'No KPIs yet'
          }
        />
      ) : (
        groups.map((group) => (
          <Flex
            key={group.id}
            component="section"
            aria-label={group.name}
            vertical
            gap={8}
          >
            <Text strong style={{ fontSize: 14 }}>
              {group.name}
            </Text>
            <Row gutter={[12, 12]}>
              {group.kpis.map((kpi) => (
                <Col key={kpi.id} xs={24} sm={12} xl={8} xxl={6}>
                  <ScorecardKpiCard
                    kpi={kpi}
                    onOpen={(k) => setOpenKpiId(k.id)}
                  />
                </Col>
              ))}
            </Row>
          </Flex>
        ))
      )}

      {adding && (
        <KpiForm
          subject={subject}
          onFormComplete={() => setAdding(false)}
          onFormCancel={() => setAdding(false)}
        />
      )}
      {openKpiId && (
        <KpiDetailsDrawer
          subject={subject}
          kpiId={openKpiId}
          onClose={() => setOpenKpiId(null)}
        />
      )}
    </Flex>
  )
}

export default KpiScorecard
