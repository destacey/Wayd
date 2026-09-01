import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import PlanReleaseForm from './plan-release-form'

jest.unmock('dayjs')

const planRelease = jest.fn()
const cutRelease = jest.fn()
const markReleased = jest.fn()
const errorMessage = jest.fn()
const successMessage = jest.fn()

jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: errorMessage, success: successMessage }),
}))

jest.mock('@/src/store/features/delivery/releases-api', () => ({
  usePlanReleaseMutation: () => [planRelease],
  useCutReleaseMutation: () => [cutRelease],
  useMarkReleaseReleasedMutation: () => [markReleased],
}))

jest.mock('@/src/store/features/product-management/products-api', () => ({
  useGetProductsQuery: () => ({
    data: [{ id: 'product-1', key: 7, name: 'Wayd API', isReleasable: true }],
    isLoading: false,
  }),
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

const renderForm = () =>
  render(
    <PlanReleaseForm onFormComplete={() => {}} onFormCancel={() => {}} />,
  )

const save = async () => {
  await userEvent.click(screen.getByRole('button', { name: 'Add' }))
}

const typeDate = async (label: string, value: string) => {
  const input = screen.getByLabelText(label)
  await userEvent.click(input)
  await userEvent.type(input, value)
  await userEvent.keyboard('{Enter}')
}

describe('PlanReleaseForm', () => {
  beforeEach(() => {
    planRelease.mockReset().mockResolvedValue({ data: { id: 'r1', key: 4 } })
    cutRelease.mockReset().mockResolvedValue({ data: undefined })
    markReleased.mockReset().mockResolvedValue({ data: undefined })
    errorMessage.mockReset()
    successMessage.mockReset()
  })

  it('records only the release when no dates are given', async () => {
    // Arrange — a release genuinely being planned has neither date yet.
    renderForm()

    // Act
    await save()

    // Assert
    expect(planRelease).toHaveBeenCalledTimes(1)
    expect(cutRelease).not.toHaveBeenCalled()
    expect(markReleased).not.toHaveBeenCalled()
  })

  it('walks the lifecycle for a release entered after it shipped', async () => {
    // Arrange — the case this exists for: someone recording what already happened.
    renderForm()
    await typeDate('Cut Date', '2026-04-10')
    await typeDate('Released Date', '2026-04-20')

    // Act
    await save()

    // Assert
    expect(planRelease).toHaveBeenCalledTimes(1)
    expect(cutRelease).toHaveBeenCalledTimes(1)
    expect(markReleased).toHaveBeenCalledTimes(1)
  })

  it('reports the release it created when a later step fails', async () => {
    // Arrange — the release exists by then, so telling someone it failed would invite a second one.
    cutRelease.mockResolvedValue({ error: { status: 400 } })
    renderForm()
    await typeDate('Cut Date', '2026-04-10')

    // Act
    await save()

    // Assert
    expect(markReleased).not.toHaveBeenCalled()
    expect(errorMessage).toHaveBeenCalledWith(
      expect.stringContaining('Release 4 was created'),
    )
    expect(successMessage).not.toHaveBeenCalled()
  })
})
