import { act, render, screen } from '@testing-library/react'
import { DeploymentDto, ProductStatusAlias, StatusCategory } from '@/src/services/wayd-api'
import CompleteDeploymentForm from './complete-deployment-form'

jest.unmock('dayjs')

const succeedDeployment = jest.fn()
const failDeployment = jest.fn()
const errorMessage = jest.fn()
const successMessage = jest.fn()

jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: errorMessage, success: successMessage }),
}))

jest.mock('@/src/store/features/delivery/deployments-api', () => ({
  useSucceedDeploymentMutation: () => [succeedDeployment],
  useFailDeploymentMutation: () => [failDeployment],
}))

jest.mock('@/src/components/contexts/auth', () => ({
  __esModule: true,
  default: () => ({ hasPermissionClaim: () => true }),
}))

/** The submit handler from the live form, so a test need not click a jsdom-disabled button. */
let submitForm: (() => Promise<void>) | undefined

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
      const handleOk = async () => {
        await onSubmit(form.getFieldsValue(true), form)
      }
      submitForm = handleOk
      return {
        form,
        isOpen: true,
        isValid: true,
        isSaving: false,
        handleOk,
        handleCancel: jest.fn(),
      }
    },
  }
})

const deployment = (): DeploymentDto =>
  ({
    id: '11111111-1111-1111-1111-111111111111',
    key: 1,
    release: { id: 'r', key: 2, name: '4.10.0' },
    environment: { id: 'e', key: 3, name: 'prod-eu' },
    startedAt: new Date('2026-04-01T10:00:00Z'),
    status: {
      id: 's',
      name: 'In Progress',
      category: StatusCategory.Active,
      alias: 20,
    },
    outcome: ProductStatusAlias.InProgress,
    isComplete: false,
    isChangeFailure: false,
  }) as DeploymentDto

const renderForm = (outcome: 'Succeeded' | 'Failed') =>
  render(
    <CompleteDeploymentForm
      deployment={deployment()}
      outcome={outcome}
      onFormComplete={() => {}}
      onFormCancel={() => {}}
    />,
  )

const submit = async () => {
  await act(async () => {
    await submitForm!()
  })
}

beforeEach(() => {
  jest.clearAllMocks()
  succeedDeployment.mockResolvedValue({ data: undefined })
  failDeployment.mockResolvedValue({ data: undefined })
})

/**
 * The OK button's enabled state is deliberately not asserted here.
 *
 * `useModalForm` derives `isValid` from `form.validateFields()`, whose promise never settles under
 * jsdom — antd's async validator queue depends on scheduling jsdom does not drive. The button is
 * therefore disabled in every one of these tests regardless of the component, so an assertion on it
 * would be testing the environment rather than the code. It is checked in the browser instead.
 *
 * `handleOk` is invoked directly below for the same reason: clicking a permanently-disabled button
 * would exercise nothing.
 */
describe('CompleteDeploymentForm', () => {
  it('offers a reason only when recording a failure', () => {
    // Arrange / Act
    const { unmount } = renderForm('Succeeded')

    // Assert — a success has no reason to give.
    expect(screen.queryByLabelText('Reason')).not.toBeInTheDocument()

    unmount()
    renderForm('Failed')
    expect(screen.getByLabelText('Reason')).toBeInTheDocument()
  })

  it('sends a success through the succeed mutation with no completion date', async () => {
    // Arrange — an omitted date means "now", which the server fills in.
    renderForm('Succeeded')

    // Act
    await submit()

    // Assert
    expect(succeedDeployment).toHaveBeenCalledTimes(1)
    expect(failDeployment).not.toHaveBeenCalled()
    expect(succeedDeployment.mock.calls[0][0].request.completedAt).toBeUndefined()
  })

  it('sends a failure through the fail mutation, never succeed', async () => {
    // Arrange — the two outcomes share a form, so the wrong mutation is a live possibility.
    renderForm('Failed')

    // Act
    await submit()

    // Assert
    expect(failDeployment).toHaveBeenCalledTimes(1)
    expect(succeedDeployment).not.toHaveBeenCalled()
  })

  it('addresses the deployment it was opened for', async () => {
    // Arrange
    renderForm('Succeeded')

    // Act
    await submit()

    // Assert
    expect(succeedDeployment.mock.calls[0][0].id).toBe(
      '11111111-1111-1111-1111-111111111111',
    )
  })
})
