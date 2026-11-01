import React from 'react'
import { act, render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import {
  IntakeRequestDetailsDto,
  IntakeRequestOutcomeKind,
  WorkStatusCategory,
} from '@/src/services/wayd-api'
import IntakeRequestOutcomes from './intake-request-outcomes'

// jest.setup stubs dayjs down to formatting; the outcome dates need the real module.
jest.unmock('dayjs')

jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: jest.fn(), success: jest.fn() }),
}))

const mockRemove = jest.fn()
jest.mock(
  '@/src/store/features/demand-management/demand-management-api',
  () => ({
    useRemoveIntakeRequestOutcomeMutation: () => [
      mockRemove,
      { isLoading: false },
    ],
  }),
)

const agent = { id: 'e-9', key: 9, name: 'Morgan Agent' }

const request = (
  canChangeOutcomes: boolean,
  outcomes: IntakeRequestDetailsDto['outcomes'] = [
    {
      kind: IntakeRequestOutcomeKind.ExistingProjectScope,
      targetId: 'p-1',
      project: { id: 'p-1', key: 'ATLAS', name: 'Atlas' },
      note: 'The export screen',
      addedBy: agent,
      addedOn: new Date('2026-10-01T09:00:00Z'),
    },
    {
      kind: IntakeRequestOutcomeKind.NewProject,
      targetId: 'p-gone',
      project: undefined,
      addedBy: agent,
      addedOn: new Date('2026-10-02T09:00:00Z'),
    },
  ],
) =>
  ({
    summary: { id: 'r-42', key: 42 },
    outcomes,
    canChangeOutcomes,
  }) as unknown as IntakeRequestDetailsDto

describe('IntakeRequestOutcomes', () => {
  beforeEach(() => {
    mockRemove.mockReset()
  })

  it('links each project and says when one has been deleted', () => {
    // Arrange / Act
    render(<IntakeRequestOutcomes request={request(false)} />)

    // Assert
    expect(screen.getByRole('link', { name: 'ATLAS · Atlas' })).toHaveAttribute(
      'href',
      '/ppm/projects/ATLAS',
    )
    expect(screen.getByText('Existing project scope')).toBeInTheDocument()
    expect(screen.getByText('The export screen')).toBeInTheDocument()
    expect(
      screen.getByText('A project that has been deleted'),
    ).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Remove' })).toBeNull()
  })

  it('links each work item with its status and says when one has been deleted', () => {
    // Arrange / Act
    render(
      <IntakeRequestOutcomes
        request={request(false, [
          {
            kind: IntakeRequestOutcomeKind.WorkItem,
            targetId: 'w-1',
            workItem: {
              id: 'w-1',
              key: 'APP-42',
              title: 'Export screen',
              workspaceKey: 'APP',
              status: 'In Progress',
              statusCategory: WorkStatusCategory.Active,
            },
            addedBy: agent,
            addedOn: new Date('2026-10-01T09:00:00Z'),
          },
          {
            kind: IntakeRequestOutcomeKind.WorkItem,
            targetId: 'w-gone',
            workItem: undefined,
            addedBy: agent,
            addedOn: new Date('2026-10-02T09:00:00Z'),
          },
        ])}
      />,
    )

    // Assert
    expect(
      screen.getByRole('link', { name: 'APP-42 · Export screen' }),
    ).toHaveAttribute('href', '/work/workspaces/APP/work-items/APP-42')
    // Active is drawn as in progress; a wrong category mapping would draw it as done or proposed.
    expect(screen.getByText('In Progress').closest('.ant-tag')).toHaveClass(
      'ant-tag-processing',
    )
    expect(screen.getAllByText('Work item')).toHaveLength(2)
    expect(
      screen.getByText('A work item that has been deleted'),
    ).toBeInTheDocument()
    expect(screen.queryByText('A project that has been deleted')).toBeNull()
  })

  it('says there is no outcome yet', () => {
    // Arrange / Act
    render(<IntakeRequestOutcomes request={request(true, [])} />)

    // Assert
    expect(screen.getByText(/No outcome yet/)).toBeInTheDocument()
  })

  it('removes an outcome after the agent confirms', async () => {
    // Arrange
    mockRemove.mockReturnValue({ unwrap: () => Promise.resolve(null) })
    render(
      <IntakeRequestOutcomes
        request={request(true, [request(true).outcomes[0]])}
      />,
    )
    await act(() =>
      userEvent.click(screen.getByRole('button', { name: 'Remove' })),
    )

    // Act
    await act(() =>
      userEvent.click(
        within(
          document.querySelector('.ant-popconfirm') as HTMLElement,
        ).getByRole('button', { name: 'Remove' }),
      ),
    )

    // Assert
    expect(mockRemove).toHaveBeenCalledWith({
      requestId: 'r-42',
      targetId: 'p-1',
    })
  })
})
