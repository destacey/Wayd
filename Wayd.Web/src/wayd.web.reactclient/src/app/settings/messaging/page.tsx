'use client'

import PageTitle from '@/src/components/common/page-title'
import {
  WaydGrid,
  createActionsColumn,
} from '@/src/components/common/wayd-grid'
import MetricCard from '@/src/components/common/metrics/metric-card'
import { useMemo, useState } from 'react'
import { Button, Flex, Typography } from 'antd'
import type { ColumnDef } from '@tanstack/react-table'
import type { ItemType } from 'antd/es/menu/interface'
import { authorizePage } from '../../../components/hoc'
import useAuth from '../../../components/contexts/auth'
import { useDocumentTitle } from '../../../hooks'
import { DeadLetterMessageResponse } from '@/src/services/wayd-api'
import {
  useGetDeadLettersQuery,
  useGetMessagingCountsQuery,
} from '@/src/store/features/admin/messaging-api'
import DeadLetterDetailsDrawer from './_components/dead-letter-details-drawer'
import useDeadLetterActions, {
  shortTypeName,
} from './_components/use-dead-letter-actions'

// The server caps a dead letter query page at 500. Fetch one max-size page and
// let the grid filter/sort client-side; the tile + overflow note carry the true
// total when the queue is (pathologically) deeper than that.
const DEAD_LETTER_PAGE_SIZE = 500

