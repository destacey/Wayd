import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import {
  IntakeDeskDto,
  IntakeDeskWorkflowDto,
  StatusCategory,
} from '@/src/services/wayd-api'
import RequestBoardView, {
  boardDeskIds,
  boardTitle,
} from './request-board-view'
import { DEFAULT_REQUEST_FILTERS } from './request-filters'

const workflow = (id: string, name: string): IntakeDeskWorkflowDto =>
  ({
    workflow: { id, key: 1, name },
    followsOrganizationDefault: true,
    hasRequests: true,
    canChange: false,
    statuses: [
      {
        id: `${id}-submitted`,
        name: 'Submitted',
        category: StatusCategory.Proposed,
        alias: 1,
      },
    ],
  }) as IntakeDeskWorkflowDto

const desk = (id: string, name: string, isOpen = true) =>
  ({ id, key: 1, name, isOpen, members: [] }) as unknown as IntakeDeskDto

const DESKS = [desk('d-product', 'Product delivery'), desk('d-it', 'IT')]

let mockWorkflows: IntakeDeskWorkflowDto[] = []
const mockWorkflowsQuery = jest.fn()
jest.mock(
  '@/src/store/features/demand-management/demand-management-api',
  () => ({
    useGetIntakeDeskWorkflowsQuery: (deskIds: string[]) => {
      mockWorkflowsQuery(deskIds)
      return {
        currentData: mockWorkflows,
        isFetching: false,
        isError: false,
      }
    },
  }),
)

describe('boardTitle', () => {
  it('names the one desk the board is for', () => {
    // Act
    const title = boardTitle(
      { ...DEFAULT_REQUEST_FILTERS, deskIds: ['d-it'] },
      DESKS,
      'Intake review',
    )

    // Assert
    expect(title).toBe('The IT desk')
  })

  it('says the board spans every desk, and on which workflow', () => {
    // Act
    const title = boardTitle(DEFAULT_REQUEST_FILTERS, DESKS, 'Intake review')

    // Assert
    expect(title).toBe('All desks, which share one workflow: Intake review')
  })
})

describe('boardDeskIds', () => {
  it('covers the desks the requests in scope are on, so an idle desk does not block the board', () => {
    // Arrange
    const desks = [desk('d-product', 'Product'), desk('d-idle', 'Idle')]
    const requests = [{ desk: { id: 'd-product' } }] as any

    // Act
    const ids = boardDeskIds(DEFAULT_REQUEST_FILTERS, desks, requests)

    // Assert
    expect(ids).toEqual(['d-product'])
  })

  it('falls back to the desks open to new requests when nothing is in scope', () => {
    // Arrange
    const desks = [desk('d-open', 'Open'), desk('d-closed', 'Closed', false)]

    // Act
    const ids = boardDeskIds(DEFAULT_REQUEST_FILTERS, desks, [])

    // Assert
    expect(ids).toEqual(['d-open'])
  })
})

describe('RequestBoardView', () => {
  beforeEach(() => mockWorkflowsQuery.mockClear())

  it('draws one board when every desk in scope shares a workflow', () => {
    // Arrange
    mockWorkflows = [
      workflow('w-1', 'Intake review'),
      workflow('w-1', 'Intake review'),
    ]

    // Act
    render(
      <RequestBoardView
        requests={[]}
        filters={DEFAULT_REQUEST_FILTERS}
        desks={DESKS}
        onPickDesk={jest.fn()}
      />,
    )

    // Assert
    expect(mockWorkflowsQuery).toHaveBeenCalledWith(['d-it', 'd-product'])
    expect(
      screen.getByText('All desks, which share one workflow: Intake review'),
    ).toBeInTheDocument()
  })

  it('waits for the desks rather than saying there are none', () => {
    // Act
    render(
      <RequestBoardView
        requests={[]}
        filters={DEFAULT_REQUEST_FILTERS}
        desks={undefined}
        onPickDesk={jest.fn()}
      />,
    )

    // Assert
    expect(
      screen.queryByText('There are no desks to show a board for.'),
    ).not.toBeInTheDocument()
  })

  it('waits for the requests rather than drawing empty columns', () => {
    // Arrange
    mockWorkflows = [workflow('w-1', 'Intake review')]

    // Act
    render(
      <RequestBoardView
        requests={undefined}
        filters={DEFAULT_REQUEST_FILTERS}
        desks={DESKS}
        onPickDesk={jest.fn()}
      />,
    )

    // Assert
    expect(screen.queryByText(/share one workflow/)).not.toBeInTheDocument()
    expect(
      screen.queryByText('There are no desks to show a board for.'),
    ).not.toBeInTheDocument()
  })

  it('asks for a desk instead of guessing when the desks follow different workflows', async () => {
    // Arrange
    mockWorkflows = [
      workflow('w-1', 'Intake review'),
      workflow('w-2', 'IT demand'),
    ]
    const onPickDesk = jest.fn()
    render(
      <RequestBoardView
        requests={[]}
        filters={DEFAULT_REQUEST_FILTERS}
        desks={DESKS}
        onPickDesk={onPickDesk}
      />,
    )

    // Act
    await act(() => userEvent.click(screen.getByRole('button', { name: 'IT' })))

    // Assert
    expect(
      screen.getByText(
        'These desks review on different workflows, so they have no single board.',
      ),
    ).toBeInTheDocument()
    expect(onPickDesk).toHaveBeenCalledWith('d-it')
  })
})
