'use client'

import WaydList from '@/src/components/common/wayd-list'
import WorkStatusTag from '@/src/components/common/work/work-status-tag'
import { useMessage } from '@/src/components/contexts/messaging'
import { StatusCategoryId as TagStatusCategory } from '@/src/components/types'
import {
  IntakeRequestDetailsDto,
  IntakeRequestOutcomeDto,
  IntakeRequestOutcomeKind,
  StatusCategory,
} from '@/src/services/wayd-api'
import { useRemoveIntakeRequestOutcomeMutation } from '@/src/store/features/demand-management/demand-management-api'
import { toApiError } from '@/src/utils'
import { Alert, Button, Flex, Popconfirm, Tag, Typography } from 'antd'
import dayjs from 'dayjs'
import NextLink from 'next/link'
import { FC, useState } from 'react'
import OutcomeDeliverableStatus from '../../../_components/outcome-deliverable-status'
import { outcomeKindLabel } from '../../../_components/outcome-kind'
import SetOutcomeDeliverablesForm from './set-outcome-deliverables-form'

const { Text, Paragraph } = Typography

/** The API names a work status category by code; the tag takes the category's number. */
const tagStatusCategory: Record<StatusCategory, TagStatusCategory> = {
  [StatusCategory.Proposed]: TagStatusCategory.Proposed,
  [StatusCategory.Active]: TagStatusCategory.Active,
  [StatusCategory.Done]: TagStatusCategory.Done,
  [StatusCategory.Removed]: TagStatusCategory.Removed,
}

/** The record the outcome points at, linked to its page, or a note that it has been deleted. */
const OutcomeTarget: FC<{ outcome: IntakeRequestOutcomeDto }> = ({
  outcome,
}) => {
  if (outcome.kind === IntakeRequestOutcomeKind.WorkItem) {
    const workItem = outcome.workItem
    if (!workItem) {
      return <Text type="secondary">A work item that has been deleted</Text>
    }
    return (
      <>
        <NextLink
          href={`/work/workspaces/${workItem.workspaceKey}/work-items/${workItem.key}`}
        >
          {workItem.key} · {workItem.title}
        </NextLink>
        <WorkStatusTag
          status={workItem.status}
          category={tagStatusCategory[workItem.statusCategory]}
        />
      </>
    )
  }

  return outcome.project ? (
    <NextLink href={`/ppm/projects/${outcome.project.key}`}>
      {outcome.project.key} · {outcome.project.name}
    </NextLink>
  ) : (
    <Text type="secondary">A project that has been deleted</Text>
  )
}

/** The deliverables an existing project scope outcome waits on, each with its status. */
const OutcomeDeliverables: FC<{ outcome: IntakeRequestOutcomeDto }> = ({
  outcome,
}) => (
  <Flex vertical gap={2}>
    {outcome.deliverables.map((d) =>
      d.isOnProject && d.statusAlias && d.statusCategory ? (
        <Flex key={d.id} gap={6} align="center" wrap>
          <Text>{d.name}</Text>
          <OutcomeDeliverableStatus
            alias={d.statusAlias}
            category={d.statusCategory}
          />
        </Flex>
      ) : (
        <Text key={d.id} type="secondary">
          A deliverable deleted from the project
        </Text>
      ),
    )}
  </Flex>
)

/**
 * Whether the outcome's deliverables meet the request. Existing project scope that maps none says so, since
 * nothing then tells when the request is met.
 */
const MetTag: FC<{ outcome: IntakeRequestOutcomeDto }> = ({ outcome }) => {
  if (outcome.isMet === true) return <Tag color="success">Met</Tag>
  if (outcome.isMet === false)
    return <Tag color="processing">Waiting on deliverables</Tag>
  if (
    outcome.kind === IntakeRequestOutcomeKind.ExistingProjectScope &&
    outcome.deliverables.length === 0
  )
    return <Tag color="warning">No deliverables mapped</Tag>
  return null
}

