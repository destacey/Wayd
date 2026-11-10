import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import ChangeIntakeRequestEstimateForm from './change-intake-request-estimate-form'
import type { UseModalFormOptions } from '@/src/hooks'

vi.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: vi.fn(), success: vi.fn() }),
}))

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

const mockChangeEstimate = vi.fn()
vi.mock('@/src/store/features/demand-management/demand-management-api', () => ({
  useChangeIntakeRequestEstimateMutation: () => [mockChangeEstimate],
}))

const estimateOn = (value: string, versionId: string, scaleName: string) => ({
  value,
  position: 0,
  scale: {
    id: 's-1',
    key: 1,
    name: scaleName,
    versionId,
    versionNumber: 1,
    isCurrentVersion: true,
  },
})

const renderForm = async (estimate?: ReturnType<typeof estimateOn>) => {
  const onFormComplete = vi.fn()
  await act(async () =>
    render(
      <ChangeIntakeRequestEstimateForm
        request={
          {
            summary: { id: 'r-1', key: 7, desk: { name: 'IT' } },
            substance: { estimate },
            canChangeEstimate: true,
            estimateValues: ['S', 'M', 'L'],
            estimationScaleVersionId: 'v-1',
          } as any
        }
        onFormComplete={onFormComplete}
        onFormCancel={vi.fn()}
      />,
    ),
  )
  return { onFormComplete }
}

const submit = async () => {
  await userEvent.click(screen.getByRole('button', { name: 'Save' }))
}

describe('ChangeIntakeRequestEstimateForm', () => {
  beforeEach(() => {
    mockChangeEstimate.mockReset()
    mockChangeEstimate.mockReturnValue({ unwrap: () => Promise.resolve() })
  })

  it('offers the desk scale values in order, after None', async () => {
    // Act
    await renderForm()

    // Assert
    expect(
      screen.getAllByRole('radio').map((r) => r.closest('label')?.textContent),
    ).toEqual(['None', 'S', 'M', 'L'])
  })

  it('saves the chosen value', async () => {
    // Arrange
    await renderForm()
    await userEvent.click(screen.getByText('M'))

    // Act
    await submit()

    // Assert
    expect(mockChangeEstimate).toHaveBeenCalledWith({
      requestId: 'r-1',
      value: 'M',
    })
  })

  it('clears the estimate when None is chosen', async () => {
    // Arrange
    await renderForm(estimateOn('L', 'v-1', 'T-Shirt Sizes'))
    await userEvent.click(screen.getByText('None'))

    // Act
    await submit()

    // Assert
    expect(mockChangeEstimate).toHaveBeenCalledWith({
      requestId: 'r-1',
      value: undefined,
    })
  })

  it('keeps an estimate from another scale without sending it to be refused', async () => {
    // Arrange
    const { onFormComplete } = await renderForm(
      estimateOn('13', 'v-old', 'Fibonacci'),
    )

    // Act
    await submit()

    // Assert
    expect(screen.getByRole('radio', { name: '13 (version 1)' })).toBeChecked()
    expect(
      screen.getByText(/which this desk no longer uses/),
    ).toBeInTheDocument()
    expect(mockChangeEstimate).not.toHaveBeenCalled()
    expect(onFormComplete).toHaveBeenCalled()
  })

  it("tells an earlier version's value apart from the same value on the desk's version", async () => {
    // Arrange — M was given on version 1, and the desk now estimates on version 2, which also has M
    await renderForm(estimateOn('M', 'v-old', 'T-Shirt Sizes'))

    // Act
    await submit()
    await userEvent.click(screen.getByText('M'))
    await submit()

    // Assert — keeping it sends nothing; choosing the desk's M gives it on the desk's version
    expect(screen.getByText('M (version 1)')).toBeInTheDocument()
    expect(mockChangeEstimate).toHaveBeenCalledTimes(1)
    expect(mockChangeEstimate).toHaveBeenCalledWith({
      requestId: 'r-1',
      value: 'M',
    })
  })
})
