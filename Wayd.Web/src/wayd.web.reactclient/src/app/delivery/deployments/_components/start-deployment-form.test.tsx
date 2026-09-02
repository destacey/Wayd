import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import StartDeploymentForm from './start-deployment-form'

jest.unmock('dayjs')

const startDeployment = jest.fn()
const errorMessage = jest.fn()
const successMessage = jest.fn()

jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: errorMessage, success: successMessage }),
}))

jest.mock('@/src/store/features/delivery/deployments-api', () => ({
  useStartDeploymentMutation: () => [startDeployment],
}))

jest.mock('@/src/store/features/delivery/versions-api', () => ({
  useGetVersionsQuery: () => ({
    data: [
      {
        id: 'version-1',
        key: 3,
        number: '4.8.2',
        product: { id: 'product-1', key: 7, name: 'Wayd API' },
      },
    ],
    isLoading: false,
  }),
}))

jest.mock('@/src/store/features/delivery/release-packages-api', () => ({
  useGetReleasePackagesQuery: () => ({
    data: [{ id: 'package-1', key: 4, version: '2026.04' }],
    isLoading: false,
  }),
}))

const getEnvironments = jest.fn((_request?: { isActive?: boolean }) => ({
  data: [
    {
      id: 'environment-1',
      key: 5,
      name: 'Production',
      category: 'Production',
      ringOrder: 4,
      isActive: true,
      deploymentCount: 12,
    },
  ],
  isLoading: false,
}))

jest.mock('@/src/store/features/delivery/deployment-environments-api', () => ({
  useGetDeploymentEnvironmentsQuery: (request?: { isActive?: boolean }) =>
    getEnvironments(request),
}))

/**
 * The live form instance, so a test can put the form into a state the UI alone cannot reach.
 * Assigned on every render of the mocked hook below.
 */
let capturedForm: { setFieldsValue: (values: object) => void } | undefined

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
      capturedForm = form
      return {
        form,
        isOpen: true,
        isValid: true,
        isSaving: false,
        // `true` includes fields that are not currently mounted. Without it antd returns only the
        // rendered ones, which would silently drop a stale value before the component's own guard
        // ever sees it — hiding exactly what the XOR test is there to check.
        handleOk: async () => await onSubmit(form.getFieldsValue(true), form),
        handleCancel: jest.fn(),
      }
    },
  }
})

const renderForm = () =>
  render(
    <StartDeploymentForm onFormComplete={() => {}} onFormCancel={() => {}} />,
  )

const save = async () => {
  await userEvent.click(screen.getByRole('button', { name: 'Start' }))
}

/**
 * Finds a Select by its form field rather than its label.
 *
 * The Segmented toggle renders radios labeled "Version" and "Package" as well, so a label lookup is
 * ambiguous; antd's Selects carry no accessible name of their own. The id antd derives from the form
 * name and the field name identifies each one exactly.
 */
const queryPicker = (field: string) =>
  document.querySelector<HTMLElement>(`#start-deployment-form_${field}`)

const picker = (field: string) => {
  const element = queryPicker(field)
  if (!element) throw new Error(`No picker for field "${field}"`)
  return element
}

const pickOption = async (field: string, optionText: string) => {
  await userEvent.click(picker(field))
  await userEvent.click(await screen.findByTitle(optionText))
}

/**
 * Moves the Segmented toggle.
 *
 * Its radio input sits behind the visible label with `pointer-events: none`, which user-event
 * refuses to click, so the click goes to the label the way a person's would.
 */
const toggleTo = async (value: 'Version' | 'Package') => {
  const input = screen.getByRole('radio', { name: value })
  await userEvent.click(input.closest('label') as HTMLElement)
}

beforeEach(() => {
  jest.clearAllMocks()
  startDeployment.mockResolvedValue({ data: { id: 'deployment-1', key: 9 } })
})

describe('StartDeploymentForm', () => {
  it('offers one picker at a time rather than two optional ones', async () => {
    // Arrange / Act
    renderForm()

    // Assert — the API validates VersionId XOR PackageId in three places, so the form makes the
    // invalid combinations unexpressible rather than merely discouraged.
    expect(picker('versionId')).toBeInTheDocument()
    expect(queryPicker('packageId')).toBeNull()
  })

  it('swaps the picker when the toggle moves', async () => {
    // Arrange
    renderForm()

    // Act
    await toggleTo('Package')

    // Assert
    expect(picker('packageId')).toBeInTheDocument()
    expect(queryPicker('versionId')).toBeNull()
  })

  it('sends only the version when deploying a version', async () => {
    // Arrange
    renderForm()
    await pickOption('versionId', 'Wayd API 4.8.2')
    await pickOption('environmentId', 'Production (Production)')

    // Act
    await save()

    // Assert
    expect(startDeployment).toHaveBeenCalledTimes(1)
    const request = startDeployment.mock.calls[0][0]
    expect(request.versionId).toBe('version-1')
    expect(request.packageId).toBeUndefined()
  })

  it('sends only the package when deploying a package', async () => {
    // Arrange
    renderForm()
    await toggleTo('Package')
    await pickOption('packageId', '2026.04')
    await pickOption('environmentId', 'Production (Production)')

    // Act
    await save()

    // Assert
    const request = startDeployment.mock.calls[0][0]
    expect(request.packageId).toBe('package-1')
    expect(request.versionId).toBeUndefined()
  })

  it('never sends both, even after a version was picked and the toggle moved', async () => {
    // Arrange — the case a pair of optional pickers would get wrong: a value chosen under the old
    // toggle position must not travel alongside the new one.
    renderForm()
    await pickOption('versionId', 'Wayd API 4.8.2')
    await toggleTo('Package')
    await pickOption('packageId', '2026.04')
    await pickOption('environmentId', 'Production (Production)')

    // Act
    await save()

    // Assert
    const request = startDeployment.mock.calls[0][0]
    expect(request.packageId).toBe('package-1')
    expect(request.versionId).toBeUndefined()
  })

  it('sends only the toggled side even when the form still holds the other', async () => {
    // Arrange — there are two guards here, and this pins the one the happy path cannot reach.
    // Clearing the other field as the toggle moves is the first; deriving the request from the
    // toggle rather than from whatever the form holds is the second. Because the first guard
    // normally empties the losing field, a test that only toggles would still pass if the second
    // were deleted — so the stale value is injected directly, standing in for any path that leaves
    // one behind (a restored draft, or a later field added without a matching clear).
    renderForm()
    await toggleTo('Package')
    await pickOption('packageId', '2026.04')
    await pickOption('environmentId', 'Production (Production)')

    capturedForm!.setFieldsValue({ versionId: 'version-1' })

    // Act
    await save()

    // Assert — whatever the form holds, the request names exactly the toggled side.
    const request = startDeployment.mock.calls[0][0]
    expect(request.packageId).toBe('package-1')
    expect(request.versionId).toBeUndefined()
  })

  it('offers only active environments', async () => {
    // Arrange / Act
    renderForm()

    // Assert — the handler refuses an inactive environment, so a retired one in the list would turn
    // that into a failed submit rather than an unavailable choice.
    expect(getEnvironments).toHaveBeenCalledWith({ isActive: true })
  })
})