export interface IntakeRequestOutcomesProps {
  request: IntakeRequestDetailsDto
}

/**
 * What the request became. Everyone who sees the request sees its outcomes; changing them needs an agent on
 * the desk while the request is accepted, which the server's `canChangeOutcomes` answers.
 */
const IntakeRequestOutcomes: FC<IntakeRequestOutcomesProps> = ({ request }) => {
  const messageApi = useMessage()
  const [removeOutcome, { isLoading: isRemoving }] =
    useRemoveIntakeRequestOutcomeMutation()
  const [editing, setEditing] = useState<IntakeRequestOutcomeDto | null>(null)

  // Reaching it changes nothing on the request: closing it stays the desk's decision.
  const readyToFulfil =
    request.deliverablesMet === true &&
    request.summary.status.category === StatusCategory.Active

  const onRemove = async (targetId: string) => {
    try {
      await removeOutcome({
        requestId: request.summary.id,
        targetId,
      }).unwrap()
      messageApi.success('Outcome removed.')
    } catch (error) {
      const apiError = toApiError(error)
      messageApi.error(apiError.detail ?? 'Failed to remove the outcome.')
    }
  }

  if (request.outcomes.length === 0) {
    return (
      <Paragraph type="secondary" style={{ marginBottom: 0 }}>
        {request.canChangeOutcomes
          ? 'No outcome yet. Add the project or work item this request became.'
          : 'No outcome has been recorded for this request.'}
      </Paragraph>
    )
  }

  return (
    <Flex vertical gap="small">
      {readyToFulfil && (
        <Alert
          type="success"
          showIcon
          title="Every mapped deliverable has been accepted."
          description="This request may be ready to fulfil."
        />
      )}
      <WaydList
        dataSource={request.outcomes}
        rowKey={(outcome) => outcome.targetId}
        renderItem={(outcome) => (
          <WaydList.Item
            actions={
              request.canChangeOutcomes
                ? [
                    ...(outcome.kind ===
                      IntakeRequestOutcomeKind.ExistingProjectScope &&
                    outcome.project
                      ? [
                          <Button
                            key="deliverables"
                            size="small"
                            type="text"
                            onClick={() => setEditing(outcome)}
                          >
                            Edit Deliverables
                          </Button>,
                        ]
                      : []),
                    <Popconfirm
                      key="remove"
                      title="Remove this outcome?"
                      description="The project or work item itself is not changed."
                      okText="Remove"
                      okButtonProps={{ danger: true, loading: isRemoving }}
                      onConfirm={() => onRemove(outcome.targetId)}
                    >
                      <Button size="small" type="text" danger>
                        Remove
                      </Button>
                    </Popconfirm>,
                  ]
                : undefined
            }
          >
            <WaydList.Item.Meta
              title={
                <Flex gap={8} align="center" wrap>
                  <OutcomeTarget outcome={outcome} />
                  <Tag>{outcomeKindLabel[outcome.kind]}</Tag>
                  <MetTag outcome={outcome} />
                </Flex>
              }
              description={
                <Flex vertical gap={4}>
                  {outcome.note && <Text>{outcome.note}</Text>}
                  {outcome.deliverables.length > 0 && (
                    <OutcomeDeliverables outcome={outcome} />
                  )}
                  <Text type="secondary">
                    Added by {outcome.addedBy.name} on{' '}
                    {dayjs(outcome.addedOn).format('MMM D, YYYY')}
                  </Text>
                </Flex>
              }
            />
          </WaydList.Item>
        )}
      />
      {editing && (
        <SetOutcomeDeliverablesForm
          requestId={request.summary.id}
          outcome={editing}
          onFormComplete={() => setEditing(null)}
          onFormCancel={() => setEditing(null)}
        />
      )}
    </Flex>
  )
}

export default IntakeRequestOutcomes
