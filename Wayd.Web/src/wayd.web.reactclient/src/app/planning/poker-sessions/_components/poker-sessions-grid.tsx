'use client'

import {
  WaydGrid,
  createActionsColumn,
} from '@/src/components/common/wayd-grid'
import { ControlItemsMenu } from '@/src/components/common/control-items-menu'
import { PokerSessionListDto } from '@/src/services/wayd-api'
import type { ColumnDef } from '@/src/components/common/wayd-grid-core'
import { ItemType } from 'antd/es/menu/interface'
import Link from 'next/link'
import { FC, useMemo } from 'react'

export interface PokerSessionsGridProps {
  sessions: PokerSessionListDto[]
  isLoading: boolean
  refetch: () => void
  gridControlMenuItems?: ItemType[]
  onEditClicked: (session: PokerSessionListDto) => void
  onCompleteClicked: (session: PokerSessionListDto) => void
  onDeleteClicked: (session: PokerSessionListDto) => void
}

interface RowMenuProps {
  session: PokerSessionListDto
  onEditClicked: (session: PokerSessionListDto) => void
  onCompleteClicked: (session: PokerSessionListDto) => void
  onDeleteClicked: (session: PokerSessionListDto) => void
}

const getRowMenuItems = (props: RowMenuProps): ItemType[] => {
  if (!props.session) return []

  // Only the session's facilitators, or a poker administrator, run it.
  if (!props.session.canManage) return []

  const items: ItemType[] = []

  if (props.session.status === 'Active') {
    items.push({
      key: 'edit',
      label: 'Edit',
      onClick: () => props.onEditClicked(props.session),
    })
    items.push({
      key: 'complete',
      label: 'Complete',
      onClick: () => props.onCompleteClicked(props.session),
    })
  }

  items.push({
    key: 'delete',
    label: 'Delete',
    danger: true,
    onClick: () => props.onDeleteClicked(props.session),
  })

  return items
}

const PokerSessionsGrid: FC<PokerSessionsGridProps> = ({
  sessions = [],
  isLoading,
  refetch,
  gridControlMenuItems,
  onEditClicked,
  onCompleteClicked,
  onDeleteClicked,
}) => {
  const showRowMenu = sessions.some((s) => s.canManage)

  const columns = useMemo<ColumnDef<PokerSessionListDto, any>[]>(
    () => [
      createActionsColumn<PokerSessionListDto>({
        unavailable: !showRowMenu,
        ariaLabel: 'Poker session actions',
        getItems: (session) =>
          getRowMenuItems({
            session,
            onEditClicked,
            onCompleteClicked,
            onDeleteClicked,
          }),
      }),
      { id: 'key', accessorKey: 'key', header: 'Key', size: 90 },
      {
        id: 'name',
        accessorKey: 'name',
        header: 'Name',
        size: 300,
        meta: { filterEnableSet: true },
        cell: ({ row }) => (
          <Link href={`/planning/poker-sessions/${row.original.key}`}>
            {row.original.name}
          </Link>
        ),
      },
      {
        id: 'status',
        accessorKey: 'status',
        header: 'Status',
        size: 125,
        meta: { filterType: 'set' },
      },
      {
        id: 'facilitators',
        accessorFn: (session) =>
          session.facilitators.map((f) => f.name).join(', '),
        header: 'Facilitators',
        size: 200,
      },
      {
        id: 'participants',
        accessorFn: (session) =>
          session.participants.map((p) => p.name).join(', '),
        header: 'Participants',
        size: 260,
      },
      {
        id: 'visibility',
        accessorKey: 'visibility',
        header: 'Visibility',
        size: 110,
        meta: { filterType: 'set' },
      },
      {
        id: 'roundCount',
        accessorKey: 'roundCount',
        header: 'Rounds',
        size: 110,
      },
    ],
    [showRowMenu, onEditClicked, onCompleteClicked, onDeleteClicked],
  )

  return (
    <WaydGrid
      columns={columns}
      data={sessions}
      onRefresh={refetch}
      isLoading={isLoading}
      persistStateKey="poker-sessions"
      csvFileName="poker-sessions"
      emptyMessage="No poker sessions found."
      rightSlot={
        gridControlMenuItems ? (
          <ControlItemsMenu items={gridControlMenuItems} />
        ) : undefined
      }
    />
  )
}

export default PokerSessionsGrid
