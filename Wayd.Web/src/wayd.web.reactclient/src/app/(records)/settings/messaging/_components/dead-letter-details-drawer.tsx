'use client'

import { useGetDeadLetterByIdQuery } from '@/src/store/features/admin/messaging-api'
import useAuth from '@/src/components/contexts/auth'
import { useMessage } from '@/src/components/contexts/messaging'
import { getDrawerWidthPixels } from '@/src/utils'
import { Button, Drawer, Flex, Space } from 'antd'
import { FC, useEffect, useState } from 'react'
import { LabeledContent } from '@/src/components/common/content'
import useDeadLetterActions, { shortTypeName } from './use-dead-letter-actions'

export interface DeadLetterDetailsDrawerProps {
  deadLetterId: string
  drawerOpen: boolean
  onDrawerClose: () => void
}

/** Pretty-prints the envelope body when it is valid JSON; falls back to the raw text. */
const formatBody = (body?: string | null): string | undefined => {
  if (!body) return undefined
  try {
    return JSON.stringify(JSON.parse(body), null, 2)
  } catch {
    return body
  }
}

const DeadLetterDetailsDrawer: FC<DeadLetterDetailsDrawerProps> = ({
  deadLetterId,
  drawerOpen,
  onDrawerClose,
}) => {
  const [size, setSize] = useState(() => getDrawerWidthPixels())
  const messageApi = useMessage()
  const { hasPermissionClaim } = useAuth()

  const canReplay = hasPermissionClaim('Permissions.Messaging.Run')
  const canDiscard = hasPermissionClaim('Permissions.Messaging.Delete')

  const {
    data: deadLetter,
    isLoading,
    error,
  } = useGetDeadLetterByIdQuery(deadLetterId)

  const { handleReplay, handleDiscard } = useDeadLetterActions()

  useEffect(() => {
    if (error) {
      messageApi.error(
        'An error occurred while loading the dead letter message. Please try again.',
      )
    }
  }, [error, messageApi])

  const body = formatBody(deadLetter?.body)

  const extraActions =
    deadLetter && (canReplay || canDiscard) ? (
      <Space>
        {canReplay && (
          <Button onClick={() => handleReplay(deadLetter, onDrawerClose)}>
            Replay
          </Button>
        )}
        {canDiscard && (
          <Button
            danger
            onClick={() => handleDiscard(deadLetter, onDrawerClose)}
          >
            Discard
          </Button>
        )}
      </Space>
    ) : undefined

  return (
    <Drawer
      title={
        deadLetter
          ? shortTypeName(deadLetter.messageType)
          : 'Dead Letter Message'
      }
      placement="right"
      onClose={onDrawerClose}
      open={drawerOpen}
      loading={isLoading}
      size={size}
      resizable={{
        onResize: (newSize) => setSize(newSize),
      }}
      destroyOnHidden={true}
      extra={extraActions}
    >
      <Flex vertical gap={10}>
        <LabeledContent label="Message Type">
          {deadLetter?.messageType}
        </LabeledContent>
        <LabeledContent label="Exception Type">
          {deadLetter?.exceptionType}
        </LabeledContent>
        <LabeledContent label="Exception Message">
          {deadLetter?.exceptionMessage}
        </LabeledContent>
        <LabeledContent label="Sent At">
          {deadLetter?.sentAt
            ? // The NSwag axios client doesn't revive date strings into Date
              // instances, so sentAt is an ISO string at runtime despite the
              // typed Date — convert before formatting.
              new Date(deadLetter.sentAt).toLocaleString()
            : undefined}
        </LabeledContent>
        <LabeledContent label="Attempts">{deadLetter?.attempts}</LabeledContent>
        <LabeledContent label="Queued for Replay">
          {deadLetter?.replayable ? 'Yes' : 'No'}
        </LabeledContent>
        {deadLetter?.source && (
          <LabeledContent label="Source">{deadLetter.source}</LabeledContent>
        )}
        {deadLetter?.receivedAt && (
          <LabeledContent label="Received At (Queue)">
            {deadLetter.receivedAt}
          </LabeledContent>
        )}
        {deadLetter?.destination && (
          <LabeledContent label="Destination">
            {deadLetter.destination}
          </LabeledContent>
        )}
        {deadLetter?.correlationId && (
          <LabeledContent label="Correlation Id">
            {deadLetter.correlationId}
          </LabeledContent>
        )}
        {body && (
          <LabeledContent label="Message Body">
            <pre
              style={{
                margin: 0,
                padding: '8px 12px',
                borderRadius: 'var(--ant-border-radius)',
                backgroundColor: 'var(--ant-color-fill-quaternary)',
                overflowX: 'auto',
                fontSize: 'var(--ant-font-size-sm)',
              }}
            >
              {body}
            </pre>
          </LabeledContent>
        )}
      </Flex>
    </Drawer>
  )
}

export default DeadLetterDetailsDrawer
