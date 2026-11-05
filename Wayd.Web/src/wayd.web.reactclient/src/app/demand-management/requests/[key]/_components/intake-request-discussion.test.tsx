import React from 'react'
import { render } from '@testing-library/react'
import {
  IntakeDeskVisibility,
  IntakeRequestDetailsDto,
  IntakeRequestSubstanceDto,
} from '@/src/services/wayd-api'
import type { MentionCandidate } from '@/src/components/common/discussions'
import IntakeRequestDiscussion from './intake-request-discussion'

interface CapturedProps {
  mentionCandidates: MentionCandidate[]
  noteMentionCandidates: MentionCandidate[]
}
let captured: CapturedProps
jest.mock('@/src/components/common/discussions', () => ({
  RecordDiscussion: (props: CapturedProps) => {
    captured = props
    return null
  },
}))

const mockUseSettings = jest.fn()
jest.mock('@/src/store/features/common/discussions-api', () => ({
  DiscussionChannelTypes: { IntakeRequest: 'demand.intake-request' },
  useGetDiscussionSettingsQuery: (...args: unknown[]) =>
    mockUseSettings(...args),
}))

const mockUseDesk = jest.fn()
const mockUseEveryone = jest.fn()
jest.mock(
  '@/src/store/features/demand-management/demand-management-api',
  () => ({
    useGetIntakeDeskQuery: (...args: unknown[]) => mockUseDesk(...args),
    useGetIntakeEmployeeOptionsQuery: (...args: unknown[]) =>
      mockUseEveryone(...args),
  }),
)

const agent = { id: 'e-9', key: 9, name: 'Marcus Lee' }
const requester = { id: 'e-1', key: 1, name: 'Priya Natarajan' }

const substance = {
  submittedBy: requester,
  participants: [
    {
      employee: { id: 'e-3', key: 3, name: 'Tom Becker' },
      addedOn: new Date(),
    },
  ],
} as unknown as IntakeRequestSubstanceDto

const request = (overrides: Partial<IntakeRequestDetailsDto> = {}) =>
  ({
    summary: { id: 'r-1', key: 1, requester, desk: { id: 'd-1', key: 1 } },
    substance,
    canAddParticipants: false,
    ...overrides,
  }) as unknown as IntakeRequestDetailsDto

describe('IntakeRequestDiscussion', () => {
  beforeEach(() => {
    jest.clearAllMocks()
    mockUseDesk.mockReturnValue({
      data: {
        visibility: IntakeDeskVisibility.Private,
        members: [{ employee: agent }],
      },
    })
    mockUseEveryone.mockReturnValue({ data: undefined })
    mockUseSettings.mockReturnValue({ data: { mentionsGrantAccess: true } })
  })

  it('offers the request and its desk for mentions on a private desk, and only the desk in a note', () => {
    // Act
    render(
      <IntakeRequestDiscussion request={request()} substance={substance} />,
    )

    // Assert
    expect(captured.mentionCandidates.map((c) => c.name)).toEqual([
      'Priya Natarajan',
      'Tom Becker',
      'Marcus Lee',
    ])
    expect(captured.noteMentionCandidates.map((c) => c.name)).toEqual([
      'Marcus Lee',
    ])
    expect(mockUseEveryone).toHaveBeenCalledWith(undefined, { skip: true })
  })

  it('offers anyone for mentions where the author could add them on an open desk', () => {
    // Arrange
    mockUseDesk.mockReturnValue({
      data: { visibility: IntakeDeskVisibility.Open, members: [] },
    })
    mockUseEveryone.mockReturnValue({
      data: [{ id: 'e-7', key: 7, name: 'Dana Ortiz' }],
    })

    // Act
    render(
      <IntakeRequestDiscussion
        request={request({ canAddParticipants: true })}
        substance={substance}
      />,
    )

    // Assert
    expect(captured.mentionCandidates.map((c) => c.name)).toEqual([
      'Dana Ortiz',
    ])
  })

  it("offers only the request's people when the desk's mentions give no access", () => {
    // Arrange
    mockUseDesk.mockReturnValue({
      data: { visibility: IntakeDeskVisibility.Open, members: [] },
    })
    mockUseSettings.mockReturnValue({ data: { mentionsGrantAccess: false } })

    // Act
    render(
      <IntakeRequestDiscussion
        request={request({ canAddParticipants: true })}
        substance={substance}
      />,
    )

    // Assert
    expect(mockUseSettings).toHaveBeenCalledWith({
      channelType: 'demand.intake-request',
      scopeId: 'd-1',
    })
    expect(captured.mentionCandidates.map((c) => c.name)).toEqual([
      'Priya Natarajan',
      'Tom Becker',
    ])
    expect(mockUseEveryone).toHaveBeenCalledWith(undefined, { skip: true })
  })

  it("offers the request's people with their numbers, to tell names apart", () => {
    // Act
    render(
      <IntakeRequestDiscussion request={request()} substance={substance} />,
    )

    // Assert
    expect(captured.mentionCandidates[0]).toEqual({
      id: 'e-1',
      name: 'Priya Natarajan',
      key: 1,
    })
  })
})
