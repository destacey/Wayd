import { render, screen } from '@testing-library/react'
import {
  IntakeWorkflowStatusDto,
  StatusCategory,
} from '@/src/services/wayd-api'
import WhatHappensNext, { journeyOf } from './what-happens-next'

const status = (
  name: string,
  category: StatusCategory,
  description?: string,
  alias = 0,
): IntakeWorkflowStatusDto => ({
  id: `s-${name}`,
  name,
  category,
  categoryLabel: category,
  alias,
  description,
})

const STATUSES = [
  status('Submitted', StatusCategory.Proposed, 'Waiting for the desk.'),
  status('Board Review', StatusCategory.Proposed),
  status('Questions for you', StatusCategory.Proposed, 'Waiting on you.', 2),
  status('Accepted', StatusCategory.Active, 'Accepted for delivery.'),
  status('Fulfilled', StatusCategory.Done, 'Delivered.'),
  status('Declined', StatusCategory.Removed, 'Not going ahead.'),
]

let mockWorkflow: { statuses: IntakeWorkflowStatusDto[] } | undefined
jest.mock(
  '@/src/store/features/demand-management/demand-management-api',
  () => ({
    useGetIntakeDeskWorkflowQuery: () => ({
      data: mockWorkflow,
      isLoading: false,
    }),
  }),
)

describe('journeyOf', () => {
  it('keeps the stages on the way to done, in order, setting the detour and closings aside by meaning', () => {
    // Act
    const journey = journeyOf(STATUSES)

    // Assert
    expect(journey.stages.map((s) => s.name)).toEqual([
      'Submitted',
      'Board Review',
      'Accepted',
      'Fulfilled',
    ])
    expect(journey.canAskRequester).toBe(true)
    expect(journey.canClose).toBe(true)
  })
})

describe('WhatHappensNext', () => {
  it('describes the desk workflow in its own words, leaving out the closing stages', () => {
    // Arrange
    mockWorkflow = { statuses: STATUSES }

    // Act
    render(<WhatHappensNext deskId="d-1" />)

    // Assert
    expect(screen.getByText('Board Review')).toBeInTheDocument()
    expect(screen.getByText('Accepted for delivery.')).toBeInTheDocument()
    expect(screen.queryByText('Not going ahead.')).not.toBeInTheDocument()
    expect(screen.queryByText('Questions for you')).not.toBeInTheDocument()
    expect(
      screen.getByText('It can also be declined, with a reason you will see.'),
    ).toBeInTheDocument()
  })

  it('shows nothing until the workflow has loaded', () => {
    // Arrange
    mockWorkflow = undefined

    // Act
    render(<WhatHappensNext deskId="d-1" />)

    // Assert
    expect(screen.queryByText('What happens next')).not.toBeInTheDocument()
  })
})
