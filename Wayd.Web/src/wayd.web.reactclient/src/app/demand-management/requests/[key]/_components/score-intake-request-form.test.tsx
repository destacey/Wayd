import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import ScoreIntakeRequestForm from './score-intake-request-form'

jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: jest.fn(), success: jest.fn() }),
}))

const mockScore = jest.fn()
const mockPreview = jest.fn()
jest.mock(
  '@/src/store/features/demand-management/demand-management-api',
  () => ({
    useScoreIntakeRequestMutation: () => [mockScore, { isLoading: false }],
    usePreviewIntakeRequestScoreMutation: () => [
      mockPreview,
      { isLoading: false },
    ],
  }),
)

const model = {
  id: 'm-1',
  key: 1,
  name: 'WSJF',
  description: '',
  state: { id: 2, name: 'Active' },
  criteria: [
    {
      id: 'c-bv',
      name: 'Business Value',
      token: 'BV',
      order: 1,
      scaleId: 'sc-1',
    },
    { id: 'c-js', name: 'Job Size', token: 'JS', order: 2 },
  ],
  scales: [
    {
      id: 'sc-1',
      name: 'Impact',
      order: 1,
      levels: [
        { id: 'l-high', label: 'High', value: 8, order: 1 },
        { id: 'l-low', label: 'Low', value: 1, order: 2 },
      ],
    },
  ],
  outputs: [
    {
      token: 'WSJF',
      name: 'WSJF',
      formula: 'BV / JS',
      isPrimary: true,
      order: 1,
    },
  ],
} as any

const renderForm = async (currentScore?: any) =>
  await act(async () =>
    render(
      <ScoreIntakeRequestForm
        requestId="r-1"
        scoringModel={model}
        modelArchived={false}
        needBy={undefined}
        currentScore={currentScore}
        onFormComplete={jest.fn()}
        onFormCancel={jest.fn()}
      />,
    ),
  )

describe('ScoreIntakeRequestForm', () => {
  beforeEach(() => {
    mockScore.mockReset()
    mockScore.mockReturnValue({ unwrap: () => Promise.resolve('s-1') })
    mockPreview.mockReset()
  })

  it('waits for every criterion to be rated before it can save', async () => {
    // Act
    await renderForm()

    // Assert
    expect(screen.getByRole('button', { name: 'Save Score' })).toBeDisabled()
  })

  it('starts from the current score and sends a level for a scale and a number otherwise', async () => {
    // Arrange
    await renderForm({
      ratings: [
        { criterionId: 'c-bv', ratingValue: 8, ratingLevelId: 'l-high' },
        { criterionId: 'c-js', ratingValue: 2 },
      ],
    })

    // Act
    await act(() =>
      userEvent.click(screen.getByRole('button', { name: 'Save Score' })),
    )

    // Assert
    expect(mockScore).toHaveBeenCalledWith({
      requestId: 'r-1',
      ratings: [
        { criterionId: 'c-bv', value: undefined, ratingLevelId: 'l-high' },
        { criterionId: 'c-js', value: 2, ratingLevelId: undefined },
      ],
    })
  })

  it('says when the requester gave no need-by date', async () => {
    // Act
    await renderForm()

    // Assert
    expect(
      screen.getByText('The requester gave no need-by date.'),
    ).toBeInTheDocument()
  })

  it('marks the criterion a 422 names by its position in the ratings', async () => {
    // Arrange
    mockScore.mockReturnValue({
      unwrap: () =>
        Promise.reject({
          status: 422,
          errors: { 'ratings[1].Value': ['Enter a number.'] },
        }),
    })
    await renderForm({
      ratings: [
        { criterionId: 'c-bv', ratingValue: 8, ratingLevelId: 'l-high' },
        { criterionId: 'c-js', ratingValue: 2 },
      ],
    })

    // Act
    await act(() =>
      userEvent.click(screen.getByRole('button', { name: 'Save Score' })),
    )

    // Assert
    expect(await screen.findByText('Enter a number.')).toBeInTheDocument()
  })

  it('previews the outputs on the server before saving', async () => {
    // Arrange
    mockPreview.mockReturnValue({
      unwrap: () =>
        Promise.resolve({
          primaryValue: 4,
          outputs: [
            {
              token: 'WSJF',
              name: 'WSJF',
              value: 4,
              isPrimary: true,
              order: 1,
            },
          ],
        }),
    })
    await renderForm({
      ratings: [
        { criterionId: 'c-bv', ratingValue: 8, ratingLevelId: 'l-high' },
        { criterionId: 'c-js', ratingValue: 2 },
      ],
    })

    // Act
    await act(() =>
      userEvent.click(
        screen.getByText('Preview', { selector: 'a, button, span' }),
      ),
    )

    // Assert
    expect(mockPreview).toHaveBeenCalledWith(
      expect.objectContaining({ requestId: 'r-1' }),
    )
    expect(await screen.findByText('WSJF = BV / JS')).toBeInTheDocument()
  })

  it('shows the need-by and what makes it that date beside the ratings', async () => {
    // Act
    await act(async () =>
      render(
        <ScoreIntakeRequestForm
          requestId="r-1"
          scoringModel={model}
          modelArchived={false}
          needBy={
            {
              date: '2027-01-31',
              basis: 'Commitment',
              family: 'ChosenByUs',
              reference: 'Acme renewal',
              committedBy: { id: 'e-2', key: 2, name: 'Dana Lead' },
            } as any
          }
          currentScore={undefined}
          onFormComplete={jest.fn()}
          onFormCancel={jest.fn()}
        />,
      ),
    )

    // Assert
    expect(
      screen.getByText(/Dana Lead promised it to Acme renewal/),
    ).toBeInTheDocument()
  })
})
