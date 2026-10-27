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
 * The intake status aliases that are detours rather than steps: open, but off the path to done. Values of
 * the server's `IntakeRequestStatusAlias`, which the generated client carries only as a number; matched by
 * meaning so a desk that renames them still has them set aside.
 */
const WAITING_ON_REQUESTER = 2
const DEFERRED = 5

/**
 * The stages a request on the desk passes through on its way to being done, in the workflow's order. The
 * detours (waiting on the requester, deferred) and the closing stages (declined, a duplicate, withdrawn)
 * are left to lines after them, since a requester reading the path wants where it is meant to go.
 */
export const journeyOf = (statuses: IntakeWorkflowStatusDto[]) => ({
  stages: statuses.filter(
    (s) =>
      s.category !== StatusCategory.Removed &&
      s.alias !== WAITING_ON_REQUESTER &&
      s.alias !== DEFERRED,
  ),
  canAskRequester: statuses.some((s) => s.alias === WAITING_ON_REQUESTER),
  canDefer: statuses.some((s) => s.alias === DEFERRED),
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
          <Flex vertical gap={4}>
            {journey.canAskRequester && (
              <Text type="secondary">
                The desk may ask you a question along the way, and waits for
                your answer.
              </Text>
            )}
            {journey.canDefer && (
              <Text type="secondary">
                It can be deferred: worth doing, but not now.
              </Text>
            )}
            {journey.canClose && (
              <Text type="secondary">
                It can also be declined, with a reason you will see.
              </Text>
            )}
          </Flex>
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
