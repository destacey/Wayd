'use client'

import {
  CaretDownOutlined,
  CaretRightOutlined,
  DeleteOutlined,
  EditOutlined,
  EyeOutlined,
  HolderOutlined,
  MoreOutlined,
  PlusOutlined,
} from '@ant-design/icons'
import { Button, Dropdown, Flex } from 'antd'
import dayjs from 'dayjs'
import type { ColumnDef } from '@/src/components/common/wayd-grid-core'
import styles from '@/src/components/common/wayd-grid/wayd-grid.module.css'
import {
  type FilterOption,
  type WaydGridColumnMeta,
  dateSortBy,
  useGridDragHandle,
} from '@/src/components/common/wayd-grid'
import type { RoadmapItemTreeNode } from './roadmap-items-grid'
import type { RoadmapColorDto } from '@/src/services/wayd-api'
import {
  roadmapColorEditor,
  roadmapEndEditor,
  roadmapNameEditor,
  roadmapStartEditor,
  roadmapTypeEditor,
} from './roadmap-items-grid.editors'

const DATE_FORMAT = 'MMM D, YYYY'
const DRAG_HANDLE_STYLE = {
  cursor: 'grab',
  touchAction: 'none',
} as const
const NAME_LINK_STYLE = {
  display: 'inline-block',
  maxWidth: '100%',
  overflow: 'hidden',
  textOverflow: 'ellipsis',
  whiteSpace: 'nowrap',
} as const
const NAME_LINK_CONTAINER_STYLE = {
  flex: 1,
  minWidth: 0,
} as const
const COLOR_SWATCH_STYLE = {
  width: 12,
  height: 12,
  borderRadius: 2,
  border: '1px solid var(--ant-color-border)',
  flexShrink: 0,
} as const

const formatDate = (dateValue?: Date | string | null) =>
  dateValue ? dayjs(dateValue).format(DATE_FORMAT) : ''

function DragHandleCell({
  isDragEnabled,
  isActivity,
}: {
  isDragEnabled: boolean
  isActivity: boolean
}) {
  const { listeners, attributes } = useGridDragHandle()

  if (!isDragEnabled || !isActivity) {
    return null
  }

  return (
    <div
      {...listeners}
      {...attributes}
      className={styles.dragHandle}
      onClick={(e) => e.stopPropagation()}
      title="Drag to reorder or change parent"
      style={DRAG_HANDLE_STYLE}
    >
      <HolderOutlined
        style={{ fontSize: 14, color: 'var(--ant-color-text-tertiary)' }}
      />
    </div>
  )
}

interface RoadmapItemsGridColumnsParams {
  /** The user can change the roadmap: editors, drag handles, row menus. */
  canManage: boolean
  onEditItem: (item: RoadmapItemTreeNode) => void
  onDeleteItem: (item: RoadmapItemTreeNode) => void
  openRoadmapItemDrawer: (itemId: string) => void
  typeFilterOptions: FilterOption[]
  isDragEnabled: boolean
  enableDragAndDrop: boolean
  addDraftItemAsChild?: (parentId: string) => void
  /** No other new row is waiting to be created. */
  canCreateItems?: boolean
  roadmapColors: RoadmapColorDto[]
}

/**
 * The text shown for an item's color in the grid (also used for sort, filter, and export):
 * the palette caption when the color matches a configured entry (case-insensitive), the
 * uppercased hex when it doesn't, and an empty string when the item has no color of its own.
 *
 * Note: this intentionally ignores the roadmap's default color — the default colors the
 * timeline at display time but is not a value, so an uncolored item shows nothing here.
 */
export const roadmapColorDisplayText = (
  color: string | null | undefined,
  roadmapColors: RoadmapColorDto[],
): string => {
  if (!color) return ''
  const match = roadmapColors.find(
    (c) => c.color.toUpperCase() === color.toUpperCase(),
  )
  return match?.name ?? color.toUpperCase()
}

