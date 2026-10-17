import { act, render, screen } from '@testing-library/react'
import EmployeeWorkingDayForm, {
  workingDayLabel,
} from './employee-working-day-form'

const changeWorkingDayHours = jest.fn()

jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: jest.fn(), success: jest.fn() }),
}))

jest.mock('@/src/store/features/organization/employee-time-away-api', () => ({
  useChangeEmployeeWorkingDayHoursMutation: () => [changeWorkingDayHours],
}))

/** The submit handler from the live form, so a test need not click a jsdom-disabled button. */
let submitForm: (() => Promise<void>) | undefined
let formInstance:
  | {
      setFieldsValue: (values: Record<string, unknown>) => void
      getFieldValue: (name: string) => unknown
    }
  | undefined

// The form instance is real — antd's Form binds to it, and a stub breaks on render.
jest.mock('@/src/hooks', () => {
  const { Form } = jest.requireActual('antd')
  return {
    useModalForm: ({
      onSubmit,
    }: {
      onSubmit: (values: Record<string, unknown>) => Promise<boolean>
    }) => {
      const [form] = Form.useForm()
      formInstance = form
      const handleOk = async () => {
        await onSubmit(form.getFieldsValue(true))
      }
      submitForm = handleOk
      return {
        form,
        isOpen: true,
        isValid: true,
        isSaving: false,
        handleOk,
        handleCancel: jest.fn(),
      }
    },
  }
})

const renderForm = (ownWorkingDayHours?: number) =>
  render(
    <EmployeeWorkingDayForm
      employeeId="employee-1"
      ownWorkingDayHours={ownWorkingDayHours}
      systemWorkingDayHours={8}
      onFormComplete={() => {}}
      onFormCancel={() => {}}
    />,
  )

const submit = async () => {
  await act(async () => {
    await submitForm!()
  })
}

beforeEach(() => {
  jest.clearAllMocks()
  changeWorkingDayHours.mockResolvedValue({ data: undefined })
})

describe('EmployeeWorkingDayForm', () => {
  it("starts on the person's own working day", () => {
    // Act
    renderForm(4)

    // Assert
    expect(formInstance!.getFieldValue('workingDayHours')).toBe(4)
  })

  it('saves the hours entered', async () => {
    // Arrange
    renderForm()
    act(() => formInstance!.setFieldsValue({ workingDayHours: 6.5 }))

    // Act
    await submit()

    // Assert
    expect(changeWorkingDayHours).toHaveBeenCalledWith({
      employeeId: 'employee-1',
      request: { workingDayHours: 6.5 },
    })
  })

  it("sends no hours when cleared, giving the person the system's working day", async () => {
    // Arrange
    renderForm(4)
    act(() => formInstance!.setFieldsValue({ workingDayHours: null }))

    // Act
    await submit()

    // Assert
    expect(changeWorkingDayHours).toHaveBeenCalledWith({
      employeeId: 'employee-1',
      request: { workingDayHours: undefined },
    })
  })

  it('takes from 1 to 24 hours', () => {
    // Act
    renderForm()

    // Assert
    const input = screen.getByRole('spinbutton', { name: 'Working day hours' })
    expect(input).toHaveAttribute('aria-valuemin', '1')
    expect(input).toHaveAttribute('aria-valuemax', '24')
  })
})

describe('workingDayLabel', () => {
  it("reads the person's own hours, or the system default's", () => {
    // Act
    const own = workingDayLabel(4.5, 8)
    const system = workingDayLabel(undefined, 7.5)

    // Assert
    expect(own).toBe('4.5 h')
    expect(system).toBe('System default (7.5 h)')
  })
})
