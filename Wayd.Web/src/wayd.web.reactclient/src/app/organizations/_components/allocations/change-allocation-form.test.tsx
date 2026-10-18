import { act, fireEvent, render, screen, waitFor } from '@testing-library/react'
import type { FormInstance } from 'antd'
import dayjs from 'dayjs'
import ChangeAllocationForm, {
  changesFor,
  fieldForError,
} from './change-allocation-form'

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

jest.mock('@/src/store/features/organizations/employee-api', () => ({
  useGetEmployeeOptionsQuery: () => ({ data: [] }),
}))

jest.mock('@/src/store/features/organization/employee-allocations-api', () => ({
  useChangeEmployeeAllocationsMutation: () => [jest.fn()],
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
    <ChangeAllocationForm
      employeeId="employee-1"
      requireEnd={requireEnd}
      onFormComplete={() => {}}
      onFormCancel={() => {}}
    />,
  )

const existing = {
  teamId: 'team-1',
  teamName: 'Cards',
  roleId: 'role-1',
  roleName: 'Engineer',
  start: '2027-01-01',
  end: undefined,
  percent: 50,
}

describe('changesFor', () => {
  it('is a single change when allocating afresh', () => {
    const changes = changesFor(undefined, {
      teamId: 'team-2',
      roleId: undefined,
      start: dayjs('2027-02-01'),
      end: dayjs('2027-02-07'),
      percent: 40,
    })

    expect(changes).toEqual([
      {
        teamId: 'team-2',
        roleId: undefined,
        start: '2027-02-01',
        end: '2027-02-07',
        percent: 40,
      },
    ])
  })

  it('clears the original range before setting the new one, so a later start takes effect', () => {
    const changes = changesFor(existing, {
      teamId: 'team-1',
      roleId: 'role-1',
      start: dayjs('2027-03-01'),
      end: null,
      percent: 50,
    })

    expect(changes).toEqual([
      {
        teamId: 'team-1',
        roleId: 'role-1',
        start: '2027-01-01',
        end: undefined,
        percent: 0,
      },
      {
        teamId: 'team-1',
        roleId: 'role-1',
        start: '2027-03-01',
        end: undefined,
        percent: 50,
      },
    ])
  })

  it('clears the original range so an earlier end takes effect', () => {
    const changes = changesFor(
      { ...existing, end: '2027-12-31' },
      {
        teamId: 'team-1',
        roleId: 'role-1',
        start: dayjs('2027-01-01'),
        end: dayjs('2027-06-30'),
        percent: 50,
      },
    )

    expect(changes[0]).toMatchObject({
      start: '2027-01-01',
      end: '2027-12-31',
      percent: 0,
    })
    expect(changes[1]).toMatchObject({
      start: '2027-01-01',
      end: '2027-06-30',
      percent: 50,
    })
  })

  it("keeps the allocation's own team and role whatever the form holds", () => {
    const changes = changesFor(existing, {
      teamId: 'team-9',
      roleId: 'role-9',
      start: dayjs('2027-01-01'),
      end: null,
      percent: 80,
    })

    expect(changes.every((c) => c.teamId === 'team-1')).toBe(true)
    expect(changes.every((c) => c.roleId === 'role-1')).toBe(true)
  })
})

describe('fieldForError', () => {
  it("shows a change's error on the field that set it", () => {
    // Arrange / Act / Assert
    expect(fieldForError('changes[1].percent')).toBe('percent')
    expect(fieldForError('changes[0].end')).toBe('end')
  })

  it('leaves other errors to the default field names', () => {
    // Arrange / Act / Assert
    expect(fieldForError('employeeId')).toBeUndefined()
  })
})

describe('ChangeAllocationForm', () => {
  it('requires an end for someone who is no longer active', async () => {
    // Arrange
    renderForm(true)

    // Act
    const errors = await endErrors({ start: dayjs('2027-02-01'), percent: 60 })

    // Assert
    expect(errors).toEqual([noEndError])
  })

  it('takes time away from someone who is no longer active with no end', async () => {
    // Arrange
    renderForm(true)

    // Act
    const errors = await endErrors({ start: dayjs('2027-02-01'), percent: 0 })

    // Assert
    expect(errors).toEqual([])
  })

  it('checks the end again when the percent rises from zero', async () => {
    // Arrange — antd re-checks a dependent field after a MessageChannel
    // message, which the global stub in jest.setup.ts never delivers
    const stubChannel = global.MessageChannel
    global.MessageChannel = class {
      port1: { onmessage: ((event: unknown) => void) | null } = {
        onmessage: null,
      }
      port2 = {
        postMessage: () => setTimeout(() => this.port1.onmessage?.({})),
      }
    } as unknown as typeof MessageChannel
    try {
      renderForm(true)
      expect(
        await endErrors({ start: dayjs('2027-02-01'), percent: 0 }),
      ).toEqual([])

      // Act
      fireEvent.change(screen.getByRole('spinbutton'), {
        target: { value: '60' },
      })

      // Assert
      await waitFor(() =>
        expect(formInstance!.getFieldError('end')).toEqual([noEndError]),
      )
    } finally {
      global.MessageChannel = stubChannel
    }
  })

  it('accepts no end for someone who is active', async () => {
    // Arrange
    renderForm()

    // Act
    const errors = await endErrors({ start: dayjs('2027-02-01'), percent: 60 })

    // Assert
    expect(errors).toEqual([])
  })

  it('still rejects an end before the start', async () => {
    // Arrange
    renderForm(true)

    // Act
    const errors = await endErrors({
      start: dayjs('2027-02-07'),
      end: dayjs('2027-02-01'),
      percent: 60,
    })

    // Assert
    expect(errors).toEqual(['The end cannot be before the start.'])
  })
})
