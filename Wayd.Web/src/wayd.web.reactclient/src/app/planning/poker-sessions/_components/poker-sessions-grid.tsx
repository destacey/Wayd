'use client'

import {
  WaydGrid,
  createActionsColumn,
} from '@/src/components/common/wayd-grid'
import { ControlItemsMenu } from '@/src/components/common/control-items-menu'
import { PokerSessionListDto } from '@/src/services/wayd-api'
import type { ColumnDef } from '@tanstack/react-table'
import { ItemType } from 'antd/es/menu/interface'
import Link from 'next/link'
import { FC, useMemo } from 'react'

export interface PokerSessionsGridProps {
  sessions: PokerSessionListDto[]
  isLoading: boolean
  refetch: () => void
  canUpdate: boolean
  canDelete: boolean
  gridControlMenuItems?: ItemType[]
  onEditClicked: (session: PokerSessionListDto) => void
  onCompleteClicked: (session: PokerSessionListDto) => void
  onDeleteClicked: (session: PokerSessionListDto) => void
}

interface RowMenuProps {
  session: PokerSessionListDto
  canUpdate: boolean
  canDelete: boolean
  onEditClicked: (session: PokerSessionListDto) => void
  onCompleteClicked: (session: PokerSessionListDto) => void
  onDeleteClicked: (session: PokerSessionListDto) => void
}

const getRowMenuItems = (props: RowMenuProps): ItemType[] => {
  if (!props.session) return []

  const items: ItemType[] = []

  if (props.canUpdate && props.session.status === 'Active') {
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

  if (props.canDelete) {
    items.push({
      key: 'delete',
      label: 'Delete',
      danger: true,
      onClick: () => props.onDeleteClicked(props.session),
    })
  }

  return items
}

const PokerSessionsGrid: FC<PokerSessionsGridProps> = ({
  sessions = [],
  isLoading,
  refetch,
  canUpdate,
  canDelete,
  gridControlMenuItems,
  onEditClicked,
  onCompleteClicked,
  onDeleteClicked,
}) => {
  const showRowMenu = canUpdate || canDelete

  const columns = useMemo<ColumnDef<PokerSessionListDto, any>[]>(
    () => [
      createActionsColumn<PokerSessionListDto>({
        hide: !showRowMenu,
        ariaLabel: 'Poker session actions',
        getItems: (session) =>
          getRowMenuItems({
            session,
            canUpdate,
            canDelete,
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
        id: 'facilitator',
        accessorKey: 'facilitator.name',
        header: 'Facilitator',
        size: 200,
      },
      {
        id: 'roundCount',
        accessorKey: 'roundCount',
        header: 'Rounds',
        size: 110,
      },
    ],
    [
      showRowMenu,
      canUpdate,
      canDelete,
      onEditClicked,
      onCompleteClicked,
      onDeleteClicked,
    ],
  )

  return (
    <WaydGrid
      columns={columns}
      data={sessions}
      onRefresh={refetch}
      isLoading={isLoading}
      height={650}
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
