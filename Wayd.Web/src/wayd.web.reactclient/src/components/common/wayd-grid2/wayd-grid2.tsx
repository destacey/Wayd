'use client'

import {
  forwardRef,
  Fragment,
  type Ref,
  type ReactElement,
  type ReactNode,
  useCallback,
  useImperativeHandle,
  useMemo,
  useRef,
  useState,
} from 'react'
import { Popover, Spin } from 'antd'
import type { FormInstance } from 'antd'
import { FilterFilled, FilterOutlined } from '@ant-design/icons'
import {
  type ColumnDef,
  type Header,
  type VisibilityState,
  getExpandedRowModel,
  getFacetedRowModel,
  getFacetedUniqueValues,
} from '@tanstack/react-table'
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
import {
  CombinedFilterPanel,
  DateFilterPanel,
  FilterPopup,
  FloatingFilter,
  SetFilterPanel,
  canFloatingEditDate,
  describeDateFilter,
  resolveFilterType,
  toDayKey,
  waydColumnFilter,
  type ColumnFilterModel,
  type FilterType,
} from '../wayd-grid-core/filters'

import { applyColumnType } from '../wayd-grid-core/column-types'
import { useGridDndSensors } from '../wayd-grid-core/dnd/grid-dnd'
import {
  INDENTATION_WIDTH,
  calculateOrderInParent,
  getProjection,
  type TreeNode,
} from '../wayd-grid-core/dnd/tree-projection'
import {
  mergeDraftsIntoTree,
  type DraftItem,
} from '../wayd-grid-core/draft-utils'
import { exportGridToCsv } from '../wayd-grid-core/grid-export'
import {
  GridHeaderCell,
  useResizeClickGuard,
  type GridHeaderCellClasses,
} from '../wayd-grid-core/grid-header-row'
import {
  FlatGridRow,
  TreeGridRow,
  type GridRowClasses,
  type TreeGridRowClasses,
} from '../wayd-grid-core/grid-row'
import { sortEmptyLast } from '../wayd-grid-core/grid-sorting'
import GridToolbar from '../wayd-grid-core/grid-toolbar'
import { countTreeNodes, flattenTree } from '../wayd-grid-core/tree-utils'
import {
  useGridEditing,
  type GridEditingConfig,
} from '../wayd-grid-core/use-grid-editing'
import { useGridState, useGridTable } from '../wayd-grid-core/use-grid-table'
import styles from './wayd-grid2.module.css'
import type {
  GridColumnContext,
  WaydGrid2Handle,
  WaydGrid2Props,
} from './types'

/** Stable empty set for columns with no expanded date-tree nodes yet. Never
 * mutated — toggles always copy before writing. */
const EMPTY_NODE_SET: Set<string> = new Set<string>()

const EMPTY_FIELD_ERRORS: Record<string, string> = {}
const DRAFT_SCROLL_MARGIN = 8

const NOOP_FORM = {
  validateFields: async () => ({}),
  getFieldsValue: () => ({}),
  setFieldsValue: () => {},
  resetFields: () => {},
} as unknown as FormInstance

