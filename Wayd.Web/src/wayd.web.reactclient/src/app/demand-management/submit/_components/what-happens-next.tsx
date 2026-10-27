'use client'

import {
  IntakeWorkflowStatusDto,
  StatusCategory,
} from '@/src/services/wayd-api'
import { useGetIntakeDeskWorkflowQuery } from '@/src/store/features/demand-management/demand-management-api'
import { Card, Flex, Skeleton, Timeline, Typography } from 'antd'
import { FC } from 'react'

const { Text, Paragraph } = Typography

const CATEGORY_COLOR: Partial<Record<StatusCategory, string>> = {
  [StatusCategory.Proposed]: 'gray',
  [StatusCategory.Active]: 'blue',
  [StatusCategory.Done]: 'green',
}

/**
 * The stages a request on the desk can pass through on its way to being done, in the workflow's order.
 * The closing stages — declined, a duplicate, withdrawn — are left to one line after them, since a
 * requester reading the path wants where it is meant to go.
 */
export const journeyOf = (statuses: IntakeWorkflowStatusDto[]) => ({
  stages: statuses.filter((s) => s.category !== StatusCategory.Removed),
  canClose: statuses.some((s) => s.category === StatusCategory.Removed),
})

export interface WhatHappensNextProps {
  /** The desk the request is going to, whose workflow is the path described. */
  deskId: string
}

/**
 * Beside the form: the path a request on this desk takes after it is submitted, in its workflow's own
 * stages and the descriptions its author wrote for them, and why the form asks what makes the date.
 */
const WhatHappensNext: FC<WhatHappensNextProps> = ({ deskId }) => {
  const { data, isLoading } = useGetIntakeDeskWorkflowQuery(deskId)
  const journey = data ? journeyOf(data.statuses) : undefined

  return (
    <Flex vertical gap={16}>
      {isLoading && <Skeleton active paragraph={{ rows: 4 }} />}
      {journey && journey.stages.length > 0 && (
        <Card size="small" title="What happens next">
          <Timeline
            items={journey.stages.map((s) => ({
              key: s.id,
              color: CATEGORY_COLOR[s.category] ?? 'gray',
              content: (
                <Flex vertical>
                  <Text strong>{s.name}</Text>
                  {s.description && (
                    <Text type="secondary">{s.description}</Text>
                  )}
                </Flex>
              ),
            }))}
          />
          {journey.canClose && (
            <Text type="secondary">
              It can also be declined, with a reason you will see.
            </Text>
          )}
        </Card>
      )}
      <Card size="small" title="Why we ask about the date">
        <Paragraph type="secondary" style={{ marginBottom: 0 }}>
          Two requests can both say 31 March. One is a contract term that will
          not move; the other is a date someone picked in a meeting. Reviewers
          need to tell them apart before planning a quarter around either.
        </Paragraph>
      </Card>
    </Flex>
  )
}

export default WhatHappensNext
