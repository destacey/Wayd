import { ReleasePackageDto } from '@/src/services/wayd-api'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import dayjs from 'dayjs'
import CorrectReleasePackageDatesForm from './correct-release-package-dates-form'

jest.unmock('dayjs')

const correctDates = jest.fn()

jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: jest.fn(), success: jest.fn() }),
}))

jest.mock(
  '@/src/store/features/product-management/release-packages-api',
  () => ({
    useCorrectReleasePackageDatesMutation: () => [correctDates],
  }),
)

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

const releasePackage = (
  overrides: Partial<ReleasePackageDto> = {},
): ReleasePackageDto =>
  ({
    id: 'package-1',
    key: 4,
    version: '2026.38',
    ...overrides,
  }) as ReleasePackageDto

// Late evening US Central: the 17th there, the 18th in UTC.
const RELEASED_AT = '2026-09-18T02:30:00Z'

const released = () =>
  releasePackage({
    targetDate: '2026-09-16' as unknown as Date,
    releasedAt: RELEASED_AT as unknown as Date,
  })

const renderForm = (dto: ReleasePackageDto) =>
  render(
    <CorrectReleasePackageDatesForm
      releasePackage={dto}
      onFormComplete={() => {}}
      onFormCancel={() => {}}
    />,
  )

describe('CorrectReleasePackageDatesForm', () => {
  beforeEach(() => {
    correctDates.mockReset().mockResolvedValue({ data: undefined })
  })

  it("pre-fills what is already recorded, the released moment in the viewer's zone", () => {
    // Arrange / Act — a correction starts from what is there.
    renderForm(released())

    // Assert
    expect(screen.getByLabelText('Target Date')).toHaveValue('2026-09-16')
    expect(screen.getByLabelText('Released At')).toHaveValue(
      dayjs(RELEASED_AT).format('YYYY-MM-DD HH:mm:ss'),
    )
  })

  it('offers no released moment on a package that has not been released', () => {
    // Arrange — the domain refuses adding one here: a released date closes the manifest, and
    // Mark Released is the action that records the first.
    // Act
    renderForm(releasePackage({ targetDate: '2026-09-16' as unknown as Date }))

    // Assert
    expect(screen.getByLabelText('Target Date')).toBeInTheDocument()
    expect(screen.queryByLabelText('Released At')).not.toBeInTheDocument()
  })

  it('sends both values, the released moment as an instant', async () => {
    // Arrange — an omitted target date is a cleared one, not an unchanged one.
    renderForm(released())

    // Act
    await userEvent.click(screen.getByRole('button', { name: 'Save' }))

    // Assert
    expect(correctDates).toHaveBeenCalledWith({
      id: 'package-1',
      request: {
        targetDate: '2026-09-16',
        releasedAt: new Date(RELEASED_AT),
      },
    })
  })

  it('says the status is left alone', () => {
    // Arrange / Act — the distinction from Mark Released is the reason this action exists.
    renderForm(released())

    // Assert
    expect(
      screen.getByText('Corrects what was recorded, not what happened.'),
    ).toBeInTheDocument()
  })
})
