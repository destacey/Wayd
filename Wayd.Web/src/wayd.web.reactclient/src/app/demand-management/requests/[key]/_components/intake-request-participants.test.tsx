import React from 'react'
import { act, render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import {
  IntakeRequestDetailsDto,
  IntakeRequestSubstanceDto,
} from '@/src/services/wayd-api'
import IntakeRequestParticipants from './intake-request-participants'

// jest.setup stubs dayjs down to formatting; the participant dates need the real module.
jest.unmock('dayjs')

jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: jest.fn(), success: jest.fn() }),
}))

const mockRemove = jest.fn()
jest.mock(
  '@/src/store/features/demand-management/demand-management-api',
  () => ({
    useRemoveIntakeRequestParticipantMutation: () => [
      mockRemove,
      { isLoading: false },
    ],
  }),
)

const substance = {
  submittedBy: { id: 'e-2', key: 2, name: 'Blake Submitter' },
  participants: [
    {
      employee: { id: 'e-3', key: 3, name: 'Casey Participant' },
      addedBy: { id: 'e-1', key: 1, name: 'Avery Requester' },
      addedOn: new Date('2026-10-01T09:00:00Z'),
    },
  ],
} as unknown as IntakeRequestSubstanceDto

const request = (canReview: boolean) =>
  ({
    summary: {
      id: 'r-42',
      key: 42,
      requester: { id: 'e-1', key: 1, name: 'Avery Requester' },
    },
    substance,
    canReview,
  }) as unknown as IntakeRequestDetailsDto

describe('IntakeRequestParticipants', () => {
  beforeEach(() => {
    mockRemove.mockReset()
  })

  it('lists the requester, a different submitter and the participants', () => {
    // Arrange / Act
    render(
      <IntakeRequestParticipants
        request={request(false)}
        substance={substance}
      />,
    )

    // Assert
    expect(screen.getByText('Requester')).toBeInTheDocument()
    expect(screen.getByText('Submitter')).toBeInTheDocument()
    expect(screen.getByText('Casey Participant')).toBeInTheDocument()
    expect(
      screen.getByText(/can read this request and reply to it/),
    ).toBeInTheDocument()
  })

  it('removes a participant after the reviewer confirms', async () => {
    // Arrange
    mockRemove.mockReturnValue({ unwrap: () => Promise.resolve(null) })
    render(
      <IntakeRequestParticipants
        request={request(true)}
        substance={substance}
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
      employeeId: 'e-3',
    })
  })
})
