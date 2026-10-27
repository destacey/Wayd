'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { IntakeRequestDetailsDto } from '@/src/services/wayd-api'
import {
  useChangeIntakeRequestStatusMutation,
  useGetIntakeRequestStatusOptionsQuery,
} from '@/src/store/features/demand-management/demand-management-api'
import { toApiError } from '@/src/utils'
import { DownOutlined } from '@ant-design/icons'
import { Button, Dropdown } from 'antd'
import { FC, useState } from 'react'

export interface MoveIntakeRequestButtonProps {
  request: IntakeRequestDetailsDto
  /** Opens the status form on a move that needs more than a click: a reason, or the surviving request. */
  onNeedsDetail: (statusId: string) => void
}

/**
 * The request's workflow at hand: every status it can move to, in the workflow's order. A move that needs
 * nothing happens on the click; one that needs a reason the requester reads, or the request a duplicate
 * gives way to, opens the status form on that status. The statuses are fetched when the menu first opens.
 */
const MoveIntakeRequestButton: FC<MoveIntakeRequestButtonProps> = ({
  request,
  onNeedsDetail,
}) => {
  const messageApi = useMessage()
  const [opened, setOpened] = useState(false)
  const { data: options, isFetching } = useGetIntakeRequestStatusOptionsQuery(
    request.summary.id,
    { skip: !opened },
  )
  const [changeStatus, { isLoading: isMoving }] =
    useChangeIntakeRequestStatusMutation()

  const move = async (statusId: string) => {
    const option = options?.find((o) => o.id === statusId)
    if (!option) return
    if (option.needsReason || option.needsSurvivingRequest) {
      onNeedsDetail(statusId)
      return
    }
    try {
      await changeStatus({ requestId: request.summary.id, statusId }).unwrap()
      messageApi.success(`Moved to ${option.name}.`)
    } catch (error) {
      messageApi.error(
        toApiError(error).detail ?? 'Failed to change the status.',
      )
    }
  }

  return (
    <Dropdown
      trigger={['click']}
      onOpenChange={(open) => open && setOpened(true)}
      menu={{
        items: isFetching
          ? [{ key: 'loading', label: 'Loading…', disabled: true }]
          : (options ?? []).map((o) => ({
              key: o.id,
              label: o.name,
            })),
        onClick: ({ key }) => move(key),
      }}
    >
      <Button loading={isMoving}>
        Move to <DownOutlined />
      </Button>
    </Dropdown>
  )
}

export default MoveIntakeRequestButton
