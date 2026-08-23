'use client'

import { FC } from 'react'
import { Card, Flex, Skeleton, Typography } from 'antd'
import { useGetPlanningIntervalIterationsQuery } from '@/src/store/features/planning/planning-interval-api'
import { PlanningIntervalIterationsListItem } from '.'

const { Title } = Typography

interface PlanningIntervalIterationsListProps {
  planningIntervalKey: number
}

const PlanningIntervalIterationsList: FC<
  PlanningIntervalIterationsListProps
> = ({ planningIntervalKey }) => {
  const { data: iterations, isLoading } = useGetPlanningIntervalIterationsQuery(
    planningIntervalKey,
    { skip: !planningIntervalKey },
  )

  if (isLoading) return <Skeleton active />

  if (!iterations?.length) return null

  return (
    <Card size="small" variant="borderless">
      <Title level={5} style={{ marginTop: 0 }}>
        Iterations
      </Title>
      <Flex vertical gap={4}>
        {iterations.map((iteration) => (
          <PlanningIntervalIterationsListItem
            key={iteration.key}
            iteration={iteration}
            planningIntervalKey={planningIntervalKey}
          />
        ))}
      </Flex>
    </Card>
  )
}

export default PlanningIntervalIterationsList
