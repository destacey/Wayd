'use client'

import WaydList from '@/src/components/common/wayd-list'
import { useMessage } from '@/src/components/contexts/messaging'
import {
  IntakeRequestDetailsDto,
  IntakeRequestSubstanceDto,
} from '@/src/services/wayd-api'
import { useRemoveIntakeRequestParticipantMutation } from '@/src/store/features/demand-management/demand-management-api'
import { toApiError } from '@/src/utils'
import { Button, Flex, Popconfirm, Tag, Typography } from 'antd'
import dayjs from 'dayjs'
import { FC } from 'react'

const { Text, Paragraph } = Typography

export interface IntakeRequestParticipantsProps {
  request: IntakeRequestDetailsDto
  substance: IntakeRequestSubstanceDto
}

interface Person {
  id: string
  name: string
  role: string
  detail?: string
  removable: boolean
}

/**
 * The request's own people. Says what participation means, because "shared with" is easily read as
 * "assigned to". Removing someone needs an agent on the desk, which the server's `canRemoveParticipants`
 * answers.
 */
const IntakeRequestParticipants: FC<IntakeRequestParticipantsProps> = ({
  request,
  substance,
}) => {
  const messageApi = useMessage()
  const [removeParticipant, { isLoading: isRemoving }] =
    useRemoveIntakeRequestParticipantMutation()

  const { summary } = request
  const people: Person[] = [
    {
      id: summary.requester.id,
      name: summary.requester.name,
      role: 'Requester',
      removable: false,
    },
    ...(substance.submittedBy.id !== summary.requester.id
      ? [
          {
            id: substance.submittedBy.id,
            name: substance.submittedBy.name,
            role: 'Submitter',
            removable: false,
          },
        ]
      : []),
    ...substance.participants.map((p) => ({
      id: p.employee.id,
      name: p.employee.name,
      role: 'Participant',
      detail: `Added ${p.addedBy ? `by ${p.addedBy.name} ` : ''}on ${dayjs(p.addedOn).format('MMM D, YYYY')}`,
      removable: request.canRemoveParticipants,
    })),
  ]

  const onRemove = async (employeeId: string) => {
    try {
      await removeParticipant({
        requestId: summary.id,
        employeeId,
      }).unwrap()
      messageApi.success('Participant removed.')
    } catch (error) {
      const apiError = toApiError(error)
      messageApi.error(apiError.detail ?? 'Failed to remove the participant.')
    }
  }

  return (
    <Flex vertical gap={8}>
      <Paragraph type="secondary" style={{ marginBottom: 0 }}>
        Participants can read this request and reply to it. They cannot change
        it, and being a participant does not assign them any work.
      </Paragraph>
      <WaydList
        dataSource={people}
        rowKey={(person) => `${person.role}-${person.id}`}
        renderItem={(person) => (
          <WaydList.Item
            actions={
              person.removable
                ? [
                    <Popconfirm
                      key="remove"
                      title={`Remove ${person.name}?`}
                      description="They lose access from now on. What they have already read stays read."
                      okText="Remove"
                      okButtonProps={{ danger: true, loading: isRemoving }}
                      onConfirm={() => onRemove(person.id)}
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
                <Flex gap={8} align="center">
                  <Text>{person.name}</Text>
                  <Tag>{person.role}</Tag>
                </Flex>
              }
              description={person.detail}
            />
          </WaydList.Item>
        )}
      />
    </Flex>
  )
}

export default IntakeRequestParticipants
