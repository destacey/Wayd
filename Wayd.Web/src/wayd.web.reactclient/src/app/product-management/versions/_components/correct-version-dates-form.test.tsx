import { VersionDto } from '@/src/services/wayd-api'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import dayjs from 'dayjs'
import CorrectVersionDatesForm from './correct-version-dates-form'

jest.unmock('dayjs')

const correctDates = jest.fn()

jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: jest.fn(), success: jest.fn() }),
}))

jest.mock('@/src/store/features/product-management/versions-api', () => ({
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

const CUT_AT = '2026-04-01T15:00:00Z'
// Late evening US Central: the 1st there, the 2nd in UTC.
const RELEASED_AT = '2026-04-02T02:30:00Z'

const released = () =>
  version({
    cutAt: CUT_AT as unknown as Date,
    releasedAt: RELEASED_AT as unknown as Date,
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

  it("pre-fills what is already recorded, in the viewer's zone", () => {
    // Arrange / Act — a correction starts from what is there; the field is rarely blank.
    renderForm(released())

    // Assert
    expect(screen.getByLabelText('Cut At')).toHaveValue(
      dayjs(CUT_AT).format('YYYY-MM-DD HH:mm:ss'),
    )
    expect(screen.getByLabelText('Released At')).toHaveValue(
      dayjs(RELEASED_AT).format('YYYY-MM-DD HH:mm:ss'),
    )
  })

  it('offers every value, including ones the version does not have', () => {
    // Arrange — a missing value is as likely to be the error as a wrong one. A version can be marked
    // released without ever being cut, so the cut moment is commonly filled in afterwards; hiding the
    // field left no route to it at all.
    // Act
    renderForm(version({ cutAt: CUT_AT as unknown as Date }))

    // Assert
    expect(screen.getByLabelText('Target Date')).toBeInTheDocument()
    expect(screen.getByLabelText('Cut At')).toBeInTheDocument()
    expect(screen.getByLabelText('Released At')).toBeInTheDocument()
  })

  it('sends all three values, the moments as instants', () => {
    // Arrange — the API takes them together, since the ordering rule spans the pair and an omitted
    // value is a cleared one rather than an unchanged one.
    renderForm(released())

    // Act
    return userEvent
      .click(screen.getByRole('button', { name: 'Save' }))
      .then(() => {
        // Assert
        expect(correctDates).toHaveBeenCalledWith({
          id: 'version-1',
          request: expect.objectContaining({
            cutAt: new Date(CUT_AT),
            releasedAt: new Date(RELEASED_AT),
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
