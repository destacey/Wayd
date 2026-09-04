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


describe('project-plan-table overdue dates', () => {
  const baseArgs: any = {
    canManageTasks: false,
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
    isStageNode: () => false,
  }

  const daysFromToday = (days: number) => {
    const date = new Date()
    date.setDate(date.getDate() + days)
    return date
  }

  const renderPlannedEnd = (task: any) => {
    const columns = getProjectPlanTableColumns(baseArgs)
    const column: any = columns.find((c: any) => c.id === 'plannedEnd')
    return column.cell({
      row: { original: task },
      getValue: () => 'the date',
    })
  }

  const renderPlannedStart = (task: any) => {
    const columns = getProjectPlanTableColumns(baseArgs)
    const column: any = columns.find((c: any) => c.id === 'plannedStart')
    return column.cell({
      row: { original: task },
      getValue: () => 'the date',
    })
  }

  it('colors a past planned end date on an incomplete task', () => {
    // Arrange
    const task = {
      id: 'task-1',
      status: { name: 'In Progress' },
      end: daysFromToday(-1),
    }

    // Act
    const { container } = render(<>{renderPlannedEnd(task)}</>)

    // Assert
    expect(container.querySelector('span')).toHaveStyle({
      color: 'var(--ant-color-error)',
    })
  })

  it('leaves a past planned end date uncolored on a completed task', () => {
    // Arrange
    const task = {
      id: 'task-1',
      status: { name: 'Completed' },
      end: daysFromToday(-1),
    }

    // Act
    const { container } = render(<>{renderPlannedEnd(task)}</>)

    // Assert
    expect(container.querySelector('span')).toBeNull()
    expect(screen.getByText('the date')).toBeInTheDocument()
  })

  it('leaves a future planned end date uncolored', () => {
    // Arrange
    const task = {
      id: 'task-1',
      status: { name: 'In Progress' },
      end: daysFromToday(5),
    }

    // Act
    const { container } = render(<>{renderPlannedEnd(task)}</>)

    // Assert
    expect(container.querySelector('span')).toBeNull()
  })

  it('colors a past milestone date in the planned start column', () => {
    // Arrange
    const task = {
      id: 'milestone-1',
      status: { name: 'Not Started' },
      type: { name: 'Milestone' },
      plannedDate: daysFromToday(-1),
    }

    // Act
    const { container } = render(<>{renderPlannedStart(task)}</>)

    // Assert
    expect(container.querySelector('span')).toHaveStyle({
      color: 'var(--ant-color-error)',
    })
  })

  it('leaves a past planned start date uncolored on a non-milestone task', () => {
    // Arrange
    const task = {
      id: 'task-1',
      status: { name: 'In Progress' },
      start: daysFromToday(-10),
      end: daysFromToday(5),
    }

    // Act
    const { container } = render(<>{renderPlannedStart(task)}</>)

    // Assert
    expect(container.querySelector('span')).toBeNull()
  })
})

describe('project-plan-table schedule column', () => {
  const baseArgs: any = {
    canManageTasks: false,
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
    isStageNode: () => false,
  }

  const daysFromToday = (days: number) => {
    const date = new Date()
    date.setDate(date.getDate() + days)
    return date
  }

  const scheduleColumn = () => {
    const columns = getProjectPlanTableColumns(baseArgs)
    return columns.find((c: any) => c.id === 'schedule') as any
  }

  const renderSchedule = (task: any) => {
    const column = scheduleColumn()
    const value = column.accessorFn(task)
    return column.cell({ row: { original: task }, getValue: () => value })
  }

  it('sits between Priority and Planned Start', () => {
    // Arrange
    const columns = getProjectPlanTableColumns(baseArgs)

    // Act
    const ids = columns.map((c: any) => c.id ?? c.accessorKey)

    // Assert
    expect(ids.indexOf('schedule')).toBe(ids.indexOf('priority') + 1)
    expect(ids.indexOf('plannedStart')).toBe(ids.indexOf('schedule') + 1)
  })

  it('renders an Overdue tag for a past due task', () => {
    // Arrange
    const task = {
      id: 'task-1',
      status: { name: 'In Progress' },
      end: daysFromToday(-3),
    }

    // Act
    render(<>{renderSchedule(task)}</>)

    // Assert
    expect(screen.getByText('Overdue')).toBeInTheDocument()
  })

  it('renders nothing for a completed task', () => {
    // Arrange
    const task = {
      id: 'task-1',
      status: { name: 'Completed' },
      end: daysFromToday(-3),
    }

    // Act
    const { container } = render(<>{renderSchedule(task)}</>)

    // Assert
    expect(container).toBeEmptyDOMElement()
  })

  it('renders nothing for an undated task', () => {
    // Arrange
    const task = { id: 'task-1', status: { name: 'In Progress' } }

    // Act
    const { container } = render(<>{renderSchedule(task)}</>)

    // Assert
    expect(container).toBeEmptyDOMElement()
  })

  it('renders nothing for a task due far in the future', () => {
    // Arrange
    const task = {
      id: 'task-1',
      status: { name: 'In Progress' },
      end: daysFromToday(60),
    }

    // Act
    const { container } = render(<>{renderSchedule(task)}</>)

    // Assert
    expect(container).toBeEmptyDOMElement()
  })

  it('offers the three buckets as filter options', () => {
    // Arrange
    const column = scheduleColumn()

    // Act
    const values = column.meta.filterOptions.map((o: any) => o.value)

    // Assert
    expect(values).toEqual(['Overdue', 'Due This Week', 'Upcoming'])
  })
})
