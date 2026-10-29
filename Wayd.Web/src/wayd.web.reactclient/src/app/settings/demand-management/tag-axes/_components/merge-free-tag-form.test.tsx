import { act, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import MergeFreeTagForm from './merge-free-tag-form'

const mockError = jest.fn()
jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: mockError, success: jest.fn() }),
}))

// The form instance is real so antd's Form binds to it; handleOk runs the real submit.
jest.mock('@/src/hooks', () => {
  const { Form: AntForm } = jest.requireActual('antd')
  return {
    useModalForm: ({
      onSubmit,
      onComplete,
    }: {
      onSubmit: (values: unknown, form: unknown) => Promise<boolean>
      onComplete: () => void
    }) => {
      const [form] = AntForm.useForm()
      return {
        form,
        isOpen: true,
        isValid: true,
        isSaving: false,
        handleOk: async () => {
          if (await onSubmit(form.getFieldsValue(), form)) onComplete()
        },
        handleCancel: jest.fn(),
      }
    },
  }
})

const source = {
  id: 't-payment',
  name: 'payment',
  isFree: true,
  isActive: true,
  requestCount: 2,
}

const mockMerge = jest.fn()
jest.mock(
  '@/src/store/features/demand-management/demand-management-api',
  () => ({
    useMergeIntakeRequestTagMutation: () => [mockMerge],
    useSearchIntakeRequestTagsQuery: () => ({
      data: [
        source,
        {
          id: 't-payments',
          name: 'Payments',
          categoryId: 'a-area',
          categoryName: 'Area',
          isFree: false,
          isActive: true,
          requestCount: 5,
        },
        {
          id: 't-billing',
          name: 'billing',
          isFree: true,
          isActive: true,
          requestCount: 1,
        },
      ],
      isLoading: false,
    }),
  }),
)

const openTargets = async () => {
  const input = screen.getByRole('combobox')
  await act(async () => {
    await userEvent.setup().click(input)
  })
  const listboxId = input.getAttribute('aria-controls')!
  return await waitFor(() => {
    const el = document
      .getElementById(listboxId)
      ?.closest('.ant-select-dropdown') as HTMLElement | null
    if (!el) throw new Error('no dropdown')
    return el
  })
}

describe('MergeFreeTagForm', () => {
  beforeEach(() => {
    mockMerge.mockReset()
    mockError.mockReset()
    mockMerge.mockReturnValue({ unwrap: () => Promise.resolve() })
  })

  it('offers every other tag, a tag on an axis named with its axis', async () => {
    // Arrange
    await act(async () =>
      render(
        <MergeFreeTagForm
          tag={source}
          onFormComplete={jest.fn()}
          onFormCancel={jest.fn()}
        />,
      ),
    )

    // Act
    const dropdown = await openTargets()

    // Assert — a tag cannot be merged into itself, and axis tags come first
    expect(within(dropdown).getByTitle('Area | Payments')).toBeInTheDocument()
    expect(within(dropdown).getByTitle('billing')).toBeInTheDocument()
    expect(within(dropdown).queryByTitle('payment')).not.toBeInTheDocument()
  })

  it('merges into the chosen tag', async () => {
    // Arrange
    const onComplete = jest.fn()
    await act(async () =>
      render(
        <MergeFreeTagForm
          tag={source}
          onFormComplete={onComplete}
          onFormCancel={jest.fn()}
        />,
      ),
    )
    const dropdown = await openTargets()
    await act(async () => {
      await userEvent
        .setup()
        .click(within(dropdown).getByTitle('Area | Payments'))
    })

    // Act
    await act(() =>
      userEvent.click(screen.getByRole('button', { name: 'Merge' })),
    )

    // Assert
    expect(mockMerge).toHaveBeenCalledWith({
      id: 't-payment',
      targetTagId: 't-payments',
    })
    expect(onComplete).toHaveBeenCalled()
  })
})
