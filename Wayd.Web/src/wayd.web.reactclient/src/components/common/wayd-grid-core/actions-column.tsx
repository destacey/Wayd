'use client'

import { Button, Dropdown, Flex } from 'antd'
import { MoreOutlined } from '@ant-design/icons'
import type { ItemType } from 'antd/es/menu/interface'
import type { ColumnDef, Row } from './index'
import type { RowData } from '@tanstack/react-table'
import type { ReactNode } from 'react'
/** Default width of the actions column — just wide enough for the ⋯ button. */
export const ACTIONS_COLUMN_SIZE = 50

export interface ActionsColumnOptions<T extends RowData> {
  /**
   * Builds the dropdown menu items for a single row. This is the only piece
   * that varies between grids: it runs per row and typically gates each item on
   * permissions and row data. Return an empty array (or one with only dividers)
   * to render no menu for that row — the cell shows nothing.
   */
  getItems: (row: T) => ItemType[]
  /**
   * Withholds the column entirely (via `meta.unavailable`). Usually
   * `!canManageX`, so it disappears when the user can't act on any row and
   * cannot be brought back through Choose Columns. Defaults to shown.
   */
  unavailable?: boolean
  /** Column id. Defaults to `'actions'`. */
  id?: string
  /** Column width. Defaults to {@link ACTIONS_COLUMN_SIZE}. */
  size?: number
  /** Accessible label for the trigger button. Defaults to `'Row actions'`. */
  ariaLabel?: string
  /**
   * Rendered immediately before the ⋯ in the same cell — a row's grab handle,
   * typically. Sharing the cell rather than taking a column of its own keeps a
   * whole column's width from going to one icon, and keeps everything that acts
   * on a row in one place. Widen the column with `size` when passing this.
   */
  leading?: ReactNode
}

/** True when a menu has at least one real (non-divider) item worth showing. */
const hasActionableItem = (items: ItemType[]): boolean =>
  items.some((item) => item != null && (item as { type?: string }).type !== 'divider')

/**
 * Cell renderer for the actions column — a `⋯` button opening a click-triggered
 * dropdown of the row's items. Renders nothing when the row has no actionable
 * items, so rows the user can't act on stay clean.
 */
const ActionsCell = <T extends RowData,>({
  row,
  getItems,
  ariaLabel,
}: {
  row: T
  getItems: (row: T) => ItemType[]
  ariaLabel: string
}) => {
  const items = getItems(row)
  if (!hasActionableItem(items)) return null

  return (
    <Dropdown menu={{ items }} trigger={['click']}>
      <Button
        type="text"
        size="small"
        aria-label={ariaLabel}
        icon={<MoreOutlined />}
      />
    </Dropdown>
  )
}

/**
 * Builds the reusable row-actions column for a WaydGrid. Everything about the
 * column is standardized — a fixed-width, non-sortable/-filterable/-resizable
 * `⋯` dropdown that hides itself when a row has no items — except {@link
 * ActionsColumnOptions.getItems}, which each grid supplies to compute per-row
 * menu items (typically gated on permissions).
 *
 * @example
 * createActionsColumn<Objective>({
 *   unavailable: !canManage,
 *   getItems: (o) => [
 *     canManage && { key: 'edit', label: 'Edit', onClick: () => edit(o) },
 *   ].filter(Boolean) as ItemType[],
 * })
 */
export const createActionsColumn = <T extends RowData,>({
  getItems,
  unavailable,
  id = 'actions',
  size = ACTIONS_COLUMN_SIZE,
  ariaLabel = 'Row actions',
  leading,
}: ActionsColumnOptions<T>): ColumnDef<T, unknown> => ({
  id,
  header: '',
  size,
  enableSorting: false,
  enableColumnFilter: false,
  enableResizing: false,
  enableGlobalFilter: false,
  // The actions column always holds its position — no drag grip, rejects drops.
  meta: {
    enableReordering: false,
    ...(unavailable === undefined ? {} : { unavailable }),
  },
  cell: ({ row }: { row: Row<T> }) =>
    leading === undefined ? (
      <ActionsCell row={row.original} getItems={getItems} ariaLabel={ariaLabel} />
    ) : (
      <Flex align="center" gap={2}>
        {leading}
        <ActionsCell row={row.original} getItems={getItems} ariaLabel={ariaLabel} />
      </Flex>
    ),
})
