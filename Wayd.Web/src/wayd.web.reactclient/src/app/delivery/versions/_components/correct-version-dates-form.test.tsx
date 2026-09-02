import { VersionDto } from '@/src/services/wayd-api'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import CorrectVersionDatesForm from './correct-version-dates-form'

jest.unmock('dayjs')

const correctDates = jest.fn()

jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: jest.fn(), success: jest.fn() }),
}))

jest.mock('@/src/store/features/delivery/versions-api', () => ({
  useCorrectVersionDatesMutation: () => [correctDates],
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

const version = (overrides: Partial<VersionDto> = {}): VersionDto =>
  ({
    id: 'version-1',
    key: 4,
    product: { id: 'product-1', key: 7, name: 'Wayd API' },
    version: '4.8.2',
    ...overrides,
  }) as VersionDto

const released = () =>
  version({
    cutDate: '2026-04-01' as unknown as Date,
    releasedDate: '2026-04-02' as unknown as Date,
  })

const renderForm = (dto: VersionDto) =>
  render(
    <CorrectVersionDatesForm
      version={dto}
      onFormComplete={() => {}}
      onFormCancel={() => {}}
    />,
  )

describe('CorrectVersionDatesForm', () => {
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

  it('offers every date, including ones the version does not have', () => {
    // Arrange — a missing date is as likely to be the error as a wrong one. A version can be marked
    // released without ever being cut, so the cut date is commonly filled in afterwards; hiding the
    // field left no route to it at all.
    // Act
    renderForm(version({ cutDate: '2026-04-01' as unknown as Date }))

    // Assert
    expect(screen.getByLabelText('Target Date')).toBeInTheDocument()
    expect(screen.getByLabelText('Cut Date')).toBeInTheDocument()
    expect(screen.getByLabelText('Released Date')).toBeInTheDocument()
  })

  it('sends all three dates when one is corrected', () => {
    // Arrange — the API takes them together, since the ordering rule spans the pair and an omitted
    // date is a cleared one rather than an unchanged one.
    renderForm(released())

    // Act
    return userEvent
      .click(screen.getByRole('button', { name: 'Save' }))
      .then(() => {
        // Assert
        expect(correctDates).toHaveBeenCalledWith({
          id: 'version-1',
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
