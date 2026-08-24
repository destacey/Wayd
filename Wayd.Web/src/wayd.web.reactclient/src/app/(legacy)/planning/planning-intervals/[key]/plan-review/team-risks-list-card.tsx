'use client'

import { RiskListDto } from '@/src/services/wayd-api'
import { PlusOutlined } from '@ant-design/icons'
import { Badge, Button, Card, Space } from 'antd'
import RiskListItem from './risk-list-item'
import WaydEmpty from '@/src/components/common/wayd-empty'
import WaydList from '@/src/components/common/wayd-list'
import { useState } from 'react'
import CreateRiskForm from '@/src/components/common/planning/create-risk-form'
import useTheme from '@/src/components/contexts/theme'

export interface TeamRisksListCardProps {
  risks: RiskListDto[]
  teamId: string
  canCreateRisks: boolean
  canUpdateRisks: boolean
  refreshRisks: () => void
}

const categoryOrder = ['Owned', 'Accepted', 'Mitigated', 'Resolved']
const exposureOrder = ['High', 'Medium', 'Low']

const TeamRisksListCard = ({
  risks,
  teamId,
  canCreateRisks,
  canUpdateRisks,
  refreshRisks,
}: TeamRisksListCardProps) => {
  const [openCreateRiskForm, setOpenCreateRiskForm] = useState<boolean>(false)
  const theme = useTheme()

  const cardTitle = (() => {
    const count = risks?.length ?? 0
    const showBadge = count > 0
    return (
      <Space>
        {'Risks'}
        {showBadge && (
          <Badge color={theme.badgeColor} size="small" count={count} />
        )}
      </Space>
    )
  })()

  const risksList = (() => {
    const sortedRisks = risks?.slice().sort((a, b) => {
      const aIndex = categoryOrder.indexOf(a.category)
      const bIndex = categoryOrder.indexOf(b.category)
      if (aIndex !== bIndex) {
        return aIndex - bIndex
      } else {
        const aExposureIndex = exposureOrder.indexOf(a.exposure)
        const bExposureIndex = exposureOrder.indexOf(b.exposure)
        return aExposureIndex - bExposureIndex
      }
    })

    return (
      <WaydList
        size="small"
        dataSource={sortedRisks}
        locale={{
          emptyText: <WaydEmpty message="No risks" />,
        }}
        renderItem={(risk) => (
          <RiskListItem
            risk={risk}
            canUpdateRisks={canUpdateRisks}
            refreshRisks={refreshRisks}
          />
        )}
      />
    )
  })()

  const onCreateRiskFormClosed = (wasCreated: boolean) => {
    setOpenCreateRiskForm(false)
    if (wasCreated) {
      refreshRisks()
    }
  }

  return (
    <>
      <Card
        size="small"
        title={cardTitle}
        extra={
          canCreateRisks && (
            <Button
              type="text"
              icon={<PlusOutlined />}
              onClick={() => setOpenCreateRiskForm(true)}
            />
          )
        }
        styles={{ body: { padding: 4 } }}
      >
        {risksList}
      </Card>
      {openCreateRiskForm && (
        <CreateRiskForm
          createForTeamId={teamId}
          onFormCreate={() => onCreateRiskFormClosed(true)}
          onFormCancel={() => onCreateRiskFormClosed(false)}
        />
      )}
    </>
  )
}

export default TeamRisksListCard
