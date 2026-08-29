import React from 'react'
import { fireEvent, render, screen } from '@testing-library/react'
import { getProjectPlanTableColumns } from './project-plan-table.columns'

describe('project-plan-table key column', () => {
  const baseArgs: any = {
    canManageTasks: true,
    selectedRowId: null,
    handleEditTask: jest.fn(),
    handleDeleteTask: jest.fn(),
    handleUpdateTask: jest.fn(),
    getFieldError: jest.fn(),
    handleKeyDown: jest.fn(),
    createSelectInputKeyDown: jest.fn(),
    taskStatusOptions: [],
    taskStatusOptionsForMilestone: [],
    taskPriorityOptions: [],
    taskTypeOptions: [],
    employeeOptions: [],
    isDragEnabled: false,
    enableDragAndDrop: false,
    addDraftTaskAsChild: jest.fn(),
    canCreateTasks: true,
    isSelectedRowMilestone: false,
    taskTypeFilterOptions: [],
    taskStatusFilterOptions: [],
    taskPriorityFilterOptions: [],
    isStageNode: () => false,
    handleEditStage: jest.fn(),
  }

  beforeEach(() => {
    jest.clearAllMocks()
  })

  it('clicking key opens drawer for non-draft rows', () => {
    const openPlanItemDrawer = jest.fn()
    const columns = getProjectPlanTableColumns({
      ...baseArgs,
      openPlanItemDrawer,
    })
    const keyCol: any = columns.find((c: any) => c.accessorKey === 'key')

    const element = keyCol.cell({
      row: { original: { id: 'task-1' } },
      getValue: () => 'TASK-1',
    })

    render(<>{element}</>)
    fireEvent.click(screen.getByRole('button', { name: 'TASK-1' }))

    expect(openPlanItemDrawer).toHaveBeenCalledWith('task-1')
  })

  it('draft rows render non-clickable key text', () => {
    const openPlanItemDrawer = jest.fn()
    const columns = getProjectPlanTableColumns({
      ...baseArgs,
      openPlanItemDrawer,
    })
    const keyCol: any = columns.find((c: any) => c.accessorKey === 'key')

    const element = keyCol.cell({
      row: { original: { id: 'draft-1' } },
      getValue: () => 'DRAFT-1',
    })

    render(<>{element}</>)

    expect(screen.getByText('DRAFT-1')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'DRAFT-1' })).not.toBeInTheDocument()
    expect(openPlanItemDrawer).not.toHaveBeenCalled()
  })
})

