'use client'

import {
  WaydGrid,
  createActionsColumn,
} from '@/src/components/common/wayd-grid'
import { ControlItemsMenu } from '@/src/components/common/control-items-menu'
import { StoryMapListDto } from '@/src/services/wayd-api'
import type { ColumnDef } from '@/src/components/common/wayd-grid-core'
import type { ItemType } from 'antd/es/menu/interface'
import Link from 'next/link'
import { FC, useMemo } from 'react'

export interface StoryMapsGridProps {
  storyMaps: StoryMapListDto[]
  isLoading: boolean
  refetch: () => void
  gridControlMenuItems?: ItemType[]
  onArchiveClicked: (storyMap: StoryMapListDto) => void
  onDeleteClicked: (storyMap: StoryMapListDto) => void
}

interface RowMenuProps {
  storyMap: StoryMapListDto
  onArchiveClicked: (storyMap: StoryMapListDto) => void
  onDeleteClicked: (storyMap: StoryMapListDto) => void
}

const getRowMenuItems = (props: RowMenuProps): ItemType[] => {
  // Each map says whether the caller manages it: an owner or a Story Maps administrator.
  if (!props.storyMap?.canManage) return []

  const items: ItemType[] = []

  if (props.storyMap.status === 'Active') {
    items.push({
      key: 'archive',
      label: 'Archive',
      onClick: () => props.onArchiveClicked(props.storyMap),
    })
  }

  items.push({
    key: 'delete',
    label: 'Delete',
    danger: true,
    onClick: () => props.onDeleteClicked(props.storyMap),
  })

  return items
}

const StoryMapsGrid: FC<StoryMapsGridProps> = ({
  storyMaps = [],
  isLoading,
  refetch,
  gridControlMenuItems,
  onArchiveClicked,
  onDeleteClicked,
}) => {
  const showRowMenu = storyMaps.some((m) => m.canManage)

  const columns = useMemo<ColumnDef<StoryMapListDto, any>[]>(
    () => [
      createActionsColumn<StoryMapListDto>({
        unavailable: !showRowMenu,
        ariaLabel: 'Story map actions',
        getItems: (storyMap) =>
          getRowMenuItems({
            storyMap,
            onArchiveClicked,
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
          <Link href={`/planning/story-maps/${row.original.key}`}>
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
        id: 'owners',
        accessorFn: (row) => row.owners.map((o) => o.name).join(', '),
        header: 'Owners',
        size: 200,
      },
      {
        id: 'visibility',
        accessorKey: 'visibility',
        header: 'Visibility',
        size: 110,
        meta: { filterType: 'set' },
      },
    ],
    [showRowMenu, onArchiveClicked, onDeleteClicked],
  )

  return (
    <WaydGrid
      columns={columns}
      data={storyMaps}
      onRefresh={refetch}
      isLoading={isLoading}
      persistStateKey="planning-story-maps"
      initialSorting={[{ id: 'name', desc: false }]}
      csvFileName="story-maps"
      emptyMessage="No story maps found."
      rightSlot={
        gridControlMenuItems ? (
          <ControlItemsMenu items={gridControlMenuItems} />
        ) : undefined
      }
    />
  )
}

export default StoryMapsGrid