const escapeSelectorValue = (value: string) =>
  typeof CSS !== 'undefined' && CSS.escape
    ? CSS.escape(value)
    : value.replace(/["\\]/g, '\\$&')

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

const headerCellClasses: GridHeaderCellClasses = {
  th: styles.th,
  thSortable: styles.thSortable,
  thResizable: styles.thResizable,
  thContent: styles.thContent,
  thText: styles.thText,
  resizer: styles.resizer,
  resizerActive: styles.resizerActive,
}

const rowClasses: GridRowClasses = {
  tr: styles.tr,
  trAlt: styles.trAlt,
  td: styles.td,
}

const treeRowClasses: TreeGridRowClasses = {
  ...rowClasses,
  trEditable: styles.trEditable,
  trSelected: styles.trSelected,
  editableCell: styles.editableCell,
}

function WaydGrid2Inner<T>(
  props: WaydGrid2Props<T>,
  ref: Ref<WaydGrid2Handle>,
) {
  // TanStack Table's instance mutates internally behind a stable identity, so
  // React Compiler memoization goes stale (sort icons, column sizes). Before
  // the table config moved into useGridTable, the direct useReactTable call
  // made the compiler skip this component automatically; the directive keeps
  // that behavior now that the call is behind the hook.
  'use no memo'
  const {
    data,
    columns: columnsProp,
    isLoading = false,
    onRefresh,
    leftSlot,
    helpContent,
    rightSlot,
    emptyMessage = 'No records found',
    csvFileName = 'grid-export',
    height,
    includeGlobalSearch = true,
    includeExportButton = true,
    includeColumnFilters = true,
    includeFloatingFilters = true,
    getSubRows,
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

  // Tree mode: rows form a hierarchy, expansion/indent/reparenting apply, and
  // a filter match keeps the row's ancestor chain visible.
  const isTree = !!getSubRows

  // ─── Auto-height ─────────────────────────────────────────
  const [gridContainerRef, autoHeight] = useRemainingHeight()
  const resolvedHeight = height ?? autoHeight

  // ─── State ───────────────────────────────────────────────
  const gridState = useGridState()
  const { searchValue, onSearchChange, onClearFilters, hasActiveFilters } =
    gridState
  const resizeGuard = useResizeClickGuard()
  const [openFilterColumnId, setOpenFilterColumnId] = useState<string | null>(
    null,
  )
  // Expanded date-tree node keys per column, owned here (above the filter
  // popover). The popover's content is rebuilt whenever the column filter
  // changes, so state kept inside the DateFilterPanel is lost on toggle; keeping
  // it on the grid makes the tree's expand/collapse survive checkbox changes.
  const [dateTreeExpanded, setDateTreeExpanded] = useState<
    Record<string, Set<string>>
  >({})

  // ─── Tree-mode state (drafts + DnD) ──────────────────────
  const [draggedNodeId, setDraggedNodeId] = useState<string | null>(null)
  const [draftTasks, setDraftTasks] = useState<DraftItem[]>([])
  const draftTasksRef = useRef<DraftItem[]>([])
  const isAddingDraftRef = useRef(false)
  const draftCounterRef = useRef(0)

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

  // ─── Draft management (tree mode) ────────────────────────
  const dataWithDrafts = useMemo(() => {
    if (!createDraftNode || draftTasks.length === 0) return data
    // Draft rows require tree-shaped nodes ({ id, children }); the cast is
    // localized here (same contract the old TreeGrid enforced via its bound).
    return mergeDraftsIntoTree(
      data as unknown as TreeNode[],
      draftTasks,
      createDraftNode as unknown as (draft: DraftItem) => TreeNode,
    ) as unknown as T[]
  }, [data, draftTasks, createDraftNode])

  // ─── Editing hook ────────────────────────────────────────
  const canEdit = editingConfig?.canEdit ?? false
  const draftPrefix = editingConfig?.draftPrefix ?? 'draft-'

  const editing = useGridEditing<T & { id: string }>(
    (editingConfig
      ? {
          ...editingConfig,
          data: dataWithDrafts,
          tableWrapperClassName: styles.grid,
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
          onCancelDraft: (draftId: string) => {
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
          tableWrapperClassName: styles.grid,
          editableColumnIds: [],
          onSave: async () => false,
          fieldErrors: EMPTY_FIELD_ERRORS,
          setFieldErrors: () => {},
          getFormValues: () => ({}),
          computeChanges: () => null,
          cellIdColumnMatchOrder: [],
        }) as GridEditingConfig<T & { id: string }>,
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
    canEdit &&
    !!createDraftNode &&
    !isLoading &&
    !isSaving &&
    !isAddingDraftRef.current
  const canCreateDraft = canAttemptCreateDraft && (!hasDraft || isEditing)

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
    isTree && enableDragAndDrop && !!onNodeMove && !isLoading && !isEditing

  const columnContext: GridColumnContext = useMemo(() => {
    const dragEnabledForColumns = dragEnabledBase && !hasActiveFilters

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
    hasActiveFilters,
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

  // Apply declarative column types (meta.columnType) before handing columns to
  // TanStack. Memoized on `columns` so the resolved array keeps a stable
  // identity across unrelated re-renders (e.g. a column-filter change). Without
  // this, every render rebuilt the column defs, TanStack rebuilt its headers,
  // and the filter Popover's trigger DOM node was replaced — which antd read as
  // a click-outside and closed the open popover on every checkbox toggle.
  const resolvedColumns = useMemo(
    () =>
      columns.map((col) =>
        applyColumnType(col as ColumnDef<T, unknown>),
      ) as typeof columns,
    [columns],
  )

  // Derive columnVisibility from each column's meta.hide (AG Grid `hide` style),
  // so columns can live in one static literal and be shown/hidden by a flag.
  const columnVisibility = useMemo<VisibilityState>(() => {
    const visibility: VisibilityState = {}
    for (const col of resolvedColumns) {
      const meta = col.meta
      if (meta?.hide === undefined) continue
      const id =
        col.id ??
        ((col as { accessorKey?: string | number }).accessorKey?.toString())
      if (id) visibility[id] = !meta.hide
    }
    return visibility
  }, [resolvedColumns])

  // ─── Flattened tree for DnD ──────────────────────────────
  const flattenedNodes = useMemo(
    () =>
      isTree ? flattenTree(dataWithDrafts as unknown as TreeNode[]) : [],
    [isTree, dataWithDrafts],
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
        await onNodeMove(
          active.id as string,
          projection.parentId,
          newOrder,
          over.id as string,
          overIndex,
        )
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
  const table = useGridTable({
    data: dataWithDrafts,
    columns: resolvedColumns,
    gridState,
    tableOptions: {
      getFacetedRowModel: getFacetedRowModel(),
      getFacetedUniqueValues: getFacetedUniqueValues(),
      // All per-column filters go through the descriptor engine, and all
      // columns sort empties (null/undefined/'') last-ascending, unless a
      // column overrides its own filterFn / sortingFn.
      defaultColumn: {
        filterFn: waydColumnFilter,
        sortingFn: sortEmptyLast,
      },
      // Tree mode: rows expand, and a filter match keeps the ancestor chain
      // visible (filterFromLeafRows).
      ...(isTree
        ? {
            getExpandedRowModel: getExpandedRowModel(),
            getSubRows,
            filterFromLeafRows: true,
            initialState: { expanded: true as const },
          }
        : {}),
    },
    extraState: { columnVisibility },
  })

  tableRef.current = table

  useImperativeHandle(ref, () => ({ table, selectedRowId }))

  const rows = table.getRowModel().rows
  const displayedRowCount = rows.length
  const totalRowCount = isTree
    ? countTreeNodes(data as unknown as TreeNode[]) + draftTasks.length
    : data.length
  const visibleColumnCount = table.getVisibleLeafColumns().length

  // Toggle a date-tree year/month node's expanded state for a given column.
  const toggleDateTreeNode = useCallback((columnId: string, nodeKey: string) => {
    setDateTreeExpanded((prev) => {
      const current = prev[columnId] ?? EMPTY_NODE_SET
      const next = new Set(current)
      if (next.has(nodeKey)) next.delete(nodeKey)
      else next.add(nodeKey)
      return { ...prev, [columnId]: next }
    })
  }, [])

  // ─── CSV export ──────────────────────────────────────────
  const onExportCsv = useCallback(() => {
    exportGridToCsv(table, csvFileName)
  }, [table, csvFileName])

  const showColumnFilters = useMemo(
    () =>
      includeColumnFilters &&
      table.getVisibleLeafColumns().some((c) => c.getCanFilter()),
    [includeColumnFilters, table],
  )

  const showFloatingFilters = showColumnFilters && includeFloatingFilters

  /**
   * Resolves a column's filter type. An explicit `meta.filterType` (including
   * one applied by a `columnType`) always wins; otherwise the type is inferred
   * from the column's data by sampling its faceted unique values:
   *   - all sampled values are numbers      → 'number'
   *   - all sampled values are Dates        → 'date'
   *   - otherwise                           → 'text'
   * Empty/mixed data falls back to 'text'.
   */
  const columnFilterType = (header: Header<T, unknown>): FilterType => {
    const meta = header.column.columnDef.meta
    if (meta?.filterType) return resolveFilterType(meta.filterType)

    const sample: unknown[] = []
    for (const v of header.column.getFacetedUniqueValues().keys()) {
      if (v === null || v === undefined || v === '') continue
      sample.push(v)
      if (sample.length >= 20) break // enough to infer; avoid scanning huge sets
    }
    if (sample.length === 0) return 'text'

    if (sample.every((v) => typeof v === 'number')) return 'number'
    if (sample.every((v) => v instanceof Date)) return 'date'
    return 'text'
  }

  /**
   * Wraps a trigger element in the multi-condition {@link FilterPopup} popover.
   * `trigger` defaults to the filter-icon button (used in the header and to the
   * right of floating inputs); callers can pass their own trigger — e.g. the
   * set-filter floating summary, so clicking it opens the same popup.
   */
  const renderFilterPopover = (
    header: Header<T, unknown>,
    trigger?: ReactNode,
  ) => {
    const meta = header.column.columnDef.meta
    const filterType = columnFilterType(header)
    const filterValue = header.column.getFilterValue() as
      | ColumnFilterModel
      | undefined
    const isFiltered = filterValue !== undefined
    const isFilterOpen = openFilterColumnId === header.column.id
    // Text columns can opt into a combined text + set panel.
    const combined = filterType === 'text' && meta?.filterEnableSet === true
    // Date columns get the Excel-style panel (date tree + relative + conditions).
    const isDate = filterType === 'date'

    return (
      <Popover
        trigger="click"
        placement="bottomRight"
        open={isFilterOpen}
        onOpenChange={(open) =>
          setOpenFilterColumnId(open ? header.column.id : null)
        }
        getPopupContainer={() => document.body}
        content={
          // Stop clicks inside the popup from bubbling to the <th> sort handler
          // (React events propagate through the portal to the logical parent).
          <div onClick={(e) => e.stopPropagation()}>
            {isDate ? (
              <DateFilterPanel
                dayKeys={getDayKeys(header)}
                value={filterValue}
                maxConditions={meta?.maxFilterConditions}
                onChange={(next) => header.column.setFilterValue(next)}
                expandedNodes={
                  dateTreeExpanded[header.column.id] ?? EMPTY_NODE_SET
                }
                onToggleNode={(key) => toggleDateTreeNode(header.column.id, key)}
              />
            ) : combined ? (
              <CombinedFilterPanel
                allValues={getSetValues(header)}
                labels={meta?.filterOptions}
                value={filterValue}
                maxConditions={meta?.maxFilterConditions}
                onChange={(next) => header.column.setFilterValue(next)}
              />
            ) : (
              <FilterPopup
                filterType={filterType}
                value={filterValue}
                maxConditions={meta?.maxFilterConditions}
                onChange={(next) => header.column.setFilterValue(next)}
              />
            )}
          </div>
        }
      >
        {trigger ?? (
          <span
            role="button"
            aria-label={
              isFiltered ? 'Edit column filter (active)' : 'Filter column'
            }
            className={`${styles.filterTrigger}${
              isFiltered ? ` ${styles.filterTriggerActive}` : ''
            }`}
            onClick={(e) => e.stopPropagation()}
          >
            {isFiltered ? <FilterFilled /> : <FilterOutlined />}
          </span>
        )}
      </Popover>
    )
  }

  /**
   * All known set values for a column: prefer declared options (stable
   * order/labels), else the distinct values present in the data (faceted).
   */
  const getSetValues = (header: Header<T, unknown>): string[] => {
    const meta = header.column.columnDef.meta
    const optionValues = meta?.filterOptions?.map((o) => o.value)
    if (optionValues) return optionValues
    return Array.from(header.column.getFacetedUniqueValues().keys())
      .filter((v): v is string => v != null && v !== '')
      .map(String)
      .sort()
  }

  /**
   * Distinct `YYYY-MM-DD` day keys present in a date column, for the date tree.
   * Each faceted value is normalized to its day (dropping time-of-day and raw
   * shape differences) and de-duplicated.
   */
  const getDayKeys = (header: Header<T, unknown>): string[] => {
    const keys = new Set<string>()
    for (const v of header.column.getFacetedUniqueValues().keys()) {
      const key = toDayKey(v)
      if (key) keys.add(key)
    }
    return Array.from(keys).sort()
  }

  /**
   * Excel/AG Grid-style set filter for a column: a compact display box plus a
   * filter icon that opens the {@link SetFilterPanel} (search + Select All +
   * checkboxes). Values are derived from the data (faceted unique values),
   * falling back to the column's declared filterOptions.
   */
  const renderSetFilter = (header: Header<T, unknown>) => {
    const column = header.column
    const meta = column.columnDef.meta
    const filterValue = column.getFilterValue() as
      | ColumnFilterModel
      | undefined
    const isFiltered = filterValue !== undefined
    const isFilterOpen = openFilterColumnId === column.id

    const allValues = getSetValues(header)

    const selected =
      filterValue?.type === 'set' ? filterValue.values : allValues
    const summary =
      !isFiltered || selected.length === allValues.length
        ? ''
        : selected.length === 1
          ? meta?.filterOptions?.find((o) => o.value === selected[0])?.label ??
            selected[0]
          : `${selected.length} selected`

    return (
      <Popover
        trigger="click"
        placement="bottomLeft"
        open={isFilterOpen}
        onOpenChange={(open) =>
          setOpenFilterColumnId(open ? column.id : null)
        }
        getPopupContainer={() => document.body}
        content={
          <div onClick={(e) => e.stopPropagation()}>
            <SetFilterPanel
              allValues={allValues}
              labels={meta?.filterOptions}
              value={filterValue}
              onChange={(next) => column.setFilterValue(next)}
            />
          </div>
        }
      >
        <div
          role="button"
          aria-label={isFiltered ? 'Filter column (active)' : 'Filter column'}
          className={styles.floatingCell}
        >
          <span className={styles.setSummary}>{summary}</span>
          <span
            className={`${styles.filterTrigger}${
              isFiltered ? ` ${styles.filterTriggerActive}` : ''
            }`}
          >
            {isFiltered ? <FilterFilled /> : <FilterOutlined />}
          </span>
        </div>
      </Popover>
    )
  }

  // ─── Row/cell click handlers (editing) ──────────────────
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
  const dndEnabled = isTree && enableDragAndDrop && !!onNodeMove
  const isDragEnabled =
    dndEnabled && !isLoading && !isEditing && !hasActiveFilters

  // ─── Render ──────────────────────────────────────────────
  // While loading/empty, the table stretches to fill the wrapper so the
  // single status row (and its spinner / empty message) centers vertically.
  const showStatusRow = isLoading || rows.length === 0

  const tableContent = (
    <div className={styles.tableWrapper}>
      <table
        className={`${styles.tableElement}${
          showStatusRow ? ` ${styles.tableElementFill}` : ''
        }`}
      >
        <colgroup>
          {table.getVisibleLeafColumns().map((column) => (
            <col key={column.id} width={column.getSize()} />
          ))}
          {/* Filler column absorbs leftover width so the table fills the
              wrapper (header band + borders edge-to-edge) without widening
              the real columns. */}
          <col className={styles.fillerCol} />
        </colgroup>
        <thead>
          {table.getHeaderGroups().map((headerGroup) => (
            <Fragment key={headerGroup.id}>
              {/* Header row */}
              <tr key={headerGroup.id}>
                {headerGroup.headers.map((header) => {
                  // When floating filters are shown, the popup trigger lives
                  // in the floating row instead of the header.
                  const showHeaderFilterIcon =
                    showColumnFilters &&
                    !showFloatingFilters &&
                    !header.isPlaceholder &&
                    header.column.getCanFilter()

                  return (
                    <GridHeaderCell
                      key={header.id}
                      header={header}
                      resizeGuard={resizeGuard}
                      classes={headerCellClasses}
                      filterSlot={
                        showHeaderFilterIcon
                          ? renderFilterPopover(header)
                          : undefined
                      }
                    />
                  )
                })}
                {/* Filler header cell — carries the header band across the
                    empty width. */}
                <th
                  aria-hidden="true"
                  className={`${styles.th} ${styles.fillerTh}`}
                />
              </tr>

              {/* Floating filter row — single-condition editor per column,
                  reflecting conditions[0] of the same descriptor. */}
              {showFloatingFilters && (
                <tr
                  key={`${headerGroup.id}-floating`}
                  data-role="floating-filters"
                >
                  {headerGroup.headers.map((header) => {
                    const column = header.column
                    if (header.isPlaceholder || !column.getCanFilter()) {
                      return (
                        <th
                          key={`${header.id}-floating`}
                          className={styles.filterTh}
                        />
                      )
                    }

                    const meta = column.columnDef.meta
                    const colFilterType = columnFilterType(header)
                    const colFilterValue = column.getFilterValue() as
                      | ColumnFilterModel
                      | undefined

                    // Set filters open the Excel-style SetFilterPanel (search
                    // + Select All + checkboxes) from a compact box + icon.
                    if (colFilterType === 'set') {
                      return (
                        <th
                          key={`${header.id}-floating`}
                          className={styles.filterTh}
                          onClick={(e) => e.stopPropagation()}
                        >
                          {renderSetFilter(header)}
                        </th>
                      )
                    }

                    // Date columns: the floating DatePicker can faithfully show
                    // and edit only a simple equals. A complex date filter
                    // (before/after/inRange, a relative range, a date-tree
                    // selection, or multiple conditions) would otherwise render
                    // as a bare date and mislead — so show a read-only summary
                    // chip in place of the input instead.
                    //
                    // Crucially, the cell structure is identical in both states:
                    // a `.floatingCell` whose LEFT child is the input or the
                    // chip, and whose RIGHT child is always the same filter-icon
                    // popover trigger from renderFilterPopover(). The popover is
                    // therefore always anchored to that stable icon — swapping
                    // the left child (input ⇄ chip) when the filter activates
                    // never replaces the popover's anchor, so it doesn't
                    // close/reopen (flicker) as the filter crosses the
                    // unfiltered ⇄ active boundary.
                    const showDateSummary =
                      colFilterType === 'date' &&
                      !canFloatingEditDate(colFilterValue)

                    return (
                      <th
                        key={`${header.id}-floating`}
                        className={styles.filterTh}
                        onClick={(e) => e.stopPropagation()}
                      >
                        <div className={styles.floatingCell}>
                          {showDateSummary ? (
                            <span
                              className={styles.setSummary}
                              title={describeDateFilter(colFilterValue)}
                            >
                              {describeDateFilter(colFilterValue)}
                            </span>
                          ) : (
                            <FloatingFilter
                              filterType={colFilterType}
                              // Only reflect a matching condition descriptor. On
                              // a combined column, a `set` descriptor leaves the
                              // floating input empty (AG Grid behavior).
                              value={
                                colFilterValue?.type === colFilterType
                                  ? colFilterValue
                                  : undefined
                              }
                              placeholder={meta?.filterPlaceholder}
                              onChange={(next) => column.setFilterValue(next)}
                            />
                          )}
                          {renderFilterPopover(header)}
                        </div>
                      </th>
                    )
                  })}
                  {/* Filler cell carries the filter-row band to the edge. */}
                  <th
                    aria-hidden="true"
                    className={`${styles.filterTh} ${styles.fillerTh}`}
                  />
                </tr>
              )}
            </Fragment>
          ))}
        </thead>
        <tbody>
          {isLoading ? (
            <tr>
              {/* Centering lives on the inner div — `display: flex` directly
                  on a <td> breaks table-cell rendering in browsers. */}
              <td colSpan={visibleColumnCount + 1} className={styles.td}>
                <div className={styles.loading}>
                  <Spin size="large" />
                </div>
              </td>
            </tr>
          ) : rows.length === 0 ? (
            <tr>
              <td colSpan={visibleColumnCount + 1} className={styles.td}>
                <div className={styles.empty}>
                  <WaydEmpty message={emptyMessage} />
                </div>
              </td>
            </tr>
          ) : isTree ? (
            rows.flatMap((row, index) => {
              const nodeId = (row.original as { id: string }).id
              const isSelected = selectedRowId === nodeId
              const isRowDragging = draggedNodeId === nodeId
              const isDraftRow = nodeId.startsWith(draftPrefix)
              const rowElements = [
                <TreeGridRow
                  key={row.id}
                  row={row}
                  index={index}
                  classes={treeRowClasses}
                  nodeId={nodeId}
                  isSelected={isSelected}
                  isDragging={isRowDragging}
                  isDragEnabled={isDragEnabled && !isDraftRow}
                  canEdit={canEdit}
                  editableColumns={editableColumns}
                  onRowClick={handleRowClickWithContext}
                  onCellClick={handleCellClick}
                />,
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
                      colSpan={visibleColumnCount + 1}
                      className={`${styles.td} ${styles.validationErrorCell}`}
                    >
                      {errorItems}
                    </td>
                  </tr>,
                )
              }

              return rowElements
            })
          ) : (
            rows.map((row, index) => (
              <FlatGridRow
                key={row.id}
                row={row}
                index={index}
                classes={rowClasses}
              />
            ))
          )}
        </tbody>
      </table>
    </div>
  )

  return (
    <div
      ref={gridContainerRef}
      className={styles.grid}
      style={{ height: resolvedHeight }}
    >
      <GridToolbar
        displayedRowCount={displayedRowCount}
        totalRowCount={totalRowCount}
        searchValue={searchValue}
        onSearchChange={onSearchChange}
        onRefresh={onRefresh}
        onClearFilters={onClearFilters}
        hasActiveFilters={hasActiveFilters}
        onExportCsv={includeExportButton ? onExportCsv : undefined}
        isLoading={isLoading}
        includeGlobalSearch={includeGlobalSearch}
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
          <SortableContext
            items={flattenedNodes.map((t) => t.node.id)}
            strategy={verticalListSortingStrategy}
          >
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
 * The unified Wayd data grid, assembled from the shared grid core (toolbar,
 * header cells, row renderers, table/state hooks, descriptor filter engine,
 * CSV export). Flat by default; provide `getSubRows` to turn on tree mode —
 * expansion, `filterFromLeafRows` (a matching child keeps its ancestor chain
 * visible), and, when configured, reparenting drag-and-drop, inline editing,
 * and draft rows. Rows render through the row-renderer seam: the flat form
 * ({@link FlatGridRow}) or the tree form ({@link TreeGridRow}).
 *
 * Named WaydGrid2 while the ag-grid WaydGrid still exists; takes the
 * canonical WaydGrid name when ag-grid is retired.
 */
const WaydGrid2 = forwardRef(WaydGrid2Inner) as <T>(
  props: WaydGrid2Props<T> & { ref?: Ref<WaydGrid2Handle> },
) => ReactElement | null

export default WaydGrid2
