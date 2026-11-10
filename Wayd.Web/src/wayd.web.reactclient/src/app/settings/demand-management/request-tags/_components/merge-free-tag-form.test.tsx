import { act, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import MergeFreeTagForm from './merge-free-tag-form'
import type { UseModalFormOptions } from '@/src/hooks'

const mockError = vi.fn()
vi.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: mockError, success: vi.fn() }),
}))

// The form instance is real so antd's Form binds to it; handleOk runs the real submit.
vi.mock(import('@/src/hooks'), async () => {
  const { Form: AntForm } = await vi.importActual<typeof import('antd')>('antd')
  return {
    useModalForm: <TValues,>({
      onSubmit,
      onComplete,
    }: UseModalFormOptions<TValues>) => {
      const [form] = AntForm.useForm<TValues>()
      return {
        form,
        isOpen: true,
        isValid: true,
        isSaving: false,
        handleOk: async () => {
          if (await onSubmit(form.getFieldsValue(), form)) onComplete()
        },
        handleCancel: vi.fn(),
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

const mockMerge = vi.fn()
vi.mock('@/src/store/features/demand-management/demand-management-api', () => ({
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
}))

const openTargets = async () => {
  const input = screen.getByRole('combobox')
  await userEvent.setup().click(input)
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

  it('offers every other tag, a tag in a category named with its category', async () => {
    // Arrange
    await act(async () =>
      render(
        <MergeFreeTagForm
          tag={source}
          onFormComplete={vi.fn()}
          onFormCancel={vi.fn()}
        />,
      ),
    )

    // Act
    const dropdown = await openTargets()

    // Assert — a tag cannot be merged into itself, and category tags come first
    expect(within(dropdown).getByTitle('Area | Payments')).toBeInTheDocument()
    expect(within(dropdown).getByTitle('billing')).toBeInTheDocument()
    expect(within(dropdown).queryByTitle('payment')).not.toBeInTheDocument()
  })

  it('merges into the chosen tag', async () => {
    // Arrange
    const onComplete = vi.fn()
    await act(async () =>
      render(
        <MergeFreeTagForm
          tag={source}
          onFormComplete={onComplete}
          onFormCancel={vi.fn()}
        />,
      ),
    )
    const dropdown = await openTargets()
    await userEvent
      .setup()
      .click(within(dropdown).getByTitle('Area | Payments'))

    // Act
    await userEvent.click(screen.getByRole('button', { name: 'Merge' }))

    // Assert
    expect(mockMerge).toHaveBeenCalledWith({
      id: 't-payment',
      targetTagId: 't-payments',
    })
    expect(onComplete).toHaveBeenCalled()
  })
})
