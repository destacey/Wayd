import { ReleaseDto } from '@/src/services/wayd-api'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import CorrectReleaseDatesForm from './correct-release-dates-form'

jest.unmock('dayjs')

const correctDates = jest.fn()

jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: jest.fn(), success: jest.fn() }),
}))

jest.mock('@/src/store/features/delivery/releases-api', () => ({
  useCorrectReleaseDatesMutation: () => [correctDates],
}))

// Not spread from the real barrel: it re-exports store-bound hooks, and pulling those in
// initialises a store this form never touches. The form instance is real, though — antd's Form
// binds to it, and a stub breaks on render.
jest.mock('@/src/hooks', () => {
  const { Form } = jest.requireActual('antd')
  return {
    useModalForm: ({
      onSubmit,
    }: {
      onSubmit: (
        values: Record<string, unknown>,
        form: unknown,
      ) => Promise<boolean>
    }) => {
      const [form] = Form.useForm()
      return {
        form,
        isOpen: true,
        isValid: true,
        isSaving: false,
        handleOk: async () => await onSubmit(form.getFieldsValue(), form),
        handleCancel: jest.fn(),
      }
    },
  }
})

const release = (overrides: Partial<ReleaseDto> = {}): ReleaseDto =>
  ({
    id: 'release-1',
    key: 4,
    product: { id: 'product-1', key: 7, name: 'Wayd API' },
    version: '4.8.2',
    ...overrides,
  }) as ReleaseDto

const released = () =>
  release({
    cutDate: '2026-04-01' as unknown as Date,
    releasedDate: '2026-04-02' as unknown as Date,
  })

const renderForm = (dto: ReleaseDto) =>
  render(
    <CorrectReleaseDatesForm
      release={dto}
      onFormComplete={() => {}}
      onFormCancel={() => {}}
    />,
  )

describe('CorrectReleaseDatesForm', () => {
  beforeEach(() => {
    correctDates.mockReset().mockResolvedValue({ data: undefined })
  })

  it('pre-fills the dates already recorded', () => {
    // Arrange / Act — a correction starts from what is there; the field is rarely blank.
    renderForm(released())

    // Assert
    expect(screen.getByLabelText('Cut Date')).toHaveValue('2026-04-01')
    expect(screen.getByLabelText('Released Date')).toHaveValue('2026-04-02')
  })

  it('offers only the dates the release has', () => {
    // Arrange — adding a date is a lifecycle step and belongs to Cut or Mark Released, which move
    // the status too. Offering the field here would invite a change the API refuses.
    // Act
    renderForm(release({ cutDate: '2026-04-01' as unknown as Date }))

    // Assert
    expect(screen.getByLabelText('Cut Date')).toBeInTheDocument()
    expect(screen.queryByLabelText('Released Date')).not.toBeInTheDocument()
  })

  it('sends both dates when one is corrected', () => {
    // Arrange — the API takes the pair, since the ordering rule spans them.
    renderForm(released())

    // Act
    return userEvent
      .click(screen.getByRole('button', { name: 'Save' }))
      .then(() => {
        // Assert
        expect(correctDates).toHaveBeenCalledWith({
          id: 'release-1',
          request: expect.objectContaining({
            cutDate: '2026-04-01',
            releasedDate: '2026-04-02',
          }),
        })
      })
  })

  it('says the status is left alone', () => {
    // Arrange / Act — the distinction from Cut and Mark Released is the reason this action exists,
    // so it is stated rather than left to be inferred from the absence of a status field.
    renderForm(released())

    // Assert
    expect(
      screen.getByText('Corrects what was recorded, not what happened.'),
    ).toBeInTheDocument()
  })
})
