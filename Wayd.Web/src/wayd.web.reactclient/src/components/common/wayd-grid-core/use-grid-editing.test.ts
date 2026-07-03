import { renderHook, act } from '@testing-library/react'
import { useGridEditing, type GridEditingConfig } from './use-grid-editing'

interface TestNode {
  id: string
  name: string
  value: number
  children: TestNode[]
}

const createTestNode = (
  id: string,
  name: string,
  children: TestNode[] = [],
): TestNode => ({
  id,
  name,
  value: 0,
  children,
})

// Minimal mock of Ant Design FormInstance
const createMockForm = () => {
  const values: Record<string, any> = {}
  return {
    getFieldsValue: jest.fn(() => ({ ...values })),
    setFieldsValue: jest.fn((v: Record<string, any>) => {
      Object.assign(values, v)
    }),
    resetFields: jest.fn(() => {
      for (const key of Object.keys(values)) {
        delete values[key]
      }
    }),
    validateFields: jest.fn(() => Promise.resolve()),
    isFieldsTouched: jest.fn(() => false),
    _values: values,
  }
}

const createDefaultConfig = (
  overrides: Partial<GridEditingConfig<TestNode>> = {},
): GridEditingConfig<TestNode> => {
  const data = [
    createTestNode('1', 'Node 1'),
    createTestNode('2', 'Node 2'),
    createTestNode('3', 'Node 3'),
  ]

  return {
    data,
    canEdit: true,
    form: createMockForm() as any,
    tableWrapperClassName: 'tableWrapper',
    editableColumnIds: ['name', 'value'],
    onSave: jest.fn(() => Promise.resolve(true)),
    fieldErrors: {},
    setFieldErrors: jest.fn(),
    getFormValues: jest.fn((rowId) => {
      const node = data.find((n) => n.id === rowId)
      return node ? { name: node.name, value: node.value } : { name: '', value: 0 }
    }),
    computeChanges: jest.fn((_rowId, formValues) => formValues),
    cellIdColumnMatchOrder: ['value', 'name'],
    ...overrides,
  }
}

