'use client'

import WaydList from '@/src/components/common/wayd-list'
import { useMessage } from '@/src/components/contexts/messaging'
import { IntakeRequestDetailsDto } from '@/src/services/wayd-api'
import { useRemoveIntakeRequestOutcomeMutation } from '@/src/store/features/demand-management/demand-management-api'
import { toApiError } from '@/src/utils'
import { Button, Flex, Popconfirm, Tag, Typography } from 'antd'
import dayjs from 'dayjs'
import NextLink from 'next/link'
import { FC } from 'react'
import { outcomeKindLabel } from '../../../_components/outcome-kind'

const { Text, Paragraph } = Typography

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
          ? 'No outcome yet. Add the project this request became.'
          : 'No outcome has been recorded for this request.'}
      </Paragraph>
    )
  }

  return (
    <WaydList
      dataSource={request.outcomes}
      rowKey={(outcome) => outcome.targetId}
      renderItem={(outcome) => (
        <WaydList.Item
          actions={
            request.canChangeOutcomes
              ? [
                  <Popconfirm
                    key="remove"
                    title="Remove this outcome?"
                    description="The project itself is not changed."
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
                {outcome.project ? (
                  <NextLink href={`/ppm/projects/${outcome.project.key}`}>
                    {outcome.project.key} · {outcome.project.name}
                  </NextLink>
                ) : (
                  <Text type="secondary">A project that has been deleted</Text>
                )}
                <Tag>{outcomeKindLabel[outcome.kind]}</Tag>
              </Flex>
            }
            description={
              <Flex vertical gap={4}>
                {outcome.note && <Text>{outcome.note}</Text>}
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
  )
}

export default IntakeRequestOutcomes
