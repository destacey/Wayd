import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import ChangeIntakeRequestEstimateForm from './change-intake-request-estimate-form'

jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: jest.fn(), success: jest.fn() }),
}))

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

const mockChangeEstimate = jest.fn()
jest.mock(
  '@/src/store/features/demand-management/demand-management-api',
  () => ({
    useChangeIntakeRequestEstimateMutation: () => [mockChangeEstimate],
  }),
)

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
  const onFormComplete = jest.fn()
  await act(async () =>
    render(
      <ChangeIntakeRequestEstimateForm
        request={
          {
            summary: { id: 'r-1', key: 7, desk: { name: 'IT' } },
            substance: { estimate },
            canChangeEstimate: true,
            estimateValues: ['S', 'M', 'L'],
          } as any
        }
        onFormComplete={onFormComplete}
        onFormCancel={jest.fn()}
      />,
    ),
  )
  return { onFormComplete }
}

const submit = async () => {
  await act(() => userEvent.click(screen.getByRole('button', { name: 'Save' })))
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
    expect(screen.getByRole('radio', { name: '13' })).toBeChecked()
    expect(
      screen.getByText(/which this desk no longer uses/),
    ).toBeInTheDocument()
    expect(mockChangeEstimate).not.toHaveBeenCalled()
    expect(onFormComplete).toHaveBeenCalled()
  })
})
