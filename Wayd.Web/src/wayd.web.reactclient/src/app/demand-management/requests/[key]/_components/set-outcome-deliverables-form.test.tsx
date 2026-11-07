import React from 'react'
import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import {
  IntakeRequestOutcomeDto,
  IntakeRequestOutcomeKind,
  ProjectDeliverableStatusAlias,
  StatusCategory,
} from '@/src/services/wayd-api'
import SetOutcomeDeliverablesForm from './set-outcome-deliverables-form'

const mockError = jest.fn()
jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: mockError, success: jest.fn() }),
}))

// As in the other form suites: keep the form real and submit through validateFields.
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
          if (await onSubmit(await form.validateFields(), form)) onComplete()
        },
        handleCancel: jest.fn(),
      }
    },
  }
})

const mockSet = jest.fn()
jest.mock(
  '@/src/store/features/demand-management/demand-management-api',
  () => ({
    useSetIntakeRequestOutcomeDeliverablesMutation: () => [mockSet],
    useGetIntakeRequestProjectDeliverablesQuery: () => ({
      data: [
        {
          id: 'd-2',
          name: 'Reports',
          statusCategory: StatusCategory.Proposed,
          statusAlias: ProjectDeliverableStatusAlias.NotStarted,
        },
      ],
      isFetching: false,
      isError: false,
    }),
  }),
)

const outcome = {
  kind: IntakeRequestOutcomeKind.ExistingProjectScope,
  targetId: 'p-1',
  project: { id: 'p-1', key: 'ATLAS', name: 'Atlas' },
  // Deleted from the project since it was mapped, so the picker no longer offers it.
  deliverables: [{ id: 'd-gone', isOnProject: false }],
} as unknown as IntakeRequestOutcomeDto

const renderForm = (onFormComplete = jest.fn()) =>
  act(async () =>
    render(
      <SetOutcomeDeliverablesForm
        requestId="r-42"
        outcome={outcome}
        onFormComplete={onFormComplete}
        onFormCancel={jest.fn()}
      />,
    ),
  )

describe('SetOutcomeDeliverablesForm', () => {
  beforeEach(() => {
    mockSet.mockReset()
    mockError.mockReset()
  })

  it('sends the outcome whole mapping with the mapping it opened on, keeping a deleted deliverable', async () => {
    // Arrange
    mockSet.mockReturnValue({ unwrap: () => Promise.resolve(null) })
    const onFormComplete = jest.fn()
    await renderForm(onFormComplete)
    expect(screen.getByText('Deleted from the project')).toBeInTheDocument()
    const picker = screen
      .getByText('Deliverables')
      .closest('.ant-form-item')!
      .querySelector('input')!
    await act(() => userEvent.click(picker))
    await act(() => userEvent.click(screen.getByTitle('Reports · Not Started')))

    // Act
    await act(() =>
      userEvent.click(screen.getByRole('button', { name: 'Save' })),
    )

    // Assert
    expect(mockSet).toHaveBeenCalledWith({
      requestId: 'r-42',
      targetId: 'p-1',
      deliverableIds: ['d-gone', 'd-2'],
      expectedDeliverableIds: ['d-gone'],
    })
    expect(onFormComplete).toHaveBeenCalled()
  })

  it('closes and says why when another change landed first', async () => {
    // Arrange
    mockSet.mockReturnValue({
      unwrap: () =>
        Promise.reject({ status: 409, detail: 'The deliverables changed.' }),
    })
    const onFormComplete = jest.fn()
    await renderForm(onFormComplete)

    // Act
    await act(() =>
      userEvent.click(screen.getByRole('button', { name: 'Save' })),
    )

    // Assert
    expect(mockError).toHaveBeenCalledWith('The deliverables changed.')
    expect(onFormComplete).toHaveBeenCalled()
  })
})
