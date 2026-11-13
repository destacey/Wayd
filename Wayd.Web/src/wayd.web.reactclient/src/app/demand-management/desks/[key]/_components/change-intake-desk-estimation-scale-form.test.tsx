import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import ChangeIntakeDeskEstimationScaleForm from './change-intake-desk-estimation-scale-form'
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

const mockChangeEstimationScale = vi.fn()
const mockOpenEstimates = vi.fn()
const mockOpenEstimatesState = vi.fn()
const mockRefetchOpenEstimates = vi.fn()
vi.mock('@/src/store/features/demand-management/demand-management-api', () => ({
  useChangeIntakeDeskEstimationScaleMutation: () => [mockChangeEstimationScale],
  useGetIntakeDeskOpenEstimatesQuery: () => ({
    data: mockOpenEstimates(),
    isFetching: false,
    error: undefined,
    refetch: mockRefetchOpenEstimates,
    ...mockOpenEstimatesState(),
  }),
  useGetIntakeEstimationScaleChoicesQuery: () => ({
    data: [
      {
        id: 's-1',
        key: 1,
        name: 'T-Shirt Sizes',
        versionNumber: 2,
        estimateValues: ['S', 'M', 'L'],
      },
      {
        id: 's-2',
        key: 2,
        name: 'Fibonacci',
        versionNumber: 1,
        estimateValues: ['1', '2', '3', '5'],
      },
    ],
    isLoading: false,
  }),
}))

const renderForm = async (estimationScale?: {
  id: string
  name: string
  isCurrentVersion?: boolean
}) =>
  await act(async () =>
    render(
      <ChangeIntakeDeskEstimationScaleForm
        desk={
          {
            id: 'd-1',
            name: 'IT',
            // Null, as the API sends a desk with no scale, rather than leaving the property out.
            estimationScale: estimationScale
              ? {
                  key: 9,
                  versionId: 'v-9',
                  versionNumber: 1,
                  isCurrentVersion: true,
                  ...estimationScale,
                }
              : null,
          } as any
        }
        onFormComplete={vi.fn()}
        onFormCancel={vi.fn()}
      />,
    ),
  )

const submit = async () => {
  await userEvent.click(screen.getByRole('button', { name: 'Change' }))
}

