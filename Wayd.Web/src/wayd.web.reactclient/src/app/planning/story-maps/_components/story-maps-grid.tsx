'use client'

import {
  WaydGrid,
  createActionsColumn,
} from '@/src/components/common/wayd-grid'
import { ControlItemsMenu } from '@/src/components/common/control-items-menu'
import { StoryMapListDto } from '@/src/services/wayd-api'
import type { ColumnDef } from '../../../../components/common/wayd-grid-core'
import { ItemType } from 'antd/es/menu/interface'
import Link from 'next/link'
import { FC, useMemo } from 'react'

export interface StoryMapsGridProps {
  storyMaps: StoryMapListDto[]
  isLoading: boolean
  refetch: () => void
  canUpdate: boolean
  canDelete: boolean
  gridControlMenuItems?: ItemType[]
  onArchiveClicked: (storyMap: StoryMapListDto) => void
  onDeleteClicked: (storyMap: StoryMapListDto) => void
}

interface RowMenuProps {
  storyMap: StoryMapListDto
  canUpdate: boolean
  canDelete: boolean
  onArchiveClicked: (storyMap: StoryMapListDto) => void
  onDeleteClicked: (storyMap: StoryMapListDto) => void
}

const getRowMenuItems = (props: RowMenuProps): ItemType[] => {
  if (!props.storyMap) return []

  const items: ItemType[] = []

  if (props.canUpdate && props.storyMap.status === 'Active') {
    items.push({
      key: 'archive',
      label: 'Archive',
      onClick: () => props.onArchiveClicked(props.storyMap),
    })
  }

  if (props.canDelete) {
    items.push({
      key: 'delete',
      label: 'Delete',
      danger: true,
      onClick: () => props.onDeleteClicked(props.storyMap),
    })
  }

  return items
}

const StoryMapsGrid: FC<StoryMapsGridProps> = ({
  storyMaps = [],
  isLoading,
  refetch,
  canUpdate,
  canDelete,
  gridControlMenuItems,
  onArchiveClicked,
  onDeleteClicked,
}) => {
  const showRowMenu = canUpdate || canDelete

  const columns = useMemo<ColumnDef<StoryMapListDto, any>[]>(
    () => [
      createActionsColumn<StoryMapListDto>({
        hide: !showRowMenu,
        ariaLabel: 'Story map actions',
        getItems: (storyMap) =>
          getRowMenuItems({
            storyMap,
            canUpdate,
            canDelete,
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
        id: 'owner',
        accessorKey: 'owner.name',
        header: 'Owner',
        size: 200,
      },
    ],
    [showRowMenu, canUpdate, canDelete, onArchiveClicked, onDeleteClicked],
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