const MessagingPage = () => {
  useDocumentTitle('Messaging')
  const [viewingDeadLetterId, setViewingDeadLetterId] = useState<string | null>(
    null,
  )
  const [drawerOpen, setDrawerOpen] = useState(false)

  const { hasPermissionClaim } = useAuth()
  const canReplay = hasPermissionClaim('Permissions.Messaging.Run')
  const canDiscard = hasPermissionClaim('Permissions.Messaging.Delete')
  const showRowActions = canReplay || canDiscard

  const {
    data: counts,
    isLoading: countsLoading,
    refetch: refetchCounts,
  } = useGetMessagingCountsQuery()

  const {
    data: deadLetters,
    isLoading: deadLettersLoading,
    refetch: refetchDeadLetters,
  } = useGetDeadLettersQuery({ pageSize: DEAD_LETTER_PAGE_SIZE })

  const { handleReplay, handleDiscard } = useDeadLetterActions()

  const refresh = () => {
    refetchCounts()
    refetchDeadLetters()
  }

  const closeDetailsDrawer = () => {
    setDrawerOpen(false)
    setViewingDeadLetterId(null)
  }

  const columns = useMemo<ColumnDef<DeadLetterMessageResponse, any>[]>(() => {
    const openDetailsDrawer = (id: string) => {
      setViewingDeadLetterId(id)
      setDrawerOpen(true)
    }

    return [
      createActionsColumn<DeadLetterMessageResponse>({
        hide: !showRowActions,
        ariaLabel: 'Dead letter message actions',
        getItems: (deadLetter) => {
          const items: ItemType[] = []

          if (canReplay) {
            items.push({
              key: 'replay',
              label: 'Replay',
              onClick: () => handleReplay(deadLetter),
            })
          }

          if (canDiscard) {
            if (items.length > 0) {
              items.push({ key: 'divider', type: 'divider' })
            }
            items.push({
              key: 'discard',
              label: 'Discard',
              danger: true,
              onClick: () => handleDiscard(deadLetter),
            })
          }

          return items
        },
      }),
      {
        id: 'messageType',
        accessorFn: (row) => shortTypeName(row.messageType),
        header: 'Message Type',
        size: 220,
        meta: { filterType: 'set' },
        cell: ({ row }) => (
          <Button
            type="link"
            style={{ padding: 0, height: 'auto', fontSize: 'inherit' }}
            onClick={() => openDetailsDrawer(row.original.id)}
          >
            {shortTypeName(row.original.messageType)}
          </Button>
        ),
      },
      {
        id: 'exceptionType',
        accessorFn: (row) => shortTypeName(row.exceptionType),
        header: 'Exception Type',
        size: 200,
        meta: { filterType: 'set' },
      },
      {
        id: 'exceptionMessage',
        accessorKey: 'exceptionMessage',
        header: 'Exception Message',
        size: 320,
      },
      {
        id: 'sentAt',
        accessorKey: 'sentAt',
        header: 'Sent At',
        meta: { columnType: 'dateTime' },
      },
      {
        id: 'attempts',
        accessorKey: 'attempts',
        header: 'Attempts',
        size: 100,
      },
      {
        id: 'replayable',
        accessorKey: 'replayable',
        header: 'Queued for Replay',
        size: 150,
        meta: { columnType: 'yesNo' },
      },
      {
        id: 'receivedAt',
        accessorKey: 'receivedAt',
        header: 'Queue',
        size: 180,
        meta: { filterType: 'set' },
      },
    ]
  }, [showRowActions, canReplay, canDiscard, handleReplay, handleDiscard])

  const deadLetterOverflow =
    deadLetters && deadLetters.totalCount > deadLetters.items.length

  return (
    <>
      <PageTitle title="Messaging" />
      <Flex gap={12} wrap style={{ marginBottom: 16 }}>
        <MetricCard
          title="Incoming"
          value={counts?.incoming ?? 0}
          loading={countsLoading}
          tooltip="Durably persisted messages waiting to be processed. Only appears when delivery backs up — a burst of messages, database latency, or messages recovered after an unclean shutdown. Zero is the healthy state; successfully processed messages pass through too quickly to see."
        />
        <MetricCard
          title="Scheduled"
          value={counts?.scheduled ?? 0}
          loading={countsLoading}
          tooltip="Messages scheduled for a future time — mostly failed messages waiting out a retry cooldown (1s / 5s / 15s between attempts). Visible for the ~20 seconds a message spends retrying before it either succeeds or dead-letters."
        />
        <MetricCard
          title="Outbox"
          value={counts?.outgoing ?? 0}
          loading={countsLoading}
          tooltip="Outgoing messages committed with a transaction but not yet dispatched. Only appears during a dispatch backlog or after an unclean shutdown. Zero is the healthy state."
        />
        <MetricCard
          title="Dead Letters"
          value={counts?.deadLetter ?? 0}
          loading={countsLoading}
          valueStyle={
            counts?.deadLetter
              ? { color: 'var(--ant-color-error)' }
              : undefined
          }
          tooltip="Messages that failed every retry attempt. Unlike the other buckets, these persist until you replay or discard them below — this is the tile to watch."
        />
        <MetricCard
          title="Handled"
          value={counts?.handled ?? 0}
          loading={countsLoading}
          tooltip="Successfully processed messages kept briefly (5 minutes) for duplicate detection, then purged. Messages executed in-memory are never persisted at all, so this is usually zero — successful work leaves no trace here. See the application logs for message history."
        />
      </Flex>
      <WaydGrid
        columns={columns}
        data={deadLetters?.items}
        onRefresh={refresh}
        isLoading={deadLettersLoading}
        persistStateKey="settings-messaging-dead-letters"
        csvFileName="dead-letter-messages"
        initialSorting={[{ id: 'sentAt', desc: true }]}
        emptyMessage="The dead letter queue is empty."
        leftSlot={
          deadLetterOverflow ? (
            <Typography.Text type="warning">
              {`Showing the first ${deadLetters.items.length} of ${deadLetters.totalCount} dead letter messages.`}
            </Typography.Text>
          ) : undefined
        }
      />
      {viewingDeadLetterId !== null && (
        <DeadLetterDetailsDrawer
          deadLetterId={viewingDeadLetterId}
          drawerOpen={drawerOpen}
          onDrawerClose={closeDetailsDrawer}
        />
      )}
    </>
  )
}

const PageWithAuthorization = authorizePage(
  MessagingPage,
  'Permission',
  'Permissions.Messaging.View',
)

export default PageWithAuthorization
