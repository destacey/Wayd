import React from 'react'
import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { App } from 'antd'
import type { ItemType } from 'antd/es/menu/interface'
import { WorkItemListDto } from '@/src/services/wayd-api'
import ProjectWorkItemsViewManager from './project-work-items-view-manager'

const mockSuccess = vi.fn()
const mockError = vi.fn()
vi.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: mockError, success: mockSuccess }),
}))

const mockRemove = vi.fn()
vi.mock('@/src/store/features/ppm/projects-api', () => ({
  useRemoveProjectWorkItemsMutation: () => [mockRemove],
}))

type GridProps = {
  getRowActions?: (workItem: WorkItemListDto) => ItemType[]
}

// The grids themselves are covered elsewhere; these tests read what the manager hands them.
let mockGridProps: GridProps = {}
// A declaration, so it is hoisted with the vi.mock calls that use it.
function mockGrid(name: string) {
  return {
    default: (props: GridProps) => {
      mockGridProps = props
      return <div data-testid={name} />
    },
  }
}
vi.mock('./project-work-items-tree-grid', () => mockGrid('tree-grid'))
vi.mock('@/src/components/common/work/work-items-grid', () =>
  mockGrid('list-grid'),
)

const workItem = (id: string, parentId?: string) =>
  ({
    id,
    key: id.toUpperCase(),
    parent: parentId ? { id: parentId } : undefined,
  }) as unknown as WorkItemListDto

const epic = workItem('epic')
const featureUnderEpic = workItem('feature', 'epic')
const featureUnderOtherParent = workItem('other-feature', 'elsewhere')
const workItems = [epic, featureUnderEpic, featureUnderOtherParent]

const renderManager = (
  canManageWorkItems: boolean,
  defaultView: 'Tree' | 'List' = 'Tree',
) =>
  render(
    <App>
      <ProjectWorkItemsViewManager
        workItems={workItems}
        isLoading={false}
        refetch={vi.fn()}
        projectId="p-1"
        canManageWorkItems={canManageWorkItems}
        defaultView={defaultView}
      />
    </App>,
  )

const removeEpic = async () => {
  const [remove] = mockGridProps.getRowActions!(epic) as unknown as {
    onClick: () => void
  }[]
  await act(async () => remove.onClick())
  await userEvent.click(screen.getByRole('button', { name: 'Remove' }))
}

describe('ProjectWorkItemsViewManager', () => {
  beforeEach(() => {
    mockGridProps = {}
    mockRemove.mockReset()
    mockSuccess.mockReset()
    mockError.mockReset()
  })

  it.each(['Tree', 'List'] as const)(
    'offers removing work items in the %s view to a user who can manage them',
    (view) => {
      // Act
      renderManager(true, view)

      // Assert
      expect(mockGridProps.getRowActions).toBeDefined()
    },
  )

  it('offers no row actions to a user who cannot manage work items', () => {
    // Act
    renderManager(false)

    // Assert
    expect(mockGridProps.getRowActions).toBeUndefined()
  })

  it('offers removal only on work items linked directly to the project', () => {
    // Arrange
    renderManager(true)
    const actionsFor = (w: WorkItemListDto) =>
      mockGridProps.getRowActions!(w).map((item) => item?.key)

    // Act
    const epicActions = actionsFor(epic)
    const inheritedActions = actionsFor(featureUnderEpic)
    const otherParentActions = actionsFor(featureUnderOtherParent)

    // Assert
    expect(epicActions).toEqual(['remove'])
    expect(inheritedActions).toEqual([])
    expect(otherParentActions).toEqual(['remove'])
  })

  it('removes the work item from the project once confirmed', async () => {
    // Arrange
    mockRemove.mockReturnValue({ unwrap: () => Promise.resolve() })
    renderManager(true)

    // Act
    await removeEpic()

    // Assert
    expect(mockRemove).toHaveBeenCalledWith({
      projectId: 'p-1',
      workItemIds: ['epic'],
    })
    expect(mockSuccess).toHaveBeenCalledWith('EPIC removed from the project.')
  })

  it('shows the API error when the removal is refused', async () => {
    // Arrange
    mockRemove.mockReturnValue({
      unwrap: () => Promise.reject({ status: 400, detail: 'Not linked.' }),
    })
    renderManager(true)

    // Act
    await removeEpic()

    // Assert
    expect(mockError).toHaveBeenCalledWith('Not linked.')
  })
})
