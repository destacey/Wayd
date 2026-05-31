import { render, screen } from '@testing-library/react'
import PlanningIntervalObjectiveWorkItemsCard from './planning-interval-objective-work-items-card'
import {
  useGetObjectiveWorkItemsQuery,
  useGetPlanningIntervalQuery,
} from '@/src/store/features/planning/planning-interval-api'
import { IterationState } from '@/src/components/types'

Object.defineProperty(window, 'matchMedia', {
  writable: true,
  value: jest.fn().mockImplementation((query) => ({
    matches: false,
    media: query,
    onchange: null,
    addListener: jest.fn(),
    removeListener: jest.fn(),
    addEventListener: jest.fn(),
    removeEventListener: jest.fn(),
    dispatchEvent: jest.fn(),
  })),
})

jest.mock('@/src/store/features/planning/planning-interval-api', () => ({
  useGetObjectiveWorkItemsQuery: jest.fn(),
  useGetPlanningIntervalQuery: jest.fn(),
}))

jest.mock('@/src/components/common/work', () => ({
  WorkItemsDashboardModal: function MockWorkItemsDashboardModal() {
    return <div>Work items dashboard modal</div>
  },
  WorkItemsListCard: function MockWorkItemsListCard({
    workItems,
  }: {
    workItems: unknown[]
  }) {
    return (
    <div>Work items: {workItems.length}</div>
    )
  },
}))

jest.mock('@/src/components/common', () => ({
  WorkProgress: function MockWorkProgress() {
    return <div>Work progress</div>
  },
}))

jest.mock(
  '../../../../_components/manage-planning-interval-objective-work-items-form',
  () =>
    function MockManagePlanningIntervalObjectiveWorkItemsForm() {
      return <div>Manage work items form</div>
    },
)

const mockObjectiveWorkItemsQuery =
  useGetObjectiveWorkItemsQuery as unknown as jest.Mock
const mockPlanningIntervalQuery =
  useGetPlanningIntervalQuery as unknown as jest.Mock

describe('PlanningIntervalObjectiveWorkItemsCard', () => {
  beforeEach(() => {
    jest.clearAllMocks()

    mockObjectiveWorkItemsQuery.mockReturnValue({
      data: {
        progressSummary: { proposed: 0, active: 0, done: 1, total: 1 },
        workItems: [{ id: 'work-item-1' }],
      },
      isLoading: false,
      refetch: jest.fn(),
    })
  })
  it('shows the work items dashboard button when the PI has started', () => {
    mockPlanningIntervalQuery.mockReturnValue({
      data: {
        state: { id: IterationState.Active, name: 'Active' },
      },
    })

    render(
      <PlanningIntervalObjectiveWorkItemsCard
        planningIntervalKey={42}
        objectiveKey={7}
        canLinkWorkItems={true}
      />,
    )

    expect(screen.getByTitle('Work items dashboard')).toBeInTheDocument()
  })

  it('hides the work items dashboard button before the PI start date', () => {
    mockPlanningIntervalQuery.mockReturnValue({
      data: {
        state: { id: IterationState.Future, name: 'Future' },
      },
    })

    render(
      <PlanningIntervalObjectiveWorkItemsCard
        planningIntervalKey={42}
        objectiveKey={7}
        canLinkWorkItems={true}
      />,
    )

    expect(screen.queryByTitle('Work items dashboard')).not.toBeInTheDocument()
  })
})
