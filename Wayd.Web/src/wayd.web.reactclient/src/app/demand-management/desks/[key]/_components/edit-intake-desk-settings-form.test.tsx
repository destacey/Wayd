import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { IntakeDeskRole, IntakeDeskVisibility } from '@/src/services/wayd-api'
import EditIntakeDeskSettingsForm from './edit-intake-desk-settings-form'

jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: jest.fn(), success: jest.fn() }),
}))

// In Jest, Form.useWatch does not refire after typing, so keep the form instance real and submit through
// validateFields, which, like the real hook, returns only the fields that are mounted.
jest.mock('@/src/hooks', () => {
  const { Form } = jest.requireActual('antd')
  return {
    useModalForm: ({
      onSubmit,
      onComplete,
    }: {
      onSubmit: (
        values: Record<string, unknown>,
        form: unknown,
      ) => Promise<boolean>
      onComplete: () => void
    }) => {
      const [form] = Form.useForm()
      return {
        form,
        isOpen: true,
        isValid: true,
        isSaving: false,
        handleOk: async () => {
          const values = await form.validateFields().catch(() => null)
          if (values && (await onSubmit(values, form))) onComplete()
        },
        handleCancel: jest.fn(),
      }
    },
  }
})

const mockUpdate = jest.fn()
jest.mock(
  '@/src/store/features/demand-management/demand-management-api',
  () => ({
    useUpdateIntakeDeskMutation: () => [mockUpdate],
    useGetIntakeChannelsQuery: () => ({
      data: [
        { id: 'c-1', name: 'Support', order: 1, isActive: true },
        { id: 'c-2', name: 'Security', order: 2, isActive: true },
        { id: 'c-3', name: 'Engineering', order: 3, isActive: false },
      ],
    }),
    useGetIntakeRequestTagCategoriesQuery: () => ({
      data: [
        { id: 'a-1', name: 'Area', isActive: true, tags: [] },
        { id: 'a-2', name: 'Segment', isActive: true, tags: [] },
        { id: 'a-3', name: 'Retired', isActive: false, tags: [] },
      ],
    }),
    useGetIntakeDeskVisibilitiesQuery: () => ({
      data: [
        {
          id: 1,
          code: 'Open',
          name: 'Open',
          description: 'Anyone reads it.',
          order: 1,
        },
        {
          id: 3,
          code: 'Private',
          name: 'Private',
          description: 'Only the desk.',
          order: 3,
        },
      ],
    }),
  }),
)

const desk = (overrides: Record<string, unknown> = {}) =>
  ({
    id: 'd-1',
    key: 1,
    name: 'IT',
    description: 'Laptops, access and software',
    visibility: IntakeDeskVisibility.Open,
    isOpen: true,
    isRequesterSelectable: true,
    requesterMayAddParticipants: true,
    offersAllChannels: true,
    channelIds: [],
    offersAllTagCategories: true,
    tagCategoryIds: [],
    allowsFreeTags: false,
    members: [
      {
        employee: { id: 'e-1', key: 1, name: 'Avery' },
        role: IntakeDeskRole.Admin,
      },
      {
        employee: { id: 'e-2', key: 2, name: 'Blake' },
        role: IntakeDeskRole.Agent,
      },
    ],
    canManage: true,
    canAdminister: false,
    canDelete: false,
    revision: 4,
    ...overrides,
  }) as any

let rerenderWith: (d: ReturnType<typeof desk>) => void = () => undefined

const renderForm = async (d = desk()) =>
  await act(async () => {
    const element = (next: ReturnType<typeof desk>) => (
      <EditIntakeDeskSettingsForm
        desk={next}
        onFormComplete={jest.fn()}
        onFormCancel={jest.fn()}
      />
    )
    const view = render(element(d))
    rerenderWith = (next) => view.rerender(element(next))
  })

