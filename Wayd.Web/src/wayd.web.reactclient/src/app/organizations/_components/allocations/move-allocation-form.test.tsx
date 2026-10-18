import { act, render } from '@testing-library/react'
import type { FormInstance } from 'antd'
import dayjs from 'dayjs'
import MoveAllocationForm, { fieldForError } from './move-allocation-form'

jest.unmock('dayjs')

jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: jest.fn(), success: jest.fn() }),
}))

let formInstance: FormInstance | undefined

// The form instance is real — antd's Form binds to it, and a stub breaks on render.
jest.mock('@/src/hooks', () => {
  const { Form } = jest.requireActual('antd')
  return {
    useModalForm: () => {
      const [form] = Form.useForm()
      formInstance = form
      return {
        form,
        isOpen: true,
        isValid: true,
        isSaving: false,
        handleOk: jest.fn(),
        handleCancel: jest.fn(),
      }
    },
  }
})

jest.mock('@/src/store/features/organizations/team-api', () => ({
  useGetTeamOptionsQuery: () => ({ data: [] }),
}))

jest.mock('@/src/store/features/organization/team-member-roles-api', () => ({
  useGetTeamMemberRolesQuery: () => ({ data: [] }),
}))

jest.mock('@/src/store/features/organization/employee-allocations-api', () => ({
  useMoveEmployeeAllocationMutation: () => [jest.fn()],
}))

/** The End field's errors once the given values are validated. */
const endErrors = async (values: Record<string, unknown>) => {
  await act(async () => {
    formInstance!.setFieldsValue(values)
    await formInstance!.validateFields(['end']).catch(() => {})
  })
  return formInstance!.getFieldError('end')
}

const noEndError = 'Enter an end date for someone who is no longer active.'

const renderForm = (requireEnd?: boolean) =>
  render(
    <MoveAllocationForm
      employeeId="employee-1"
      allocations={[]}
      requireEnd={requireEnd}
      onFormComplete={() => {}}
      onFormCancel={() => {}}
    />,
  )

describe('fieldForError', () => {
  it('shows a source team or role error on the From field', () => {
    // Arrange / Act / Assert
    expect(fieldForError('fromTeamId')).toBe('from')
    expect(fieldForError('fromRoleId')).toBe('from')
  })

  it('leaves other errors to the default field names', () => {
    // Arrange / Act / Assert
    expect(fieldForError('toTeamId')).toBeUndefined()
  })
})

describe('MoveAllocationForm', () => {
  it('requires an end for someone who is no longer active', async () => {
    // Arrange
    renderForm(true)

    // Act
    const errors = await endErrors({ start: dayjs('2027-02-01') })

    // Assert
    expect(errors).toEqual([noEndError])
  })

  it('accepts no end for someone who is active', async () => {
    // Arrange
    renderForm()

    // Act
    const errors = await endErrors({ start: dayjs('2027-02-01') })

    // Assert
    expect(errors).toEqual([])
  })
})
