import React from 'react'
import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import {
  IntakeRequestDetailsDto,
  IntakeRequestOutcomeDto,
  IntakeRequestOutcomeKind,
  ProjectDeliverableStatusAlias,
  StatusCategory,
} from '@/src/services/wayd-api'
import IntakeRequestOutcomes from './intake-request-outcomes'

vi.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: vi.fn(), success: vi.fn() }),
}))

const mockRemove = vi.fn()
vi.mock('@/src/store/features/demand-management/demand-management-api', () => ({
  useRemoveIntakeRequestOutcomeMutation: () => [
    mockRemove,
    { isLoading: false },
  ],
  useSetIntakeRequestOutcomeDeliverablesMutation: () => [vi.fn()],
  useGetIntakeRequestProjectDeliverablesQuery: () => ({
    data: [],
    isFetching: false,
    isError: false,
  }),
}))

const agent = { id: 'e-9', key: 9, name: 'Morgan Agent' }

const request = (
  canChangeOutcomes: boolean,
  outcomes: IntakeRequestDetailsDto['outcomes'] = [
    {
      kind: IntakeRequestOutcomeKind.ExistingProjectScope,
      targetId: 'p-1',
      project: { id: 'p-1', key: 'ATLAS', name: 'Atlas' },
      note: 'The export screen',
      deliverables: [],
      addedBy: agent,
      addedOn: new Date('2026-10-01T09:00:00Z'),
    },
    {
      kind: IntakeRequestOutcomeKind.NewProject,
      targetId: 'p-gone',
      project: undefined,
      deliverables: [],
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
              statusCategory: StatusCategory.Active,
            },
            deliverables: [],
            addedBy: agent,
            addedOn: new Date('2026-10-01T09:00:00Z'),
          },
          {
            kind: IntakeRequestOutcomeKind.WorkItem,
            targetId: 'w-gone',
            workItem: undefined,
            deliverables: [],
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

  it('says when existing project scope maps no deliverables, and only for that kind', () => {
    // Arrange / Act
    render(<IntakeRequestOutcomes request={request(false)} />)

    // Assert
    expect(screen.getAllByText('No deliverables mapped')).toHaveLength(1)
  })

  const mapped = (
    deliverables: IntakeRequestOutcomeDto['deliverables'],
    isMet: boolean | undefined,
  ): IntakeRequestOutcomeDto => ({
    ...request(true).outcomes[0],
    deliverables,
    isMet,
  })

  const mappedRequest = (
    outcome: IntakeRequestOutcomeDto,
    deliverablesMet: boolean | undefined,
    category = StatusCategory.Active,
    canChangeOutcomes = true,
  ) =>
    ({
      ...request(canChangeOutcomes, [outcome]),
      summary: { id: 'r-42', key: 42, status: { category } },
      deliverablesMet,
    }) as unknown as IntakeRequestDetailsDto

  it('shows each mapped deliverable with its status, and one deleted from the project', () => {
    // Arrange / Act
    render(
      <IntakeRequestOutcomes
        request={mappedRequest(
          mapped(
            [
              {
                id: 'd-1',
                name: 'Reports',
                statusCategory: StatusCategory.Removed,
                statusAlias: ProjectDeliverableStatusAlias.Descoped,
                isOnProject: true,
              },
              { id: 'd-gone', isOnProject: false },
            ],
            false,
          ),
          false,
        )}
      />,
    )

    // Assert
    expect(screen.getByText('Reports')).toBeInTheDocument()
    expect(screen.getByText('Descoped').closest('.ant-tag')).toHaveClass(
      'ant-tag-warning',
    )
    expect(
      screen.getByText('A deliverable deleted from the project'),
    ).toBeInTheDocument()
    expect(screen.getByText('Waiting on deliverables')).toBeInTheDocument()
    expect(screen.queryByText(/ready to fulfil/)).toBeNull()
  })

  it('says an accepted request whose deliverables are all accepted may be ready to fulfil', () => {
    // Arrange
    const outcome = mapped(
      [
        {
          id: 'd-1',
          name: 'Reports',
          statusCategory: StatusCategory.Done,
          statusAlias: ProjectDeliverableStatusAlias.Accepted,
          isOnProject: true,
        },
      ],
      true,
    )

    // Act
    render(<IntakeRequestOutcomes request={mappedRequest(outcome, true)} />)

    // Assert
    expect(screen.getByText('Met')).toBeInTheDocument()
    expect(
      screen.getByText('This request may be ready to fulfil.'),
    ).toBeInTheDocument()
  })

  it('does not suggest fulfilling a request that is already closed', () => {
    // Arrange / Act
    render(
      <IntakeRequestOutcomes
        request={mappedRequest(
          mapped([], undefined),
          true,
          StatusCategory.Done,
          false,
        )}
      />,
    )

    // Assert
    expect(screen.queryByText(/ready to fulfil/)).toBeNull()
  })

  it('offers editing deliverables only on an existing project scope outcome, to someone who may change outcomes', () => {
    // Arrange / Act
    const { rerender } = render(
      <IntakeRequestOutcomes request={request(true)} />,
    )

    // Assert — the scope outcome takes deliverables; the new project outcome does not
    expect(
      screen.getAllByRole('button', { name: 'Edit Deliverables' }),
    ).toHaveLength(1)
    rerender(<IntakeRequestOutcomes request={request(false)} />)
    expect(
      screen.queryByRole('button', { name: 'Edit Deliverables' }),
    ).toBeNull()
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
    await userEvent.click(screen.getByRole('button', { name: 'Remove' }))

    // Act
    await userEvent.click(
      within(
        document.querySelector('.ant-popconfirm') as HTMLElement,
      ).getByRole('button', { name: 'Remove' }),
    )

    // Assert
    expect(mockRemove).toHaveBeenCalledWith({
      requestId: 'r-42',
      targetId: 'p-1',
    })
  })
})