describe('useGridEditing', () => {
  describe('initialization', () => {
    it('starts with no selection', () => {
      const config = createDefaultConfig()
      const { result } = renderHook(() => useGridEditing(config))

      expect(result.current.selectedRowId).toBeNull()
      expect(result.current.selectedCellId).toBeNull()
      expect(result.current.isSaving).toBe(false)
    })

    it('provides tableRef', () => {
      const config = createDefaultConfig()
      const { result } = renderHook(() => useGridEditing(config))

      expect(result.current.tableRef).toBeDefined()
      expect(result.current.tableRef.current).toBeNull()
    })
  })

  describe('editableColumns', () => {
    it('returns static columns when editableColumnIds is an array', () => {
      const config = createDefaultConfig({
        editableColumnIds: ['name', 'value'],
      })
      const { result } = renderHook(() => useGridEditing(config))

      expect(result.current.editableColumns).toEqual(['name', 'value'])
    })

    it('returns dynamic columns when editableColumnIds is a function', () => {
      const config = createDefaultConfig({
        editableColumnIds: (selectedRowId) =>
          selectedRowId?.startsWith('draft-')
            ? ['type', 'name', 'value']
            : ['name', 'value'],
      })
      const { result } = renderHook(() => useGridEditing(config))

      // No row selected — null selectedRowId
      expect(result.current.editableColumns).toEqual(['name', 'value'])
    })
  })

  describe('selection state', () => {
    it('setSelectedRowId updates selectedRowId', () => {
      const config = createDefaultConfig()
      const { result } = renderHook(() => useGridEditing(config))

      act(() => {
        result.current.setSelectedRowId('1')
      })

      expect(result.current.selectedRowId).toBe('1')
    })

    it('setSelectedCellId updates selectedCellId', () => {
      const config = createDefaultConfig()
      const { result } = renderHook(() => useGridEditing(config))

      act(() => {
        result.current.setSelectedRowId('1')
        result.current.setSelectedCellId('1-name')
      })

      expect(result.current.selectedCellId).toBe('1-name')
    })

    it('initializes form when row is selected', () => {
      const config = createDefaultConfig()
      const { result } = renderHook(() => useGridEditing(config))

      act(() => {
        result.current.setSelectedRowId('1')
      })

      expect(config.getFormValues).toHaveBeenCalledWith('1', config.data)
      expect(config.form.setFieldsValue).toHaveBeenCalled()
    })

    it('resets form when selection is cleared', () => {
      const config = createDefaultConfig()
      const { result } = renderHook(() => useGridEditing(config))

      act(() => {
        result.current.setSelectedRowId('1')
      })

      act(() => {
        result.current.setSelectedRowId(null)
      })

      // resetFields called on both select and deselect
      expect(config.form.resetFields).toHaveBeenCalled()
    })
  })

  describe('getFieldError', () => {
    it('returns undefined when no error exists', () => {
      const config = createDefaultConfig({ fieldErrors: {} })
      const { result } = renderHook(() => useGridEditing(config))

      expect(result.current.getFieldError('name')).toBeUndefined()
    })

    it('returns error string when error exists', () => {
      const config = createDefaultConfig({
        fieldErrors: { name: 'Name is required' },
      })
      const { result } = renderHook(() => useGridEditing(config))

      expect(result.current.getFieldError('name')).toBe('Name is required')
    })
  })

  describe('saveFormChanges', () => {
    it('calls computeChanges and onSave', async () => {
      const onSave = jest.fn(() => Promise.resolve(true))
      const computeChanges = jest.fn(() => ({ name: 'Updated' }))
      const config = createDefaultConfig({ onSave, computeChanges })
      const { result } = renderHook(() => useGridEditing(config))

      act(() => {
        result.current.setSelectedRowId('1')
      })

      let success: boolean
      await act(async () => {
        success = await result.current.saveFormChanges('1')
      })

      expect(computeChanges).toHaveBeenCalled()
      expect(onSave).toHaveBeenCalledWith('1', { name: 'Updated' })
      expect(success!).toBe(true)
    })

    it('skips save when computeChanges returns null', async () => {
      const onSave = jest.fn(() => Promise.resolve(true))
      const computeChanges = jest.fn(() => null)
      const config = createDefaultConfig({ onSave, computeChanges })
      const { result } = renderHook(() => useGridEditing(config))

      act(() => {
        result.current.setSelectedRowId('1')
      })

      let success: boolean
      await act(async () => {
        success = await result.current.saveFormChanges('1')
      })

      expect(onSave).not.toHaveBeenCalled()
      expect(success!).toBe(true) // No changes = success
    })

    it('returns false when validateFields returns errors', async () => {
      const onSave = jest.fn(() => Promise.resolve(true))
      const setFieldErrors = jest.fn()
      const validateFields = jest.fn(() => ({
        name: 'Name is required',
      }))
      const config = createDefaultConfig({
        onSave,
        setFieldErrors,
        validateFields,
      })
      const { result } = renderHook(() => useGridEditing(config))

      act(() => {
        result.current.setSelectedRowId('1')
      })

      let success: boolean
      await act(async () => {
        success = await result.current.saveFormChanges('1')
      })

      expect(onSave).not.toHaveBeenCalled()
      expect(setFieldErrors).toHaveBeenCalledWith({
        name: 'Name is required',
      })
      expect(success!).toBe(false)
    })

    it('returns false when onSave fails', async () => {
      const onSave = jest.fn(() => Promise.resolve(false))
      const config = createDefaultConfig({ onSave })
      const { result } = renderHook(() => useGridEditing(config))

      act(() => {
        result.current.setSelectedRowId('1')
      })

      let success: boolean
      await act(async () => {
        success = await result.current.saveFormChanges('1')
      })

      expect(success!).toBe(false)
    })

    it('returns false when form validation throws', async () => {
      const mockForm = createMockForm()
      mockForm.validateFields.mockRejectedValue(new Error('Validation failed'))
      const onSave = jest.fn()
      const config = createDefaultConfig({ form: mockForm as any, onSave })
      const { result } = renderHook(() => useGridEditing(config))

      act(() => {
        result.current.setSelectedRowId('1')
      })

      let success: boolean
      await act(async () => {
        success = await result.current.saveFormChanges('1')
      })

      expect(onSave).not.toHaveBeenCalled()
      expect(success!).toBe(false)
    })
  })

  describe('draft support', () => {
    it('identifies draft rows by prefix', () => {
      const config = createDefaultConfig({
        editableColumnIds: (rowId) =>
          rowId?.startsWith('draft-')
            ? ['type', 'name', 'value']
            : ['name', 'value'],
      })
      const { result } = renderHook(() => useGridEditing(config))

      // Select a draft row
      act(() => {
        result.current.setSelectedRowId('draft-1')
      })

      expect(result.current.editableColumns).toEqual([
        'type',
        'name',
        'value',
      ])
    })

    it('uses custom draftPrefix', () => {
      const config = createDefaultConfig({
        draftPrefix: 'new-',
        editableColumnIds: (rowId) =>
          rowId?.startsWith('new-')
            ? ['type', 'name']
            : ['name'],
      })
      const { result } = renderHook(() => useGridEditing(config))

      act(() => {
        result.current.setSelectedRowId('new-1')
      })

      expect(result.current.editableColumns).toEqual(['type', 'name'])
    })
  })

  describe('handleKeyDown – Tab navigation', () => {
    // Helper: set up the hook with a selected row and a mock tableRef
    const setupForKeyDown = (
      overrides: Partial<GridEditingConfig<TestNode>> = {},
    ) => {
      const onSave = jest.fn(() => Promise.resolve(true))
      const computeChanges = jest.fn(() => ({ name: 'Changed' }))
      const config = createDefaultConfig({
        onSave,
        computeChanges,
        ...overrides,
      })
      const hookResult = renderHook(() => useGridEditing(config))

      // Wire up a fake table model so row lookup works
      const rows = config.data.map((node) => ({ original: node }))
      act(() => {
        hookResult.result.current.tableRef.current = {
          getRowModel: () => ({ rows }),
        }
        hookResult.result.current.setSelectedRowId('1')
        hookResult.result.current.setSelectedCellId('1-name')
      })

      return { hookResult, onSave, computeChanges, config }
    }

    const createKeyEvent = (
      key: string,
      extra: Partial<React.KeyboardEvent> = {},
    ) =>
      ({
        key,
        preventDefault: jest.fn(),
        stopPropagation: jest.fn(),
        shiftKey: false,
        ...extra,
      }) as unknown as React.KeyboardEvent

    // Stub DOM cells so findNextColInCurrentRow finds them
    const stubCellElements = (
      rowId: string,
      columns: string[],
    ): HTMLElement[] => {
      const cells: HTMLElement[] = []
      for (const col of columns) {
        const cell = document.createElement('div')
        cell.setAttribute('data-cell-id', `${rowId}-${col}`)
        const input = document.createElement('input')
        cell.appendChild(input)
        document.body.appendChild(cell)
        cells.push(cell)
      }
      return cells
    }

    afterEach(() => {
      document.body.innerHTML = ''
    })

    it('Tab moves to next column within the same row without saving', async () => {
      const { hookResult, onSave } = setupForKeyDown()
      stubCellElements('1', ['name', 'value'])

      const event = createKeyEvent('Tab')

      await act(async () => {
        await hookResult.result.current.handleKeyDown(event, '1', 'name')
      })

      expect(event.preventDefault).toHaveBeenCalled()
      expect(event.stopPropagation).toHaveBeenCalled()
      // Same row → no save
      expect(onSave).not.toHaveBeenCalled()
      // Should move to 'value' column
      expect(hookResult.result.current.selectedCellId).toBe('1-value')
      expect(hookResult.result.current.selectedRowId).toBe('1')
    })

    it('Tab from last column saves and moves to next row', async () => {
      const { hookResult, onSave } = setupForKeyDown()
      stubCellElements('1', ['name', 'value'])

      const event = createKeyEvent('Tab')

      await act(async () => {
        await hookResult.result.current.handleKeyDown(event, '1', 'value')
      })

      // Moving to next row → must save
      expect(onSave).toHaveBeenCalledWith('1', { name: 'Changed' })
      expect(hookResult.result.current.selectedRowId).toBe('2')
      expect(hookResult.result.current.selectedCellId).toBe('2-name')
    })

    it('Tab always blurs activeElement (dropdown selection prevented at input level)', async () => {
      const { hookResult } = setupForKeyDown()
      stubCellElements('1', ['name', 'value'])

      const activeInput = document.createElement('input')
      document.body.appendChild(activeInput)
      activeInput.focus()
      const blurSpy = jest.spyOn(activeInput, 'blur')

      const event = createKeyEvent('Tab')

      await act(async () => {
        await hookResult.result.current.handleKeyDown(event, '1', 'name')
      })

      expect(blurSpy).toHaveBeenCalled()

      blurSpy.mockRestore()
    })

    it('Tab from last column with failed save stays on current row', async () => {
      const onSave = jest.fn(() => Promise.resolve(false))
      const { hookResult } = setupForKeyDown({ onSave })
      stubCellElements('1', ['name', 'value'])

      const event = createKeyEvent('Tab')

      await act(async () => {
        await hookResult.result.current.handleKeyDown(event, '1', 'value')
      })

      // Save failed → should stay on row 1
      expect(hookResult.result.current.selectedRowId).toBe('1')
    })

    it('Shift+Tab moves to previous column', async () => {
      const { hookResult, onSave } = setupForKeyDown()
      stubCellElements('1', ['name', 'value'])

      const event = createKeyEvent('Tab', { shiftKey: true } as any)

      await act(async () => {
        await hookResult.result.current.handleKeyDown(event, '1', 'value')
      })

      expect(onSave).not.toHaveBeenCalled()
      expect(hookResult.result.current.selectedCellId).toBe('1-name')
    })

    it('Tab with open dropdown does NOT toggle selection (prevents default)', async () => {
      const { hookResult } = setupForKeyDown()
      stubCellElements('1', ['name', 'value'])

      // Simulate open dropdown
      const dropdown = document.createElement('div')
      dropdown.classList.add('ant-select-dropdown')
      document.body.appendChild(dropdown)

      const event = createKeyEvent('Tab')

      await act(async () => {
        await hookResult.result.current.handleKeyDown(event, '1', 'name')
      })

      // preventDefault must be called to stop antd Select from
      // interpreting Tab as a selection action
      expect(event.preventDefault).toHaveBeenCalled()
      expect(event.stopPropagation).toHaveBeenCalled()
    })
  })

  describe('createSelectInputKeyDown – prevents rc-select Tab selection', () => {
    it('stops propagation for Tab key so rc-select does not select on Tab', async () => {
      const onSave = jest.fn(() => Promise.resolve(true))
      const computeChanges = jest.fn(() => ({ name: 'Changed' }))
      const config = createDefaultConfig({ onSave, computeChanges })
      const { result } = renderHook(() => useGridEditing(config))

      // Wire up tableRef so handleKeyDown can resolve rows
      const rows = config.data.map((node) => ({ original: node }))
      act(() => {
        result.current.tableRef.current = {
          getRowModel: () => ({ rows }),
        }
        result.current.setSelectedRowId('1')
        result.current.setSelectedCellId('1-name')
      })

      // Stub DOM cells
      const cells: HTMLElement[] = []
      for (const col of ['name', 'value']) {
        const cell = document.createElement('div')
        cell.setAttribute('data-cell-id', `1-${col}`)
        cell.appendChild(document.createElement('input'))
        document.body.appendChild(cell)
        cells.push(cell)
      }

      const event = {
        key: 'Tab',
        shiftKey: false,
        stopPropagation: jest.fn(),
        preventDefault: jest.fn(),
      } as unknown as React.KeyboardEvent<HTMLInputElement>

      const handler = result.current.createSelectInputKeyDown('1', 'name')
      await act(async () => {
        handler(event)
      })

      expect(event.stopPropagation).toHaveBeenCalled()
      // Should still navigate (handleKeyDown called internally)
      expect(result.current.selectedCellId).toBe('1-value')

      document.body.innerHTML = ''
    })

    it('does NOT stop propagation for non-Tab keys', () => {
      const config = createDefaultConfig()
      const { result } = renderHook(() => useGridEditing(config))

      const event = {
        key: 'Enter',
        stopPropagation: jest.fn(),
      } as unknown as React.KeyboardEvent<HTMLInputElement>

      const handler = result.current.createSelectInputKeyDown('1', 'name')
      act(() => {
        handler(event)
      })

      expect(event.stopPropagation).not.toHaveBeenCalled()
    })
  })

  describe('canEdit guard', () => {
    it('handleRowClick does nothing when canEdit is false', async () => {
      const config = createDefaultConfig({ canEdit: false })
      const { result } = renderHook(() => useGridEditing(config))

      const mockEvent = {
        target: document.createElement('td'),
      } as unknown as React.MouseEvent

      await act(async () => {
        await result.current.handleRowClick(mockEvent, {
          rowId: '1',
          isEditableColumn: () => true,
          getClickedColumnId: () => 'name',
        })
      })

      expect(result.current.selectedRowId).toBeNull()
    })
  })
})