export const getRoadmapItemsGridColumns = ({
  canManage,
  onEditItem,
  onDeleteItem,
  openRoadmapItemDrawer,
  typeFilterOptions,
  isDragEnabled,
  enableDragAndDrop,
  addDraftItemAsChild,
  canCreateItems = true,
  roadmapColors,
}: RoadmapItemsGridColumnsParams): ColumnDef<RoadmapItemTreeNode>[] => {
  const colorDisplayText = (color: string | null | undefined): string =>
    roadmapColorDisplayText(color, roadmapColors)
  return [
    ...(canManage
      ? [
          {
            id: 'actions',
            header: '',
            size: 50,
            enableSorting: false,
            enableGlobalFilter: false,
            enableColumnFilter: false,
            enableResizing: false,
            meta: {
              enableExport: false,
              enableReordering: false,
            } satisfies WaydGridColumnMeta,
            cell: ({ row }: { row: any }) => {
              const item = row.original as RoadmapItemTreeNode
              const isDraft = item.id.startsWith('draft-')
              const isActivity = item.type === 'Activity'
              return (
                <Flex justify="end" gap={8}>
                  {enableDragAndDrop && (
                    <DragHandleCell
                      isDragEnabled={isDragEnabled}
                      isActivity={isActivity}
                    />
                  )}
                  {!isDraft && (
                    <Dropdown
                      menu={{
                        items: [
                          {
                            key: 'details',
                            label: 'View Details',
                            icon: <EyeOutlined />,
                            onClick: () => openRoadmapItemDrawer(item.id),
                          },
                          ...(isActivity && addDraftItemAsChild
                            ? [
                                {
                                  key: 'add-child',
                                  label: 'Add Child Item',
                                  icon: <PlusOutlined />,
                                  disabled: !canCreateItems,
                                  onClick: () => addDraftItemAsChild(item.id),
                                },
                              ]
                            : []),
                          // There is no milestone edit form.
                          ...(item.type !== 'Milestone'
                            ? [
                                {
                                  key: 'edit',
                                  label: 'Edit',
                                  icon: <EditOutlined />,
                                  onClick: () => onEditItem(item),
                                },
                              ]
                            : []),
                          {
                            key: 'delete',
                            label: 'Delete',
                            icon: <DeleteOutlined />,
                            onClick: () => onDeleteItem(item),
                          },
                        ],
                      }}
                      trigger={['click']}
                    >
                      <Button
                        type="text"
                        size="small"
                        icon={<MoreOutlined />}
                        tabIndex={-1}
                        aria-label="Row actions"
                        data-row-actions=""
                      />
                    </Dropdown>
                  )}
                </Flex>
              )
            },
          } as ColumnDef<RoadmapItemTreeNode>,
        ]
      : []),
    {
      accessorKey: 'name',
      header: 'Name',
      size: 300,
      enableColumnFilter: true,
      sortFn: 'alphanumeric',
      meta: {
        treeColumn: true,
        editor: roadmapNameEditor,
      } satisfies WaydGridColumnMeta,
      cell: ({ row }: { row: any }) => {
        const item = row.original as RoadmapItemTreeNode
        const depth = row.depth
        const isDraft = item.id.startsWith('draft-')
        const isActivity = item.type === 'Activity'

        return (
          <Flex
            className={styles.nameCell}
            align="center"
            justify="space-between"
            gap={4}
            style={{ width: '100%' }}
          >
            <Flex align="center" gap={0} style={{ flex: 1, minWidth: 0 }}>
              {Array.from({ length: depth }).map((_, index) => (
                <span key={index} className={styles.indentSpacer} />
              ))}
              {row.getCanExpand() ? (
                <Button
                  type="text"
                  size="small"
                  icon={
                    row.getIsExpanded() ? (
                      <CaretDownOutlined />
                    ) : (
                      <CaretRightOutlined />
                    )
                  }
                  onClick={(e) => {
                    e.stopPropagation()
                    row.getToggleExpandedHandler()()
                  }}
                  tabIndex={-1}
                  aria-expanded={row.getIsExpanded()}
                  aria-label="Show children"
                  className={styles.expanderBtn}
                />
              ) : (
                <span className={styles.indentSpacer} />
              )}
              {isDraft ? (
                <span style={{ ...NAME_LINK_STYLE }}>
                  {item.name || '(New Item)'}
                </span>
              ) : (
                <span style={NAME_LINK_CONTAINER_STYLE}>
                  <a
                    role="button"
                    tabIndex={-1}
                    onClick={(e) => {
                      e.stopPropagation()
                      openRoadmapItemDrawer(item.id)
                    }}
                    onKeyDown={(e) => {
                      if (e.key !== 'Enter' && e.key !== ' ') return
                      e.preventDefault()
                      openRoadmapItemDrawer(item.id)
                    }}
                    style={NAME_LINK_STYLE}
                  >
                    {item.name}
                  </a>
                </span>
              )}
            </Flex>
            {canManage && !isDraft && isActivity && addDraftItemAsChild && (
              <Button
                type="text"
                size="small"
                icon={<PlusOutlined />}
                tabIndex={-1}
                className={`${styles.rowActionBtn} ${styles.inlineRowAction}`}
                onClick={(e) => {
                  e.stopPropagation()
                  addDraftItemAsChild(item.id)
                }}
                disabled={!canCreateItems}
                title="Add child item"
              />
            )}
          </Flex>
        )
      },
    },
    {
      id: 'type',
      accessorFn: (row) => row.type,
      header: 'Type',
      size: 110,
      enableGlobalFilter: true,
      enableColumnFilter: true,
      sortFn: 'text',
      meta: {
        filterType: 'select',
        filterOptions: typeFilterOptions,
        editor: roadmapTypeEditor,
      } satisfies WaydGridColumnMeta,
      cell: ({ row }: { row: any }) =>
        (row.original as RoadmapItemTreeNode).type,
    },
    {
      id: 'start',
      accessorFn: (row) => {
        const dateValue = row.type === 'Milestone' ? row.date : row.start
        return formatDate(dateValue)
      },
      header: 'Start',
      size: 120,
      enableGlobalFilter: true,
      enableColumnFilter: true,
      sortFn: dateSortBy((row: any) => {
        const item = row.original as RoadmapItemTreeNode
        return item.type === 'Milestone' ? item.date : item.start
      }),
      meta: {
        editor: roadmapStartEditor,
        exportFormatter: (_value, row) => {
          const dateValue = row.type === 'Milestone' ? row.date : row.start
          return formatDate(dateValue)
        },
      } satisfies WaydGridColumnMeta,
      cell: ({ row }: { row: any }) => {
        const item = row.original as RoadmapItemTreeNode
        return formatDate(item.type === 'Milestone' ? item.date : item.start)
      },
    },
    {
      id: 'end',
      accessorFn: (row) => formatDate(row.end),
      header: 'End',
      size: 120,
      enableGlobalFilter: true,
      enableColumnFilter: true,
      sortFn: dateSortBy((row: any) => row.original.end),
      meta: {
        editor: roadmapEndEditor,
        exportFormatter: (value) => formatDate(value as string | null),
      } satisfies WaydGridColumnMeta,
      cell: ({ row }: { row: any }) =>
        formatDate((row.original as RoadmapItemTreeNode).end),
    },
    {
      id: 'color',
      accessorFn: (row) => colorDisplayText(row.color),
      header: 'Color',
      size: 100,
      enableSorting: false,
      enableGlobalFilter: true,
      enableColumnFilter: true,
      meta: {
        editor: roadmapColorEditor(roadmapColors),
        exportFormatter: (_value, row) => {
          if (row.type === 'Timebox') return ''
          return colorDisplayText(row.color)
        },
      } satisfies WaydGridColumnMeta,
      cell: ({ row }: { row: any }) => {
        const item = row.original as RoadmapItemTreeNode

        // Only the item's own color is shown. An item with no color renders an
        // empty cell — the roadmap default colors the timeline, not this value.
        if (item.type === 'Timebox' || !item.color) {
          return null
        }

        return (
          <Flex align="center" gap={6}>
            <span
              aria-hidden
              style={{ ...COLOR_SWATCH_STYLE, backgroundColor: item.color }}
            />
            <span>{colorDisplayText(item.color)}</span>
          </Flex>
        )
      },
    },
  ]
}
