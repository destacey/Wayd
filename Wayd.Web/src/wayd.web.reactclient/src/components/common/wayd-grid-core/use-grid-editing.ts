import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import type { FormInstance } from 'antd'

/**
 * Arguments passed to the row click handler.
 */
export interface RowClickArgs {
  rowId: string
  isEditableColumn: (columnId: string) => boolean
  getClickedColumnId: (target: HTMLElement) => string | null
}

/**
 * Configuration for the inline editing hook.
 */
export interface GridEditingConfig<T extends { id: string }> {
  /** The row data (including any merged drafts). */
  data: T[]

  /** Whether the user has permission to edit. */
  canEdit: boolean

  /** Ant Design Form instance for managing inline form state. */
  form: FormInstance

  /** CSS class name of the table wrapper element (used for click-outside detection). */
  tableWrapperClassName: string

  /**
   * Which columns are editable. Can be a static list or a function
   * that returns different columns based on the selected row (e.g., drafts may have extra editable fields).
   */
  editableColumnIds: string[] | ((selectedRowId: string | null) => string[])

  /**
   * Called to save changes for a row. Returns true on success, false on failure.
   * For draft rows, this creates a new item. For existing rows, this patches the item.
   */
  onSave: (rowId: string, updates: Record<string, any>) => Promise<boolean>

  /** Current field-level validation errors. */
  fieldErrors: Record<string, string>

  /** Setter for field-level validation errors. */
  setFieldErrors: (errors: Record<string, string>) => void

  /**
   * Given a row ID and the current data, return the initial form values.
   * Domain-specific: the generic hook doesn't know field shapes.
   */
  getFormValues: (rowId: string, data: T[]) => Record<string, any>

  /**
   * Given a row ID, current form values, and original data, compute
   * the changed fields. Returns null if nothing changed (skip save).
   * For drafts, typically return all form values.
   */
  computeChanges: (
    rowId: string,
    formValues: Record<string, any>,
    data: T[],
  ) => Record<string, any> | null

  /**
   * Optional client-side cross-field validation before save.
   * Return field errors or empty object.
   */
  validateFields?: (
    rowId: string,
    formValues: Record<string, any>,
  ) => Record<string, string>

  /**
   * Column IDs ordered for suffix matching (longest-suffix first).
   * Used by focusCellById to identify which column a cell ID refers to.
   * Example: ['estimatedEffortHours', 'plannedStart', 'plannedEnd', 'assignees', 'priority', 'status', 'progress', 'type', 'name']
   */
  cellIdColumnMatchOrder: readonly string[]

  /** Called when a draft row is cancelled (Escape on a draft). */
  onCancelDraft?: (rowId: string) => void

  /** Prefix used to identify draft rows (default: 'draft-'). */
  draftPrefix?: string
}

/**
 * Generic inline editing hook for grid components. Grid-agnostic: rows only
 * need an `id` — tree grids and flat grids configure it the same way.
 *
 * Provides:
 * - Row/cell selection state management
 * - MutationObserver-based focus management for Ant Design form controls
 * - Keyboard navigation (Tab, Shift+Tab, Enter, Escape, ArrowUp, ArrowDown)
 * - Click-outside save behavior (with dropdown/picker exclusion)
 * - Save-before-navigate pattern
 * - Draft row support
 *
 * Domain-specific logic (form initialization, change detection, validation)
 * is provided via callbacks in the config object.
 */