describe('ChangeIntakeDeskEstimationScaleForm', () => {
  beforeEach(() => {
    mockChangeEstimationScale.mockReset()
    mockChangeEstimationScale.mockReturnValue({
      unwrap: () => Promise.resolve(),
    })
    mockOpenEstimates.mockReturnValue([])
    mockOpenEstimatesState.mockReturnValue({})
  })

  it('names each scale with its values', async () => {
    // Arrange
    await renderForm()

    // Act
    await userEvent.click(screen.getByRole('combobox'))

    // Assert
    expect(await screen.findByText('S, M, L')).toBeInTheDocument()
    expect(screen.getByText('1, 2, 3, 5')).toBeInTheDocument()
  })

  it('gives the desk the chosen scale', async () => {
    // Arrange
    await renderForm()
    await userEvent.click(screen.getByRole('combobox'))
    await userEvent.click(await screen.findByTitle('T-Shirt Sizes'))

    // Act
    await submit()

    // Assert
    expect(mockChangeEstimationScale).toHaveBeenCalledWith({
      deskId: 'd-1',
      estimationScaleId: 's-1',
      openEstimates: 'Keep',
    })
  })

  it('stops estimating when no scale is chosen', async () => {
    // Act
    await renderForm()
    await submit()

    // Assert
    expect(mockChangeEstimationScale).toHaveBeenCalledWith({
      deskId: 'd-1',
      estimationScaleId: undefined,
      openEstimates: 'Keep',
    })
  })

  it('keeps an inactive scale the desk still estimates on showing by name', async () => {
    // Act
    await renderForm({ id: 's-old', name: 'Old Scale' })

    // Assert
    expect(screen.getByTitle('Old Scale (Inactive)')).toBeInTheDocument()
  })

  describe('with estimated open requests', () => {
    const onOldScale = (
      value: string,
      position: number,
      requestCount: number,
    ) => ({
      value,
      position,
      requestCount,
      scale: {
        id: 's-old',
        key: 9,
        name: 'Old Scale',
        versionId: 'v-9',
        versionNumber: 1,
        isCurrentVersion: true,
      },
    })

    beforeEach(() => {
      mockOpenEstimates.mockReturnValue([
        onOldScale('S', 0, 2),
        onOldScale('M', 1, 1),
      ])
    })

    it('counts them by value and version once the change would move the desk', async () => {
      // Arrange
      await renderForm({ id: 's-old', name: 'Old Scale' })
      expect(screen.queryByText('Their Estimates')).not.toBeInTheDocument()

      // Act
      await userEvent.click(screen.getByRole('combobox'))
      await userEvent.click(await screen.findByTitle('T-Shirt Sizes'))

      // Assert
      expect(
        screen.getByText('3 open requests have estimates:'),
      ).toBeInTheDocument()
      expect(
        screen.getByText('Old Scale, version 1: S (2), M (1)'),
      ).toBeInTheDocument()
      expect(screen.getByText('Their Estimates')).toBeInTheDocument()
    })

    it('clears them when Clear is chosen', async () => {
      // Arrange
      await renderForm({ id: 's-old', name: 'Old Scale' })
      await userEvent.click(screen.getByRole('combobox'))
      await userEvent.click(await screen.findByTitle('T-Shirt Sizes'))
      await userEvent.click(
        screen.getByRole('radio', { name: 'Clear every one' }),
      )

      // Act
      await submit()

      // Assert
      expect(mockChangeEstimationScale).toHaveBeenCalledWith({
        deskId: 'd-1',
        estimationScaleId: 's-1',
        openEstimates: 'Clear',
      })
    })

    it('keeps them unless Clear is chosen', async () => {
      // Arrange
      await renderForm({ id: 's-old', name: 'Old Scale' })
      await userEvent.click(screen.getByRole('combobox'))
      await userEvent.click(await screen.findByTitle('T-Shirt Sizes'))

      // Act
      await submit()

      // Assert
      expect(mockChangeEstimationScale).toHaveBeenCalledWith({
        deskId: 'd-1',
        estimationScaleId: 's-1',
        openEstimates: 'Keep',
      })
    })

    it('asks when the desk would move to a newer version of its scale', async () => {
      // Act
      await renderForm({
        id: 's-1',
        name: 'T-Shirt Sizes',
        isCurrentVersion: false,
      })

      // Assert
      expect(screen.getByText('Their Estimates')).toBeInTheDocument()
    })

    it('does not ask while the desk stays on the version it uses', async () => {
      // Act
      await renderForm({ id: 's-1', name: 'T-Shirt Sizes' })

      // Assert
      expect(screen.queryByText('Their Estimates')).not.toBeInTheDocument()
      expect(screen.getByRole('button', { name: 'Change' })).toBeEnabled()
    })

    it('asks when the desk would stop estimating', async () => {
      // Arrange
      await renderForm({ id: 's-1', name: 'T-Shirt Sizes' })

      // Act
      await userEvent.click(document.querySelector('.ant-select-clear')!)

      // Assert
      expect(screen.getByText('Their Estimates')).toBeInTheDocument()
    })

    it('holds the change until the estimates have loaded', async () => {
      // Arrange
      mockOpenEstimatesState.mockReturnValue({
        data: undefined,
        isFetching: true,
      })
      await renderForm({ id: 's-old', name: 'Old Scale' })

      // Act
      await userEvent.click(screen.getByRole('combobox'))
      await userEvent.click(await screen.findByTitle('T-Shirt Sizes'))

      // Assert
      expect(screen.getByRole('button', { name: 'Change' })).toBeDisabled()
      expect(screen.queryByText('Their Estimates')).not.toBeInTheDocument()
    })

    it('offers a retry when the estimates fail to load', async () => {
      // Arrange
      mockOpenEstimatesState.mockReturnValue({
        data: undefined,
        error: { status: 500 },
      })
      await renderForm({ id: 's-old', name: 'Old Scale' })
      await userEvent.click(screen.getByRole('combobox'))
      await userEvent.click(await screen.findByTitle('T-Shirt Sizes'))

      // Act
      await userEvent.click(screen.getByRole('button', { name: 'Retry' }))

      // Assert
      expect(
        screen.getByText("The open requests' estimates could not be loaded."),
      ).toBeInTheDocument()
      expect(screen.getByRole('button', { name: 'Change' })).toBeDisabled()
      expect(mockRefetchOpenEstimates).toHaveBeenCalled()
    })
  })
})
