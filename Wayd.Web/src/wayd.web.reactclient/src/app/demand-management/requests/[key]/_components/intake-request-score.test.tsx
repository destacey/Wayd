import { render, screen } from '@testing-library/react'
import IntakeRequestScore, { deskRankText } from './intake-request-score'

// jest.setup stubs dayjs down to formatting; the score dates need the real module.
jest.unmock('dayjs')

let mockScoring: any
jest.mock(
  '@/src/store/features/demand-management/demand-management-api',
  () => ({
    useGetIntakeRequestScoringQuery: () => ({
      data: mockScoring,
      isLoading: false,
      isError: false,
    }),
  }),
)

const model = { id: 'm-1', key: 1, name: 'WSJF' }
const score = (scoringModel = { id: 'm-1', key: 1, name: 'WSJF' }) => ({
  id: 's-1',
  sequence: 1,
  primaryValue: 2,
  scoredOn: new Date('2026-10-01T09:00:00Z'),
  scoredBy: { id: 'e-1', key: 1, name: 'Casey Agent' },
  scoringModel,
  ratings: [],
  outputs: [],
})

describe('deskRankText', () => {
  it.each([
    [1, '1st'],
    [2, '2nd'],
    [3, '3rd'],
    [4, '4th'],
    [11, '11th'],
    [12, '12th'],
    [13, '13th'],
    [21, '21st'],
    [112, '112th'],
  ])('reads position %i as %s', (position, ordinal) => {
    // Act
    const text = deskRankText({ position, of: 200 })

    // Assert
    expect(text).toBe(`${ordinal} of 200 open requests on this desk`)
  })

  it('says request for a desk of one', () => {
    // Act
    const text = deskRankText({ position: 1, of: 1 })

    // Assert
    expect(text).toBe('1st of 1 open request on this desk')
  })
})

describe('IntakeRequestScore', () => {
  it('warns when the desk scores with an archived model', () => {
    // Arrange
    mockScoring = {
      scoringModel: model,
      scoringModelArchived: true,
      canScore: true,
      scores: [],
    }

    // Act
    render(<IntakeRequestScore requestId="r-1" isOpen />)

    // Assert
    expect(
      screen.getByText("This desk's scoring model has been archived."),
    ).toBeInTheDocument()
  })

  it('says scoring is frozen on a closed request', () => {
    // Arrange
    mockScoring = {
      scoringModel: model,
      scoringModelArchived: false,
      canScore: false,
      scores: [score()],
    }

    // Act
    render(<IntakeRequestScore requestId="r-1" isOpen={false} />)

    // Assert
    expect(
      screen.getByText('Scoring is frozen while the request is closed.'),
    ).toBeInTheDocument()
  })

  it('says a score from a model the desk no longer uses does not rank', () => {
    // Arrange
    mockScoring = {
      scoringModel: model,
      scoringModelArchived: false,
      canScore: true,
      scores: [score({ id: 'm-old', key: 2, name: 'Old Model' })],
    }

    // Act
    render(<IntakeRequestScore requestId="r-1" isOpen />)

    // Assert
    expect(
      screen.getByText(
        "Scored with Old Model, which this desk no longer uses, so it does not rank with the desk's other requests.",
      ),
    ).toBeInTheDocument()
  })
})