export function useGridEditing<T extends { id: string }>(
  config: GridEditingConfig<T>,
) {
  const {
    data,
    canEdit,
    form,
    tableWrapperClassName,
    editableColumnIds,
    onSave,
    fieldErrors,
    setFieldErrors,
    getFormValues,
    computeChanges,
    validateFields,
    cellIdColumnMatchOrder,
    onCancelDraft,
    draftPrefix = 'draft-',
  } = config

  const [selectedRowId, setSelectedRowId] = useState<string | null>(null)
  const [selectedCellId, setSelectedCellId] = useState<string | null>(null)
  const [isSaving, setIsSaving] = useState(false)

  const tableRef = useRef<any>(null)
  const isInitializingRef = useRef(false)
  const lastFocusedCellRef = useRef<string | null>(null)
  const focusRequestTokenRef = useRef(0)
  const focusObserverRef = useRef<MutationObserver | null>(null)

  // Store callback props in refs to avoid re-triggering effects when
  // consumers pass inline functions (new reference each render).
  const getFormValuesRef = useRef(getFormValues)
  const computeChangesRef = useRef(computeChanges)
  const validateFieldsRef = useRef(validateFields)
  const onSaveRef = useRef(onSave)
  const onCancelDraftRef = useRef(onCancelDraft)
  const setFieldErrorsRef = useRef(setFieldErrors)
  const fieldErrorsRef = useRef(fieldErrors)

  useEffect(() => {
    getFormValuesRef.current = getFormValues
    computeChangesRef.current = computeChanges
    validateFieldsRef.current = validateFields
    onSaveRef.current = onSave
    onCancelDraftRef.current = onCancelDraft
    setFieldErrorsRef.current = setFieldErrors
    fieldErrorsRef.current = fieldErrors
  })

  // Resolve editable columns for a given row (or the currently selected row)
  const resolveEditableColumns = useCallback(
    (rowId: string | null) => {
      if (typeof editableColumnIds === 'function') {
        return editableColumnIds(rowId)
      }
      return editableColumnIds
    },
    [editableColumnIds],
  )

  // Resolve editable columns (may be static or dynamic based on selected row)
  const editableColumns = useMemo(
    () => resolveEditableColumns(selectedRowId),
    [resolveEditableColumns, selectedRowId],
  )

  const getFieldError = useCallback(
    (fieldName: string): string | undefined => {
      return fieldErrors[fieldName]
    },
    [fieldErrors],
  )

  // Focus management using MutationObserver for DOM stability
  const focusCellById = useCallback(
    (cellId: string) => {
      focusObserverRef.current?.disconnect()
      focusObserverRef.current = null
      const requestToken = ++focusRequestTokenRef.current

      let columnId = ''
      for (const col of cellIdColumnMatchOrder) {
        if (cellId.endsWith(`-${col}`)) {
          columnId = col
          break
        }
      }

      const isActiveElementInsideCell = () => {
        const active = document.activeElement as HTMLElement | null
        const activeCellId = active
          ?.closest?.('[data-cell-id]')
          ?.getAttribute('data-cell-id')
        return activeCellId === cellId
      }

      const getActiveCellId = () => {
        const active = document.activeElement as HTMLElement | null
        return active
          ?.closest?.('[data-cell-id]')
          ?.getAttribute('data-cell-id')
      }

      const tryFocus = (attempt: number) => {
        if (focusRequestTokenRef.current !== requestToken) {
          return
        }

        let cellElement: Element | null = null
        const allCells = document.querySelectorAll('[data-cell-id]')
        for (const cell of allCells) {
          if (cell.getAttribute('data-cell-id') === cellId) {
            cellElement = cell
            break
          }
        }

        if (cellElement) {
          const cellElementNode = cellElement as HTMLElement

          let input: HTMLElement | null = null

          // Try DatePicker first
          const picker = cellElement.querySelector('.ant-picker') as
            | HTMLElement
            | null
          const pickerInput = cellElement.querySelector(
            '.ant-picker-input > input',
          ) as HTMLElement | null

          if (picker && pickerInput) {
            picker.focus({ preventScroll: true })
            input = pickerInput
          } else {
            // Try Select
            const selectInput = cellElement.querySelector(
              'input.ant-select-input',
            ) as HTMLElement | null
            if (selectInput) {
              input = selectInput
            } else {
              // Try regular input
              input = cellElement.querySelector('input')
            }
          }

          if (!input) {
            input = cellElement.querySelector('.ant-picker')
          }

          if (!input) {
            input = cellElement.querySelector(
              '[data-color-picker-focus]',
            ) as HTMLElement | null
          }

          if (!input) {
            input = cellElement.querySelector(
              '.ant-color-picker-trigger',
            ) as HTMLElement | null
          }

          if (input instanceof HTMLElement) {
            input.focus({ preventScroll: true })
            if (input instanceof HTMLInputElement) {
              input.select()
            }

            if (!isActiveElementInsideCell()) {
              if (attempt < 12) {
                setTimeout(() => tryFocus(attempt + 1), 20)
              }
              return
            }

            // DatePicker inputs can be re-mounted during state updates.
            // Re-check shortly after and re-focus if focus was lost.
            if (picker && pickerInput) {
              setTimeout(() => {
                if (!isActiveElementInsideCell()) {
                  const activeCellId = getActiveCellId()
                  if (activeCellId && activeCellId !== cellId) {
                    return
                  }
                  tryFocus(6)
                }
              }, 40)
            }
            return
          }
        }

        if (attempt < 12) {
          setTimeout(() => tryFocus(attempt + 1), 20)
        }
      }

      // Use MutationObserver to wait for DOM stability before focusing
      const cellEl = document.querySelector(`[data-cell-id="${cellId}"]`)
      const observeTarget = cellEl?.closest('tr') ?? document.body
      let stabilityTimer: ReturnType<typeof setTimeout> | null = null
      const observer = new MutationObserver(() => {
        if (stabilityTimer) clearTimeout(stabilityTimer)
        stabilityTimer = setTimeout(() => {
          observer.disconnect()
          focusObserverRef.current = null
          tryFocus(0)
        }, 50)
      })
      focusObserverRef.current = observer
      observer.observe(observeTarget, {
        childList: true,
        subtree: true,
      })
      // Kick off the timer in case there are no mutations
      stabilityTimer = setTimeout(() => {
        observer.disconnect()
        tryFocus(0)
      }, 50)
    },
    [cellIdColumnMatchOrder],
  )

  // Save form changes for a row
  const saveFormChanges = useCallback(
    async (rowId: string) => {
      try {
        // Run client-side validation if provided
        if (validateFieldsRef.current) {
          const validationErrors = validateFieldsRef.current(
            rowId,
            form.getFieldsValue(),
          )
          if (Object.keys(validationErrors).length > 0) {
            setFieldErrorsRef.current(validationErrors)
            return false
          }
        }

        await form.validateFields()

        const formValues = form.getFieldsValue()

        // Clear any prior inline errors once local validation passes
        if (Object.keys(fieldErrorsRef.current).length > 0) {
          setFieldErrorsRef.current({})
        }

        // Use domain-specific change detection
        const changes = computeChangesRef.current(rowId, formValues, data)
        if (changes === null) {
          return true // No changes, nothing to save
        }

        setIsSaving(true)
        const success = await onSaveRef.current(rowId, changes)
        setIsSaving(false)
        return Boolean(success)
      } catch {
        setIsSaving(false)
        return false
      }
    },
    [data, form],
  )

  // Initialize form when row selection changes
  useEffect(() => {
    isInitializingRef.current = true

    if (selectedRowId) {
      form.resetFields()
      setFieldErrorsRef.current({})

      const formValues = getFormValuesRef.current(selectedRowId, data)
      form.setFieldsValue(formValues)
      isInitializingRef.current = false
    } else {
      form.resetFields()
      isInitializingRef.current = false
    }

    lastFocusedCellRef.current = null
  }, [data, form, selectedRowId])

  // Focus the target cell when selectedCellId changes
  useEffect(() => {
    if (!selectedRowId || !selectedCellId) return
    if (lastFocusedCellRef.current === selectedCellId) return

    lastFocusedCellRef.current = selectedCellId
    focusCellById(selectedCellId)
  }, [focusCellById, selectedRowId, selectedCellId])

  // Handle click outside table to save changes and exit edit mode
  useEffect(() => {
    if (!selectedRowId) return

    const handleClickOutside = async (event: MouseEvent) => {
      const target = event.target as HTMLElement

      if (
        target.closest('.ant-select-dropdown') ||
        target.closest('.ant-picker-dropdown') ||
        target.closest('.ant-color-picker')
      ) {
        return
      }

      // Treat interacting with a filter row like an "outside" click (covers
      // both the legacy inline filter row and the floating-filter row).
      const clickedInFilterRow = Boolean(
        target.closest(
          '[data-role="column-filters"], [data-role="floating-filters"]',
        ),
      )

      if (clickedInFilterRow) {
        const saved = await saveFormChanges(selectedRowId)
        if (saved) {
          setSelectedRowId(null)
          setSelectedCellId(null)
        }
        return
      }

      const clickedInsideTable = target.closest(
        `.${tableWrapperClassName}`,
      )

      if (clickedInsideTable) {
        return
      }

      const saved = await saveFormChanges(selectedRowId)
      if (saved) {
        setSelectedRowId(null)
        setSelectedCellId(null)
      }
    }

    document.addEventListener('mousedown', handleClickOutside)
    return () => {
      document.removeEventListener('mousedown', handleClickOutside)
    }
  }, [selectedRowId, saveFormChanges, tableWrapperClassName])

  // Global keyboard handler for navigation when row is selected
  useEffect(() => {
    if (!selectedRowId || !tableRef.current) return

    const handleGlobalKeyDown = async (e: KeyboardEvent) => {
      if (isSaving) return

      const activeElement = document.activeElement
      const currentCellElement = activeElement?.closest('[data-cell-id]')

      if (
        (e.key === 'Enter' ||
          e.key === 'ArrowUp' ||
          e.key === 'ArrowDown') &&
        !currentCellElement
      ) {
        if (
          document.querySelector(
            '.ant-select-dropdown:not(.ant-select-dropdown-hidden), .ant-picker-dropdown:not(.ant-picker-dropdown-hidden)',
          )
        ) {
          return
        }

        if (activeElement?.closest(`.${tableWrapperClassName}`)) {
          return
        }

        const eventTarget = e.target as HTMLElement
        if (
          eventTarget?.closest?.('[data-cell-id]') ||
          eventTarget?.closest?.('.ant-select') ||
          eventTarget?.closest?.('.ant-picker')
        ) {
          return
        }

        e.preventDefault()

        const rows = tableRef.current.getRowModel().rows
        const currentRowIndex = rows.findIndex(
          (r: any) => r.original.id === selectedRowId,
        )
        if (currentRowIndex === -1) return

        let targetRowId: string | null = null

        if (e.key === 'Enter' || e.key === 'ArrowDown') {
          if (currentRowIndex < rows.length - 1) {
            targetRowId = rows[currentRowIndex + 1].original.id
          }
        } else if (e.key === 'ArrowUp') {
          if (currentRowIndex > 0) {
            targetRowId = rows[currentRowIndex - 1].original.id
          }
        }

        if (targetRowId) {
          const saved = await saveFormChanges(selectedRowId)
          if (!saved) return
          setSelectedRowId(targetRowId)
          setSelectedCellId(`${targetRowId}-${editableColumns[0]}`)
        }
      }
    }

    document.addEventListener('keydown', handleGlobalKeyDown, true)
    return () => {
      document.removeEventListener('keydown', handleGlobalKeyDown, true)
    }
  }, [
    editableColumns,
    isSaving,
    saveFormChanges,
    selectedRowId,
    tableWrapperClassName,
  ])

  // Per-cell keyboard handler
  const handleKeyDown = useCallback(
    async (e: React.KeyboardEvent, rowId: string, columnId: string) => {
      if (!selectedRowId || !tableRef.current) return

      if (isSaving) {
        e.preventDefault()
        return
      }

      const rows = tableRef.current.getRowModel().rows
      const currentRowIndex = rows.findIndex(
        (r: any) => r.original.id === rowId,
      )
      if (currentRowIndex === -1) return

      const currentColIndex = editableColumns.indexOf(columnId)
      if (currentColIndex === -1) return

      let nextRowId: string | null = null
      let nextColId: string | null = null

      switch (e.key) {
        case 'Enter':
          if (
            document.querySelector(
              '.ant-select-dropdown:not(.ant-select-dropdown-hidden), .ant-picker-dropdown:not(.ant-picker-dropdown-hidden)',
            )
          ) {
            return
          }
          e.preventDefault()
          {
            const saved = await saveFormChanges(selectedRowId)
            if (saved) {
              if (currentRowIndex < rows.length - 1) {
                nextRowId = rows[currentRowIndex + 1].original.id
                const targetCols = resolveEditableColumns(nextRowId)
                nextColId = targetCols[0]
                setSelectedRowId(nextRowId)
                setSelectedCellId(`${nextRowId}-${nextColId}`)
              } else {
                setSelectedRowId(null)
                setSelectedCellId(null)
              }
            }
          }
          return

        case 'ArrowUp':
          if (
            document.querySelector(
              '.ant-select-dropdown:not(.ant-select-dropdown-hidden), .ant-picker-dropdown:not(.ant-picker-dropdown-hidden)',
            )
          ) {
            return
          }
          e.preventDefault()
          if (currentRowIndex > 0) {
            nextRowId = rows[currentRowIndex - 1].original.id
            const targetCols = resolveEditableColumns(nextRowId)
            nextColId = targetCols[0]
            await saveFormChanges(selectedRowId)
            setSelectedRowId(nextRowId)
            setSelectedCellId(`${nextRowId}-${nextColId}`)
            return
          }
          break

        case 'ArrowDown':
          if (
            document.querySelector(
              '.ant-select-dropdown:not(.ant-select-dropdown-hidden), .ant-picker-dropdown:not(.ant-picker-dropdown-hidden)',
            )
          ) {
            return
          }
          e.preventDefault()
          if (currentRowIndex < rows.length - 1) {
            nextRowId = rows[currentRowIndex + 1].original.id
            const targetCols = resolveEditableColumns(nextRowId)
            nextColId = targetCols.includes(columnId) ? columnId : targetCols[0]
            await saveFormChanges(selectedRowId)
            setSelectedRowId(nextRowId)
            setSelectedCellId(`${nextRowId}-${nextColId}`)
            return
          }
          break

        case 'Escape':
          e.preventDefault()
          if (rowId.startsWith(draftPrefix) && onCancelDraftRef.current) {
            onCancelDraftRef.current(rowId)
          }
          setSelectedRowId(null)
          setSelectedCellId(null)
          return

        case 'Tab': {
          e.preventDefault()
          e.stopPropagation()

          if (document.activeElement instanceof HTMLElement) {
            document.activeElement.blur()
          }

          const findNextColInCurrentRow = (
            startColIdx: number,
            direction: 1 | -1,
          ): string | null => {
            let idx = startColIdx
            while (idx >= 0 && idx < editableColumns.length) {
              const col = editableColumns[idx]
              const cell = document.querySelector(
                `[data-cell-id="${rowId}-${col}"]`,
              )
              if (
                cell &&
                cell.querySelector(
                  'input, .ant-select, .ant-picker, .ant-color-picker, .ant-color-picker-trigger, [data-color-picker-focus]',
                )
              ) {
                return col
              }
              idx += direction
            }
            return null
          }

          if (e.shiftKey) {
            const col = findNextColInCurrentRow(currentColIndex - 1, -1)
            if (col) {
              nextColId = col
              nextRowId = rowId
            } else if (currentRowIndex > 0) {
              nextRowId = rows[currentRowIndex - 1].original.id
              const targetCols = resolveEditableColumns(nextRowId)
              nextColId = targetCols[targetCols.length - 1]
            } else {
              await saveFormChanges(rowId)
              setSelectedRowId(null)
              setSelectedCellId(null)
              return
            }
          } else {
            const col = findNextColInCurrentRow(currentColIndex + 1, 1)
            if (col) {
              nextColId = col
              nextRowId = rowId
            } else if (currentRowIndex < rows.length - 1) {
              nextRowId = rows[currentRowIndex + 1].original.id
              const targetCols = resolveEditableColumns(nextRowId)
              nextColId = targetCols[0]
            } else {
              await saveFormChanges(rowId)
              setSelectedRowId(null)
              setSelectedCellId(null)
              return
            }
          }
          break
        }
      }

      if (nextRowId && nextColId) {
        if (nextRowId !== rowId) {
          const tabSaved = await saveFormChanges(rowId)
          if (!tabSaved) return
        }

        setSelectedRowId(nextRowId)
        setSelectedCellId(`${nextRowId}-${nextColId}`)
      }
    },
    [
      draftPrefix,
      editableColumns,
      isSaving,
      resolveEditableColumns,
      saveFormChanges,
      selectedRowId,
    ],
  )

  // Row click handler
  const handleRowClick = useCallback(
    async (e: React.MouseEvent, args: RowClickArgs) => {
      if (isSaving || !canEdit) {
        return
      }

      const target = e.target as HTMLElement
      if (
        target.closest('.ant-select-dropdown') ||
        target.closest('.ant-picker-dropdown') ||
        target.closest('.ant-color-picker') ||
        target.closest('input') ||
        target.closest('.ant-select-selector') ||
        target.closest('.ant-color-picker-trigger') ||
        target.classList.contains('ant-select-item-option-content')
      ) {
        return
      }

      if (
        target.closest('button') ||
        target.closest('.ant-btn') ||
        target.closest('.ant-dropdown-trigger') ||
        target.closest('.ant-dropdown-menu')
      ) {
        return
      }

      const clickedColumnId = args.getClickedColumnId(target) ?? editableColumns[0]
      const isEditable = args.isEditableColumn(clickedColumnId)

      if (selectedRowId === args.rowId) {
        if (isEditable) {
          const targetCellId = `${args.rowId}-${clickedColumnId}`
          if (selectedCellId !== targetCellId) {
            setSelectedCellId(targetCellId)
          } else {
            focusCellById(targetCellId)
          }
        } else {
          const targetCellId = `${args.rowId}-${editableColumns[0]}`
          if (selectedCellId !== targetCellId) {
            setSelectedCellId(targetCellId)
          } else {
            focusCellById(targetCellId)
          }
        }
      } else if (selectedRowId) {
        const saved = await saveFormChanges(selectedRowId)
        if (saved) {
          const targetColumns = resolveEditableColumns(args.rowId)
          const targetIsEditable = targetColumns.includes(clickedColumnId)
          setSelectedRowId(args.rowId)
          const targetCellId = targetIsEditable
            ? `${args.rowId}-${clickedColumnId}`
            : `${args.rowId}-${targetColumns[0]}`
          setSelectedCellId(targetCellId)
        }
      } else {
        const targetColumns = resolveEditableColumns(args.rowId)
        const targetIsEditable = targetColumns.includes(clickedColumnId)
        setSelectedRowId(args.rowId)
        const targetCellId = targetIsEditable
          ? `${args.rowId}-${clickedColumnId}`
          : `${args.rowId}-${targetColumns[0]}`
        setSelectedCellId(targetCellId)
      }
    },
    [
      canEdit,
      editableColumns,
      focusCellById,
      isSaving,
      resolveEditableColumns,
      saveFormChanges,
      selectedCellId,
      selectedRowId,
    ],
  )

  // Intercept Tab on the Select's inner <input> to prevent rc-select from
  // treating Tab as a selection key (it handles Tab identically to Enter).
  // rc-select processes keydown on its container *after* the input's keydown
  // bubbles up, so stopping propagation here prevents the unwanted selection.
  //
  // Because stopPropagation also blocks our own onKeyDown handler on the
  // Select container, we call handleKeyDown directly for Tab events.
  const createSelectInputKeyDown = useCallback(
    (rowId: string, columnId: string) =>
      (e: React.KeyboardEvent<HTMLInputElement | HTMLTextAreaElement>) => {
        if (e.key === 'Tab') {
          e.stopPropagation()
          // Synthesize the call that stopPropagation blocked
          handleKeyDown(
            e as unknown as React.KeyboardEvent,
            rowId,
            columnId,
          )
        }
      },
    [handleKeyDown],
  )

  return {
    tableRef,
    selectedRowId,
    selectedCellId,
    setSelectedRowId,
    setSelectedCellId,
    isSaving,
    getFieldError,
    editableColumns,
    saveFormChanges,
    handleKeyDown,
    createSelectInputKeyDown,
    handleRowClick,
  }
}
