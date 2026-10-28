import React from 'react'
import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import {
  IntakeRequestDetailsDto,
  IntakeRequestOutcomeKind,
} from '@/src/services/wayd-api'
import AddIntakeRequestOutcomeForm from './add-intake-request-outcome-form'

jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: jest.fn(), success: jest.fn() }),
}))

// What validateFields rejected with: jsdom does not render a field's error text after a failed submit.
const mockValidationFailures: unknown[] = []

// In Jest, Form.useWatch does not refire after typing, so the real hook's validity never updates; as the
// other form suites do, keep the form instance real and submit through validateFields.
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
          try {
            if (await onSubmit(await form.validateFields(), form)) onComplete()
          } catch (error) {
            // A validation failure leaves the form open, as the real hook does.
            mockValidationFailures.push(error)
          }
        },
        handleCancel: jest.fn(),
      }
    },
  }
})

const mockAdd = jest.fn()
jest.mock(
  '@/src/store/features/demand-management/demand-management-api',
  () => ({
    useAddIntakeRequestOutcomeMutation: () => [mockAdd],
  }),
)

jest.mock('@/src/store/features/ppm/projects-api', () => ({
  useGetProjectOptionsQuery: () => ({
    data: [
      { label: 'Atlas (ATLAS)', value: 'p-1' },
      { label: 'Borealis (BOREAL)', value: 'p-2' },
    ],
    isLoading: false,
  }),
}))

const request = {
  summary: { id: 'r-42', key: 42 },
  outcomes: [{ targetId: 'p-1', kind: IntakeRequestOutcomeKind.NewProject }],
  canChangeOutcomes: true,
} as unknown as IntakeRequestDetailsDto

const renderForm = (onFormComplete = jest.fn()) =>
  act(async () =>
    render(
      <AddIntakeRequestOutcomeForm
        request={request}
        onFormComplete={onFormComplete}
        onFormCancel={jest.fn()}
      />,
    ),
  )

const openProjectPicker = async () => {
  const select = screen
    .getByText('Project')
    .closest('.ant-form-item')!
    .querySelector('input')!
  await act(() => userEvent.click(select))
}

describe('AddIntakeRequestOutcomeForm', () => {
  beforeEach(() => {
    mockAdd.mockReset()
    mockValidationFailures.length = 0
  })

  it('offers only projects the request does not already point at', async () => {
    // Arrange
    await renderForm()

    // Act
    await openProjectPicker()

    // Assert
    expect(screen.getByTitle('Borealis (BOREAL)')).toBeInTheDocument()
    expect(screen.queryByTitle('Atlas (ATLAS)')).not.toBeInTheDocument()
  })

  it('records a new project with no note', async () => {
    // Arrange
    mockAdd.mockReturnValue({ unwrap: () => Promise.resolve(null) })
    const onFormComplete = jest.fn()
    await renderForm(onFormComplete)
    await openProjectPicker()
    await act(() => userEvent.click(screen.getByTitle('Borealis (BOREAL)')))

    // Act
    await act(() =>
      userEvent.click(screen.getByRole('button', { name: 'Add' })),
    )

    // Assert
    expect(mockAdd).toHaveBeenCalledWith({
      requestId: 'r-42',
      request: {
        kind: IntakeRequestOutcomeKind.NewProject,
        projectId: 'p-2',
        note: undefined,
      },
    })
    expect(onFormComplete).toHaveBeenCalled()
  })

  it('refuses existing project scope without saying what was absorbed', async () => {
    // Arrange
    await renderForm()
    await act(() =>
      userEvent.click(
        screen.getByLabelText('Scope added to an existing project'),
      ),
    )
    await openProjectPicker()
    await act(() => userEvent.click(screen.getByTitle('Borealis (BOREAL)')))

    // Act
    await act(() =>
      userEvent.click(screen.getByRole('button', { name: 'Add' })),
    )

    // Assert
    expect(mockAdd).not.toHaveBeenCalled()
    expect(mockValidationFailures).toContainEqual(
      expect.objectContaining({
        errorFields: [
          expect.objectContaining({
            name: ['note'],
            errors: ['Say what the project absorbed from this request.'],
          }),
        ],
      }),
    )
  })
})
