'use client'

import {
  forwardRef,
  Fragment,
  type ChangeEvent,
  type Ref,
  useCallback,
  useEffect,
  useImperativeHandle,
  useMemo,
  useRef,
  useState,
  ReactElement,
} from 'react'
import { Input, Select, Spin } from 'antd'
import type { FormInstance } from 'antd'
import { FilterOutlined } from '@ant-design/icons'
import { flexRender, getExpandedRowModel } from '@tanstack/react-table'
import {
  DndContext,
  type DragCancelEvent,
  type DragEndEvent,
  type DragStartEvent,
  closestCenter,
} from '@dnd-kit/core'
import { SortableContext, verticalListSortingStrategy } from '@dnd-kit/sortable'

import { WaydEmpty } from '@/src/components/common'

import { useRemainingHeight } from '@/src/hooks'
import styles from './tree-grid.module.css'
import type {
  DraftItem,
  TreeGridColumnContext,
  TreeGridColumnMeta,
  TreeGridHandle,
  TreeGridProps,
  TreeNode,
} from './types'
import { countTreeNodes, flattenTree } from './tree-utils'
import { mergeDraftsIntoTree } from './draft-utils'
import {
  GridSortableRow,
  useGridDndSensors,
} from '../wayd-grid-core/dnd/grid-dnd'
import {
  INDENTATION_WIDTH,
  calculateOrderInParent,
  getProjection,
} from '../wayd-grid-core/dnd/tree-projection'
import { exportGridToCsv } from '../wayd-grid-core/grid-export'
import {
  GridHeaderCell,
  useResizeClickGuard,
  type GridHeaderCellClasses,
} from '../wayd-grid-core/grid-header-row'
import { useGridEditing } from '../wayd-grid-core/use-grid-editing'
import { useGridState, useGridTable } from '../wayd-grid-core/use-grid-table'
import TreeGridToolbar from './tree-grid-toolbar'