describe('EditIntakeDeskSettingsForm', () => {
  beforeEach(() => {
    mockUpdate.mockReset()
    mockUpdate.mockReturnValue({ unwrap: () => Promise.resolve() })
  })

  it('lets a Desk Admin see but not change the visibility', async () => {
    // Arrange / Act
    await renderForm(desk({ canAdminister: false }))

    // Assert
    expect(screen.getByRole('radio', { name: /Private/ })).toBeDisabled()
    expect(
      screen.getByText(
        'Only the Demand Management Admin can change who can see the requests on a desk.',
      ),
    ).toBeInTheDocument()
  })

  it('lets the Demand Management Admin change the visibility', async () => {
    // Arrange / Act
    await renderForm(desk({ canAdminister: true }))

    // Assert
    expect(screen.getByRole('radio', { name: /Private/ })).toBeEnabled()
  })

  it('saves the changed settings with the desk otherwise unchanged', async () => {
    // Arrange
    await renderForm()
    const name = screen.getByLabelText('Name')
    await userEvent.clear(name)
    await userEvent.type(name, 'IT services')

    // Act
    await act(() =>
      userEvent.click(screen.getByRole('button', { name: 'Save' })),
    )

    // Assert
    expect(mockUpdate).toHaveBeenCalledWith({
      id: 'd-1',
      expectedRevision: 4,
      name: 'IT services',
      description: 'Laptops, access and software',
      visibility: IntakeDeskVisibility.Open,
      isRequesterSelectable: true,
      isOpen: true,
      requesterMayAddParticipants: true,
      offersAllChannels: true,
      channelIds: [],
      offersAllTagCategories: true,
      tagCategoryIds: [],
      allowsFreeTags: false,
      members: [
        { employeeId: 'e-1', role: IntakeDeskRole.Admin },
        { employeeId: 'e-2', role: IntakeDeskRole.Agent },
      ],
    })
  })

  it('saves the tag axes a desk chooses to offer', async () => {
    // Arrange
    await renderForm()
    await act(() =>
      userEvent.click(
        screen.getByRole('radio', { name: 'Only the tag axes chosen here' }),
      ),
    )
    await act(() =>
      userEvent.click(screen.getByRole('checkbox', { name: 'Segment' })),
    )

    // Act
    await act(() =>
      userEvent.click(screen.getByRole('button', { name: 'Save' })),
    )

    // Assert
    expect(mockUpdate).toHaveBeenCalledWith(
      expect.objectContaining({
        offersAllTagCategories: false,
        tagCategoryIds: ['a-2'],
      }),
    )
  })

  it('saves free tags turned on', async () => {
    // Arrange
    await renderForm()
    await act(() =>
      userEvent.click(screen.getByRole('switch', { name: 'Allow free tags' })),
    )

    // Act
    await act(() =>
      userEvent.click(screen.getByRole('button', { name: 'Save' })),
    )

    // Assert
    expect(mockUpdate).toHaveBeenCalledWith(
      expect.objectContaining({ allowsFreeTags: true }),
    )
  })

  it('leaves out an inactive tag axis the desk never chose', async () => {
    // Arrange
    await renderForm(
      desk({ offersAllTagCategories: false, tagCategoryIds: [] }),
    )

    // Assert
    expect(
      screen.queryByRole('checkbox', { name: /Retired/ }),
    ).not.toBeInTheDocument()
  })

  it('saves the channels a desk chooses to offer', async () => {
    // Arrange
    await renderForm()
    await act(() =>
      userEvent.click(
        screen.getByRole('radio', { name: 'Only the channels chosen here' }),
      ),
    )
    await act(() =>
      userEvent.click(screen.getByRole('checkbox', { name: 'Security' })),
    )

    // Act
    await act(() =>
      userEvent.click(screen.getByRole('button', { name: 'Save' })),
    )

    // Assert
    expect(mockUpdate).toHaveBeenCalledWith(
      expect.objectContaining({
        offersAllChannels: false,
        channelIds: ['c-2'],
      }),
    )
  })

  it('offers an archived channel only where the desk already chose it', async () => {
    // Arrange / Act
    await renderForm(desk({ offersAllChannels: false, channelIds: ['c-3'] }))

    // Assert
    expect(
      screen.getByRole('checkbox', { name: 'Engineering (Archived)' }),
    ).toBeChecked()
    expect(screen.getByRole('checkbox', { name: 'Support' })).not.toBeChecked()
  })

  it('leaves out an archived channel the desk never chose', async () => {
    // Arrange / Act
    await renderForm(desk({ offersAllChannels: false, channelIds: [] }))

    // Assert
    expect(screen.queryByText(/Engineering/)).not.toBeInTheDocument()
  })

  it('keeps the revision it opened with when the page refetches the desk, so a retry stays refused', async () => {
    // Arrange — a failed save refetches the desk; the form must not adopt the newer revision with older values
    await renderForm(desk({ revision: 4 }))
    await act(async () =>
      rerenderWith(desk({ revision: 5, name: 'Changed by someone else' })),
    )

    // Act
    await act(() =>
      userEvent.click(screen.getByRole('button', { name: 'Save' })),
    )

    // Assert
    expect(mockUpdate).toHaveBeenCalledWith(
      expect.objectContaining({ expectedRevision: 4 }),
    )
  })
})
