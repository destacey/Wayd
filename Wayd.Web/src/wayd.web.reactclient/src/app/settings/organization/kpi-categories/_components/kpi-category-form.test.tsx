import type { UseModalFormOptions } from '@/src/hooks'
import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import KpiCategoryForm from './kpi-category-form'

vi.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: vi.fn(), success: vi.fn() }),
}))

// Submit through validateFields, as the real hook does, with a real form instance.
vi.mock(import('@/src/hooks'), async () => {
  const { Form } = await vi.importActual<typeof import('antd')>('antd')
  return {
    useModalForm: <TValues,>({
      onSubmit,
      onComplete,
    }: UseModalFormOptions<TValues>) => {
      const [form] = Form.useForm<TValues>()
      return {
        form,
        isOpen: true,
        isValid: true,
        isSaving: false,
        handleOk: async () => {
          const values = await form.validateFields().catch(() => null)
          if (values && (await onSubmit(values, form))) onComplete()
        },
        handleCancel: vi.fn(),
      }
    },
  }
})

const mockCreate = vi.fn()
const mockUpdate = vi.fn()
vi.mock('@/src/store/features/common/kpis-api', () => ({
  useCreateKpiCategoryMutation: () => [mockCreate],
  useUpdateKpiCategoryMutation: () => [mockUpdate],
}))

const renderForm = async (
  category?: Parameters<typeof KpiCategoryForm>[0]['category'],
) => {
  const onFormComplete = vi.fn()
  await act(async () => {
    render(
      <KpiCategoryForm
        category={category}
        onFormComplete={onFormComplete}
        onFormCancel={vi.fn()}
      />,
    )
  })
  return { onFormComplete }
}

describe('KpiCategoryForm', () => {
  beforeEach(() => {
    mockCreate.mockReset()
    mockUpdate.mockReset()
    mockCreate.mockReturnValue({ unwrap: () => Promise.resolve('cat-2') })
    mockUpdate.mockReturnValue({ unwrap: () => Promise.resolve() })
  })

  it('adds a category with its name trimmed and a blank description left out', async () => {
    // Arrange
    const { onFormComplete } = await renderForm()
    await userEvent.type(
      screen.getByRole('textbox', { name: /Name/ }),
      ' Flow ',
    )
    await userEvent.type(
      screen.getByRole('textbox', { name: 'Description' }),
      '   ',
    )

    // Act
    await userEvent.click(screen.getByRole('button', { name: 'Add' }))

    // Assert
    expect(mockCreate).toHaveBeenCalledWith({
      name: 'Flow',
      description: undefined,
    })
    expect(onFormComplete).toHaveBeenCalled()
  })

  it('renames a category by its id', async () => {
    // Arrange
    await renderForm({
      id: 'cat-1',
      name: 'Flow',
      description: 'How work moves.',
      order: 0,
      isActive: true,
      kpiCount: 3,
    })
    const name = screen.getByRole('textbox', { name: /Name/ })
    await userEvent.clear(name)
    await userEvent.type(name, 'Delivery flow')

    // Act
    await userEvent.click(screen.getByRole('button', { name: 'Save' }))

    // Assert
    expect(mockCreate).not.toHaveBeenCalled()
    expect(mockUpdate).toHaveBeenCalledWith({
      id: 'cat-1',
      request: { name: 'Delivery flow', description: 'How work moves.' },
    })
  })

  it('marks the name when another category already has it', async () => {
    // Arrange
    mockCreate.mockReturnValue({
      unwrap: () =>
        Promise.reject({
          status: 422,
          errors: { Name: ['Another KPI category already has this name.'] },
        }),
    })
    const { onFormComplete } = await renderForm()
    await userEvent.type(screen.getByRole('textbox', { name: /Name/ }), 'Flow')

    // Act
    await userEvent.click(screen.getByRole('button', { name: 'Add' }))

    // Assert
    expect(
      await screen.findByText('Another KPI category already has this name.'),
    ).toBeInTheDocument()
    expect(onFormComplete).not.toHaveBeenCalled()
  })
})