const EMPTY_FIELD_ERRORS: Record<string, string> = {}
const FILTER_DEBOUNCE_MS = 250
const DRAFT_SCROLL_MARGIN = 8
const headerCellClasses: GridHeaderCellClasses = {
  th: styles.th,
  thSortable: styles.thSortable,
  thResizable: styles.thResizable,
  thContent: styles.thContent,
  thText: styles.thText,
  resizer: styles.resizer,
  resizerActive: styles.resizerActive,
}
const escapeSelectorValue = (value: string) =>
  typeof CSS !== 'undefined' && CSS.escape
    ? CSS.escape(value)
    : value.replace(/["\\]/g, '\\$&')
const formatTagPlaceholder = (values: { label?: unknown; value?: unknown }[]) => {
  const labels = values
    .map((v) => String(v.label ?? v.value ?? ''))
    .filter(Boolean)
  return labels.length === 1 ? labels[0] : `${labels.length} selected`
}
const NOOP_FORM = {
  validateFields: async () => ({}),
  getFieldsValue: () => ({}),
  setFieldsValue: () => {},
  resetFields: () => {},
} as unknown as FormInstance

const scrollRowIntoViewIfNeeded = (
  rowId: string,
  tableWrapperClassName: string,
) => {
  const row = document.querySelector(
    `[data-row-id="${escapeSelectorValue(rowId)}"]`,
  ) as HTMLElement | null
  const wrapper = row?.closest(
    `.${tableWrapperClassName}`,
  ) as HTMLElement | null

  if (!row || !wrapper) return 'missing'

  const rowRect = row.getBoundingClientRect()
  const wrapperRect = wrapper.getBoundingClientRect()

  const visibleTop = wrapperRect.top + wrapper.clientTop
  const visibleBottom = visibleTop + wrapper.clientHeight

  if (rowRect.top < visibleTop + DRAFT_SCROLL_MARGIN) {
    wrapper.scrollTop -= visibleTop + DRAFT_SCROLL_MARGIN - rowRect.top
    return 'done'
  }

  if (rowRect.bottom > visibleBottom - DRAFT_SCROLL_MARGIN) {
    wrapper.scrollTop +=
      rowRect.bottom - (visibleBottom - DRAFT_SCROLL_MARGIN)
    return 'done'
  }

  return 'done'
}

const requestScrollRowIntoView = (
  rowId: string,
  tableWrapperClassName: string,
) => {
  let attempts = 0

  const tryScroll = () => {
    if (scrollRowIntoViewIfNeeded(rowId, tableWrapperClassName) === 'done') {
      return
    }
    attempts += 1
    if (attempts < 12) {
      setTimeout(tryScroll, 20)
    }
  }

  requestAnimationFrame(tryScroll)
}

function TreeGridInner<T extends TreeNode>(
  props: TreeGridProps<T>,
  ref: Ref<TreeGridHandle>,
) {
  // TanStack Table's instance mutates internally behind a stable identity, so
  // React Compiler memoization goes stale (sort icons, column sizes). Before
  // the table config moved into useGridTable, the direct useReactTable call
  // made the compiler skip this component automatically; the directive keeps
  // that behavior now that the call is behind the hook.
  'use no memo'
  const {
    data,
    getSubRows,
    isLoading,
    columns: columnsProp,
    onRefresh,
    leftSlot,
    helpContent,
    rightSlot,
    emptyMessage = 'No records found',
    csvFileName = 'tree-grid-export',
    height,
    enableDragAndDrop = false,
    onNodeMove,
    onMoveRejected,
    moveValidator,
    editingConfig,
    fieldErrors: externalFieldErrors,
    onFieldErrorsChange,
    createDraftNode,
    onDraftCancelled,
    onDraftsChange,
  } = props

  // ─── Auto-height ─────────────────────────────────────────
  const [treeGridContainerRef, autoHeight] = useRemainingHeight()
  const resolvedHeight = height ?? autoHeight

  // ─── State ───────────────────────────────────────────────
  const [draggedNodeId, setDraggedNodeId] = useState<string | null>(null)
  const [draftTasks, setDraftTasks] = useState<DraftItem[]>([])
  const [textFilterDraftValues, setTextFilterDraftValues] = useState<
    Record<string, string>
  >({})
  const draftTasksRef = useRef<DraftItem[]>([])
  const isAddingDraftRef = useRef(false)
  const draftCounterRef = useRef(0)
  const resizeGuard = useResizeClickGuard()
  const filterDebounceTimersRef = useRef<
    Map<string, ReturnType<typeof setTimeout>>
  >(new Map())
  const pendingFilterFocusRef = useRef<{
    inputId: string
    selectionStart: number | null
    selectionEnd: number | null
  } | null>(null)

  const gridState = useGridState({
    onClear: () => {
      setTextFilterDraftValues({})
      filterDebounceTimersRef.current.forEach((timer) => clearTimeout(timer))
      filterDebounceTimersRef.current.clear()
    },
  })
  const {
    searchValue,
    sorting,
    columnFilters,
    onSearchChange,
    onClearFilters,
    hasActiveFilters,
  } = gridState

  const updateDraftTasks = useCallback(
    (updater: (prev: DraftItem[]) => DraftItem[]) => {
      setDraftTasks((prev) => {
        const next = updater(prev)
        draftTasksRef.current = next
        return next
      })
    },
    [],
  )

  // Field errors: delegate to external state when provided
  const [internalFieldErrors, setInternalFieldErrors] =
    useState<Record<string, string>>(EMPTY_FIELD_ERRORS)
  const fieldErrors = externalFieldErrors ?? internalFieldErrors
  const setFieldErrors = useCallback(
    (errors: Record<string, string>) => {
      if (onFieldErrorsChange) {
        onFieldErrorsChange(errors)
      } else {
        setInternalFieldErrors(errors)
      }
    },
    [onFieldErrorsChange],
  )

  // ─── Draft management ────────────────────────────────────
  const dataWithDrafts = useMemo(() => {
    if (!createDraftNode || draftTasks.length === 0) return data
    return mergeDraftsIntoTree(data, draftTasks, createDraftNode)
  }, [data, draftTasks, createDraftNode])

  // ─── Editing hook ────────────────────────────────────────
  const canEdit = editingConfig?.canEdit ?? false
  const draftPrefix = editingConfig?.draftPrefix ?? 'draft-'

  const editing = useGridEditing<T>(
    editingConfig
      ? {
          ...editingConfig,
          data: dataWithDrafts,
          tableWrapperClassName: styles.table,
          fieldErrors,
          setFieldErrors,
          onSave: async (rowId: string, updates: Record<string, any>) => {
            const success = await editingConfig.onSave(rowId, updates)
            if (success && rowId.startsWith(draftPrefix)) {
              updateDraftTasks((prev) => {
                const next = prev.filter((d) => d.id !== rowId)
                onDraftsChange?.(next)
                return next
              })
            }
            return success
          },
          onCancelDraft: (draftId) => {
            updateDraftTasks((prev) => {
              const next = prev.filter((d) => d.id !== draftId)
              onDraftsChange?.(next)
              return next
            })
            onDraftCancelled?.(draftId)
          },
        }
      : {
          data: dataWithDrafts,
          canEdit: false,
          form: NOOP_FORM,
          tableWrapperClassName: styles.table,
          editableColumnIds: [],
          onSave: async () => false,
          fieldErrors: EMPTY_FIELD_ERRORS,
          setFieldErrors: () => {},
          getFormValues: () => ({}),
          computeChanges: () => null,
          cellIdColumnMatchOrder: [],
        },
  )

  const {
    tableRef,
    selectedRowId,
    setSelectedRowId,
    setSelectedCellId,
    isSaving,
    saveFormChanges,
    getFieldError,
    editableColumns,
    handleKeyDown,
    createSelectInputKeyDown,
    handleRowClick,
  } = editing

  // ─── Draft helpers ───────────────────────────────────────
  const isEditing = selectedRowId !== null
  const hasDraft = draftTasks.length > 0
  const canAttemptCreateDraft =
    canEdit && !!createDraftNode && !isLoading && !isSaving && !isAddingDraftRef.current
  const canCreateDraft =
    canAttemptCreateDraft && (!hasDraft || isEditing)

  const addDraft = useCallback(
    async (parentId?: string): Promise<string | null> => {
      if (!canCreateDraft) return null
      if (isAddingDraftRef.current) return null

      isAddingDraftRef.current = true
      try {
        if (selectedRowId) {
          const saved = await saveFormChanges(selectedRowId)
          if (!saved) return null
        }

        // Prevent multiple simultaneous drafts from being added.
        if (draftTasksRef.current.length > 0) {
          return null
        }

        draftCounterRef.current += 1
        const newDraft: DraftItem = {
          id: `${draftPrefix}${Date.now()}-${draftCounterRef.current}`,
          parentId,
          order: 0,
        }
        updateDraftTasks((prev) => {
          const next = [...prev, newDraft]
          onDraftsChange?.(next)
          return next
        })

        // Ensure parent is expanded when adding a child draft
        if (parentId && tableRef.current) {
          const rows = tableRef.current.getRowModel().rows
          const parentRow = rows.find((r: any) => r.original.id === parentId)
          if (parentRow && !parentRow.getIsExpanded()) {
            parentRow.toggleExpanded()
          }
        }

        // Defer selection so the draft row renders first (unselected),
        // then a second render mounts the editable input for focusing.
        requestAnimationFrame(() => {
          setTimeout(() => {
            setSelectedRowId(newDraft.id)
            setSelectedCellId(`${newDraft.id}-name`)
            requestScrollRowIntoView(newDraft.id, styles.tableWrapper)
          }, 50)
        })

        return newDraft.id
      } finally {
        isAddingDraftRef.current = false
      }
    },
    [
      canCreateDraft,
      draftPrefix,
      onDraftsChange,
      saveFormChanges,
      selectedRowId,
      setSelectedRowId,
      setSelectedCellId,
      tableRef,
      updateDraftTasks,
    ],
  )

  const addDraftAtRoot = useCallback(() => {
    void addDraft()
    return null
  }, [addDraft])

  const addDraftAsChild = useCallback(
    (parentId: string) => {
      void addDraft(parentId)
      return null
    },
    [addDraft],
  )

  // ─── Column context ──────────────────────────────────────
  const dragEnabledBase =
    enableDragAndDrop && !!onNodeMove && !isLoading && !isEditing

  const columnContext: TreeGridColumnContext = useMemo(() => {
    const dragEnabledForColumns =
      dragEnabledBase &&
      !(!!searchValue || columnFilters.length > 0 || sorting.length > 0)

    return {
      selectedRowId,
      handleKeyDown,
      createSelectInputKeyDown,
      getFieldError,
      editableColumns,
      isDragEnabled: dragEnabledForColumns,
      canCreateDraft,
      addDraftAtRoot,
      addDraftAsChild,
    }
  }, [
    searchValue,
    columnFilters.length,
    sorting.length,
    selectedRowId,
    handleKeyDown,
    createSelectInputKeyDown,
    getFieldError,
    editableColumns,
    dragEnabledBase,
    canCreateDraft,
    addDraftAtRoot,
    addDraftAsChild,
  ])

  // ─── Resolved columns ───────────────────────────────────
  const columns = useMemo(() => {
    if (typeof columnsProp === 'function') {
      return columnsProp(columnContext)
    }
    return columnsProp
  }, [columnsProp, columnContext])

  // ─── Flattened tree for DnD ──────────────────────────────
  const flattenedNodes = useMemo(
    () => flattenTree(dataWithDrafts),
    [dataWithDrafts],
  )

  // ─── DnD setup ───────────────────────────────────────────
  const sensors = useGridDndSensors()

  const handleDragStart = useCallback(
    (event: DragStartEvent) => {
      setDraggedNodeId(event.active.id as string)
      setSelectedRowId(null)
    },
    [setSelectedRowId],
  )

  const handleDragEnd = useCallback(
    async (event: DragEndEvent) => {
      const { active, over, delta } = event
      setDraggedNodeId(null)

      if (!over || !onNodeMove) return

      const horizontalOffset = delta.x
      const hasHorizontalMovement =
        Math.abs(horizontalOffset) >= INDENTATION_WIDTH / 2

      if (active.id === over.id && !hasHorizontalMovement) return

      const projection = getProjection(
        flattenedNodes,
        active.id as string,
        over.id as string,
        horizontalOffset,
        INDENTATION_WIDTH,
        moveValidator,
      )

      if (!projection.canDrop) {
        onMoveRejected?.(
          projection.reason || 'Cannot move item to this location',
        )
        return
      }

      const overIndex = flattenedNodes.findIndex((t) => t.node.id === over.id)
      const newOrder = calculateOrderInParent(
        flattenedNodes,
        active.id as string,
        overIndex,
        projection.parentId,
      )

      try {
        await onNodeMove(active.id as string, projection.parentId, newOrder, over.id as string, overIndex)
      } catch (error) {
        onMoveRejected?.(
          error instanceof Error ? error.message : 'Failed to move item',
        )
      }
    },
    [flattenedNodes, moveValidator, onNodeMove, onMoveRejected],
  )

  const handleDragCancel = useCallback((_event: DragCancelEvent) => {
    setDraggedNodeId(null)
    if (document.activeElement instanceof HTMLElement) {
      document.activeElement.blur()
    }
  }, [])

  // ─── TanStack table ─────────────────────────────────────
  const totalRowCount = useMemo(
    () => countTreeNodes(data) + draftTasks.length,
    [data, draftTasks.length],
  )

  const table = useGridTable({
    data: dataWithDrafts,
    columns,
    gridState,
    tableOptions: {
      getExpandedRowModel: getExpandedRowModel(),
      getSubRows: getSubRows ?? ((row) => row.children as T[]),
      filterFromLeafRows: true,
      initialState: { expanded: true },
    },
  })

  tableRef.current = table

  const displayedRowCount = table.getRowModel().rows.length

  // ─── Toolbar wiring ──────────────────────────────────────
  const visibleColumnCount = table.getVisibleLeafColumns().length

  useEffect(() => {
    const pending = pendingFilterFocusRef.current
    if (!pending) return

    pendingFilterFocusRef.current = null
    const input = document.getElementById(
      pending.inputId,
    ) as HTMLInputElement | null
    if (!input) return

    input.focus({ preventScroll: true })
    if (
      pending.selectionStart !== null &&
      pending.selectionEnd !== null &&
      input.setSelectionRange
    ) {
      input.setSelectionRange(pending.selectionStart, pending.selectionEnd)
    }
  }, [columnFilters])

  useEffect(() => {
    const timers = filterDebounceTimersRef.current
    return () => {
      timers.forEach((timer) => clearTimeout(timer))
      timers.clear()
    }
  }, [])

  const handleTextFilterChange = useCallback(
    (
      e: ChangeEvent<HTMLInputElement>,
      columnId: string,
      setFilterValue: (value: string | undefined) => void,
      filterInputId: string,
    ) => {
      const next = e.target.value
      setTextFilterDraftValues((prev) => ({
        ...prev,
        [columnId]: next,
      }))

      const existingTimer = filterDebounceTimersRef.current.get(columnId)
      if (existingTimer) {
        clearTimeout(existingTimer)
      }

      const timer = setTimeout(() => {
        setFilterValue(next ? next : undefined)
        setTextFilterDraftValues((prev) => {
          if (!(columnId in prev)) return prev
          const updated = { ...prev }
          delete updated[columnId]
          return updated
        })
        filterDebounceTimersRef.current.delete(columnId)
      }, FILTER_DEBOUNCE_MS)
      filterDebounceTimersRef.current.set(columnId, timer)

      pendingFilterFocusRef.current = {
        inputId: filterInputId,
        selectionStart: e.target.selectionStart,
        selectionEnd: e.target.selectionEnd,
      }
    },
    [],
  )

  // ─── CSV export ──────────────────────────────────────────
  const onExportCsv = useCallback(() => {
    exportGridToCsv(table, csvFileName)
  }, [table, csvFileName])

  // ─── Ref handle ──────────────────────────────────────────
  useImperativeHandle(ref, () => ({
    table,
    selectedRowId,
  }))

  // ─── Row/cell click handlers ────────────────────────────
  const handleRowClickWithContext = useCallback(
    (e: React.MouseEvent, rowId: string) => {
      void handleRowClick(e, {
        rowId,
        isEditableColumn: (columnId) => editableColumns.includes(columnId),
        getClickedColumnId: (target) =>
          target.closest('td')?.getAttribute('data-column-id') ?? null,
      })
    },
    [handleRowClick, editableColumns],
  )

  const handleCellClick = useCallback((e: React.MouseEvent) => {
    const target = e.target as HTMLElement
    if (
      target.closest('input') ||
      target.closest('.ant-select') ||
      target.closest('.ant-picker') ||
      target.closest('.ant-color-picker')
    ) {
      e.stopPropagation()
    }
  }, [])

  // ─── Resolved leftSlot ──────────────────────────────────
  const resolvedLeftSlot =
    typeof leftSlot === 'function' ? leftSlot(columnContext) : leftSlot

  // ─── DnD wrapping ───────────────────────────────────────
  const dndEnabled = enableDragAndDrop && !!onNodeMove
  const isDragEnabled = dndEnabled && !isLoading && !isEditing && !hasActiveFilters

  // ─── Render ──────────────────────────────────────────────
  const tableContent = (
    <div className={styles.tableWrapper}>
      <table className={styles.tableElement}>
        <colgroup>
          {table.getVisibleLeafColumns().map((column) => (
            <col key={column.id} width={column.getSize()} />
          ))}
        </colgroup>
        <thead>
          {table.getHeaderGroups().map((headerGroup) => (
            <Fragment key={headerGroup.id}>
              {/* Header row */}
              <tr key={headerGroup.id}>
                {headerGroup.headers.map((header) => (
                  <GridHeaderCell
                    key={header.id}
                    header={header}
                    resizeGuard={resizeGuard}
                    classes={headerCellClasses}
                  />
                ))}
              </tr>

              {/* Filter row */}
              <tr key={`${headerGroup.id}-filters`} data-role="column-filters">
                {headerGroup.headers.map((header) => {
                  const column = header.column
                  if (!column.getCanFilter() || header.isPlaceholder) {
                    return (
                      <th
                        key={`${header.id}-filter`}
                        className={styles.filterTh}
                      />
                    )
                  }

                  const meta = column.columnDef.meta as
                    | TreeGridColumnMeta
                    | undefined
                  const filterType = meta?.filterType ?? 'text'
                  const rawFilterValue = column.getFilterValue()

                  if (filterType === 'select') {
                    const options = meta?.filterOptions ?? []
                    const selectValue = (
                      Array.isArray(rawFilterValue) ? rawFilterValue : []
                    ) as string[]

                    return (
                      <th
                        key={`${header.id}-filter`}
                        className={styles.filterTh}
                        onClick={(e) => e.stopPropagation()}
                      >
                        <Select
                          size="small"
                          mode="multiple"
                          allowClear
                          maxTagCount={0}
                          maxTagPlaceholder={formatTagPlaceholder}
                          value={selectValue.length ? selectValue : undefined}
                          options={options}
                          suffixIcon={<FilterOutlined />}
                          popupMatchSelectWidth={false}
                          classNames={{
                            popup: { root: styles.filterPopup },
                          }}
                          onChange={(v) =>
                            column.setFilterValue(v && v.length ? v : undefined)
                          }
                          className={styles.filterControl}
                        />
                      </th>
                    )
                  }

                  // text or numericRange — both render an Input
                  const appliedFilterValue = (rawFilterValue ?? '') as string
                  const draftValue = textFilterDraftValues[column.id]
                  const textValue =
                    draftValue !== undefined ? draftValue : appliedFilterValue
                  const filterInputId = `tree-grid-filter-${header.id}`
                  return (
                    <th
                      key={`${header.id}-filter`}
                      className={styles.filterTh}
                      onClick={(e) => e.stopPropagation()}
                    >
                      <Input
                        id={filterInputId}
                        size="small"
                        allowClear
                        placeholder={meta?.filterPlaceholder}
                        value={textValue}
                        onChange={(e) =>
                          handleTextFilterChange(
                            e,
                            column.id,
                            column.setFilterValue,
                            filterInputId,
                          )
                        }
                        className={styles.filterControl}
                      />
                    </th>
                  )
                })}
              </tr>
            </Fragment>
          ))}
        </thead>
        <tbody>
          {isLoading ? (
            <tr>
              <td
                colSpan={visibleColumnCount}
                className={`${styles.td} ${styles.loading}`}
              >
                <Spin />
              </td>
            </tr>
          ) : table.getRowModel().rows.length === 0 ? (
            <tr>
              <td
                colSpan={visibleColumnCount}
                className={`${styles.td} ${styles.empty}`}
              >
                <WaydEmpty message={emptyMessage} />
              </td>
            </tr>
          ) : (
            table.getRowModel().rows.flatMap((row, index) => {
              const isSelected = selectedRowId === row.original.id
              const isDragging = draggedNodeId === row.original.id
              const isDraftRow = row.original.id.startsWith(draftPrefix)
              const rowElements = [
                <GridSortableRow
                  key={row.id}
                  nodeId={row.original.id}
                  isDragEnabled={isDragEnabled && !isDraftRow}
                  isDragging={isDragging}
                  className={`${styles.tr}${canEdit ? ` ${styles.trEditable}` : ''}${index % 2 === 1 ? ` ${styles.trAlt}` : ''}${isSelected ? ` ${styles.trSelected}` : ''}`}
                  onClick={(e) => handleRowClickWithContext(e, row.original.id)}
                >
                  {row.getVisibleCells().map((cell) => {
                    const isEditableCell =
                      isSelected && editableColumns.includes(cell.column.id)

                    return (
                      <td
                        key={cell.id}
                        data-cell-id={`${row.original.id}-${cell.column.id}`}
                        data-column-id={cell.column.id}
                        className={`${styles.td}${isEditableCell ? ` ${styles.editableCell}` : ''}`}
                        onClick={handleCellClick}
                      >
                        {flexRender(
                          cell.column.columnDef.cell,
                          cell.getContext(),
                        )}
                      </td>
                    )
                  })}
                </GridSortableRow>,
              ]

              // Add validation error row
              if (isSelected && Object.keys(fieldErrors).length > 0) {
                const errorItems = Object.entries(fieldErrors).map(
                  ([field, error]) => (
                    <div key={field} className={styles.validationErrorItem}>
                      <span className={styles.validationErrorField}>
                        {field}:
                      </span>{' '}
                      {error}
                    </div>
                  ),
                )

                rowElements.push(
                  <tr
                    key={`${row.id}-errors`}
                    className={`${styles.tr} ${styles.validationErrorRow}`}
                  >
                    <td
                      colSpan={visibleColumnCount}
                      className={`${styles.td} ${styles.validationErrorCell}`}
                    >
                      {errorItems}
                    </td>
                  </tr>,
                )
              }

              return rowElements
            })
          )}
        </tbody>
      </table>
    </div>
  )

  return (
    <div ref={treeGridContainerRef} className={styles.table} style={{ height: resolvedHeight }}>
      <TreeGridToolbar
        displayedRowCount={displayedRowCount}
        totalRowCount={totalRowCount}
        searchValue={searchValue}
        onSearchChange={onSearchChange}
        onRefresh={onRefresh}
        onClearFilters={onClearFilters}
        hasActiveFilters={hasActiveFilters}
        onExportCsv={onExportCsv}
        isLoading={isLoading}
        leftSlot={resolvedLeftSlot}
        helpContent={helpContent}
        rightSlot={rightSlot}
      />

      {dndEnabled ? (
        <DndContext
          sensors={sensors}
          collisionDetection={closestCenter}
          onDragStart={handleDragStart}
          onDragEnd={handleDragEnd}
          onDragCancel={handleDragCancel}
        >
          <SortableContext items={flattenedNodes.map((t) => t.node.id)} strategy={verticalListSortingStrategy}>
            {tableContent}
          </SortableContext>
        </DndContext>
      ) : (
        tableContent
      )}
    </div>
  )
}

/**
 * Reusable tree grid component. Wraps TanStack React Table, @dnd-kit, and
 * the tree-grid utility library into a single component with toolbar, inline
 * editing, drag-and-drop reordering, column filtering, and CSV export.
 *
 * Analogous to WaydGrid for ag-grid, but for hierarchical (tree) data.
 */
const TreeGrid = forwardRef(TreeGridInner) as <T extends TreeNode>(
  props: TreeGridProps<T> & { ref?: Ref<TreeGridHandle> },
) => ReactElement | null

export default TreeGrid
