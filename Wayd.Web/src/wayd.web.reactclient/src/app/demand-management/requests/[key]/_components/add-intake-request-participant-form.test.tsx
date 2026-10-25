import React from 'react'
import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { IntakeRequestDetailsDto } from '@/src/services/wayd-api'
import AddIntakeRequestParticipantForm from './add-intake-request-participant-form'

jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: jest.fn(), success: jest.fn() }),
}))

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
          if (await onSubmit(await form.validateFields(), form)) onComplete()
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
    useAddIntakeRequestParticipantMutation: () => [mockAdd],
    useGetIntakeEmployeeOptionsQuery: () => ({
      data: [
        { id: 'e-1', key: 1, name: 'Avery Requester' },
        { id: 'e-2', key: 2, name: 'Blake Submitter' },
        { id: 'e-3', key: 3, name: 'Casey Participant' },
        { id: 'e-4', key: 4, name: 'Drew Newcomer' },
      ],
    }),
  }),
)

const request = {
  summary: {
    id: 'r-42',
    key: 42,
    requester: { id: 'e-1', key: 1, name: 'Avery Requester' },
  },
  substance: {
    submittedBy: { id: 'e-2', key: 2, name: 'Blake Submitter' },
    participants: [
      {
        employee: { id: 'e-3', key: 3, name: 'Casey Participant' },
        addedOn: new Date(),
      },
    ],
  },
  canAddParticipants: true,
} as unknown as IntakeRequestDetailsDto

const openPicker = async () => {
  const select = screen
    .getByText('Person')
    .closest('.ant-form-item')!
    .querySelector('input')!
  await act(() => userEvent.click(select))
}

describe('AddIntakeRequestParticipantForm', () => {
  beforeEach(() => {
    mockAdd.mockReset()
  })

  it('offers only people who are not already on the request', async () => {
    // Arrange
    await act(async () =>
      render(
        <AddIntakeRequestParticipantForm
          request={request}
          onFormComplete={jest.fn()}
          onFormCancel={jest.fn()}
        />,
      ),
    )

    // Act
    await openPicker()

    // Assert
    expect(screen.getByTitle('Drew Newcomer')).toBeInTheDocument()
    expect(screen.queryByTitle('Avery Requester')).not.toBeInTheDocument()
    expect(screen.queryByTitle('Blake Submitter')).not.toBeInTheDocument()
    expect(screen.queryByTitle('Casey Participant')).not.toBeInTheDocument()
  })

  it('adds the chosen person to the request', async () => {
    // Arrange
    mockAdd.mockReturnValue({ unwrap: () => Promise.resolve(null) })
    const onFormComplete = jest.fn()
    await act(async () =>
      render(
        <AddIntakeRequestParticipantForm
          request={request}
          onFormComplete={onFormComplete}
          onFormCancel={jest.fn()}
        />,
      ),
    )
    await openPicker()
    await act(() => userEvent.click(screen.getByTitle('Drew Newcomer')))

    // Act
    await act(() =>
      userEvent.click(screen.getByRole('button', { name: 'Add' })),
    )

    // Assert
    expect(mockAdd).toHaveBeenCalledWith({
      requestId: 'r-42',
      employeeId: 'e-4',
    })
    expect(onFormComplete).toHaveBeenCalled()
  })
})
