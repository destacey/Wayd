'use client'

import { FC } from 'react'
import Link from 'next/link'
import { Card, Flex, Typography } from 'antd'
import WaydDateRange from '@/src/components/common/wayd-date-range'
import useTheme from '@/src/components/contexts/theme'
import './planning-interval-iterations-list-item.css'
import { PlanningIntervalIterationListDto } from '@/src/services/wayd-api'

const { Title, Text } = Typography

interface PlanningIntervalIterationsListItemProps {
  iteration: PlanningIntervalIterationListDto
  planningIntervalKey: number
}

const PlanningIntervalIterationsListItem: FC<
  PlanningIntervalIterationsListItemProps
> = ({ iteration, planningIntervalKey }) => {
  const { token } = useTheme()

  const isActive = iteration.state === 'Active'

  return (
    <Link
      href={`/planning/planning-intervals/${planningIntervalKey}/iterations/${iteration.key}`}
      aria-current={isActive ? 'true' : undefined}
      className="pi-iteration-link"
    >
      <Card
        size="small"
        hoverable
        className="pi-iteration-card"
        styles={{
          body: {
            padding: token.paddingSM,
            backgroundColor: isActive ? token.colorPrimaryBg : undefined,
          },
        }}
      >
        <Flex justify="space-between" align="center">
          <Flex vertical>
            <Title
              level={5}
              style={{
                margin: 0,
                color: isActive ? token.colorPrimaryText : token.colorText,
              }}
            >
              {iteration.name}
            </Title>
            <Text type="secondary">{iteration.category.name}</Text>
          </Flex>

          <Flex align="center" gap={token.marginXS}>
            <WaydDateRange dateRange={iteration} />
          </Flex>
        </Flex>
      </Card>
    </Link>
  )
}

export default PlanningIntervalIterationsListItem
