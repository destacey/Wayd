'use client'

import { DatePicker, Form, InputNumber, Modal, Select } from 'antd'
import type { Dayjs } from 'dayjs'
import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { caseInsensitiveCompare } from '@/src/components/common/wayd-grid'
import {
  isApiError,
  parseCalendarDate,
  toCalendarDate,
  type ApiError,
  type CalendarDate,
} from '@/src/utils'
import { useGetTeamOptionsQuery } from '@/src/store/features/organizations/team-api'
import { useGetTeamMemberRolesQuery } from '@/src/store/features/organization/team-member-roles-api'
import {
  EmployeeAllocationDto,
  useMoveEmployeeAllocationMutation,
} from '@/src/store/features/organization/employee-allocations-api'

const { Item: FormItem } = Form

interface Props {
  employeeId: string
  /** The employee's allocations, which the time can be moved from. */
  allocations: EmployeeAllocationDto[]
  /** Where the time is going and when, when the caller already knows. */
  target?: {
    teamId: string
    roleId?: string
    start: CalendarDate
    /** Absent for no end. */
    end?: CalendarDate
  }
  onFormComplete: () => void
  onFormCancel: () => void
}

interface FormValues {
  /** Index into the distinct team-and-role sources. */
  from: number
  toTeamId: string
  toRoleId?: string
  start: Dayjs
  end?: Dayjs | null
  percent: number
}

interface Source {
  teamId: string
  roleId?: string
  label: string
}

/** Each team and role the employee has time on that the caller may change, once. */
const sourcesOf = (allocations: EmployeeAllocationDto[]): Source[] => {
  const sources = new Map<string, Source>()
  for (const allocation of allocations) {
    if (!allocation.canManage) continue

    const key = `${allocation.team.id}|${allocation.role?.id ?? ''}`
    if (!sources.has(key)) {
      sources.set(key, {
        teamId: allocation.team.id,
        roleId: allocation.role?.id,
        label: allocation.role
          ? `${allocation.team.name} (${allocation.role.name})`
          : allocation.team.name,
      })
    }
  }

  return [...sources.values()].sort((a, b) =>
    caseInsensitiveCompare(a.label, b.label),
  )
}

const MoveAllocationForm = ({
  employeeId,
  allocations,
  target,
  onFormComplete,
  onFormCancel,
}: Props) => {
  const messageApi = useMessage()
  const { data: teamOptions } = useGetTeamOptionsQuery(false)
  const { data: roles } = useGetTeamMemberRolesQuery(false)
  const [moveAllocation] = useMoveEmployeeAllocationMutation()

  const sources = sourcesOf(allocations)

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<FormValues>({
      onSubmit: async (values) => {
        try {
          const source = sources[values.from]
          const response = await moveAllocation({
            employeeId,
            request: {
              fromTeamId: source.teamId,
              fromRoleId: source.roleId,
              toTeamId: values.toTeamId,
              toRoleId: values.toRoleId,
              start: toCalendarDate(values.start),
              end: values.end ? toCalendarDate(values.end) : undefined,
              percent: values.percent,
            },
          })
          if (response.error) throw response.error
          messageApi.success('Allocation moved.')
          return true
        } catch (error) {
          const apiError: ApiError = isApiError(error) ? error : {}
          messageApi.error(apiError.detail ?? 'Failed to move the allocation.')
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage: 'Failed to move the allocation.',
      permission: 'Permissions.Teams.Update',
    })

  const roleOptions = roles?.map((r) => ({ label: r.name, value: r.id })) ?? []

  return (
    <Modal
      title="Move Allocation"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Move"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="move-allocation-form"
        initialValues={
          target && {
            toTeamId: target.teamId,
            toRoleId: target.roleId,
            start: parseCalendarDate(target.start),
            end: target.end ? parseCalendarDate(target.end) : undefined,
          }
        }
      >
        <FormItem name="from" label="From" rules={[{ required: true }]}>
          <Select
            placeholder="Select where the time comes from"
            options={sources.map((source, index) => ({
              label: source.label,
              value: index,
            }))}
          />
        </FormItem>
        <FormItem name="toTeamId" label="To team" rules={[{ required: true }]}>
          <Select
            showSearch={{ optionFilterProp: 'label' }}
            placeholder="Select a team or team of teams"
            options={teamOptions}
          />
        </FormItem>
        <FormItem name="toRoleId" label="To role">
          <Select
            allowClear
            showSearch={{ optionFilterProp: 'label' }}
            placeholder="No role"
            options={roleOptions}
          />
        </FormItem>
        <FormItem name="start" label="Start" rules={[{ required: true }]}>
          <DatePicker />
        </FormItem>
        <FormItem
          name="end"
          label="End"
          dependencies={['start']}
          extra="Leave empty for no end."
          rules={[
            ({ getFieldValue }) => ({
              validator: (_, value: Dayjs | null | undefined) => {
                const start: Dayjs | undefined = getFieldValue('start')
                return !value || !start || !value.isBefore(start, 'day')
                  ? Promise.resolve()
                  : Promise.reject(
                      new Error('The end cannot be before the start.'),
                    )
              },
            }),
          ]}
        >
          <DatePicker />
        </FormItem>
        <FormItem
          name="percent"
          label="Percent of their time to move"
          extra="Taken from the source and added to the target on every day of the range."
          rules={[{ required: true }]}
        >
          <InputNumber min={1} max={100} precision={0} suffix="%" />
        </FormItem>
      </Form>
    </Modal>
  )
}

export default MoveAllocationForm
