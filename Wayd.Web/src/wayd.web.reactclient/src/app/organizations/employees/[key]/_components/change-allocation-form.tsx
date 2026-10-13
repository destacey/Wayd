'use client'

import { DatePicker, Form, InputNumber, Modal, Select } from 'antd'
import type { Dayjs } from 'dayjs'
import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import {
  isApiError,
  parseCalendarDate,
  toCalendarDate,
  type ApiError,
  type CalendarDate,
} from '@/src/utils'
import { useGetTeamOptionsQuery } from '@/src/store/features/organizations/team-api'
import { useGetTeamMemberRolesQuery } from '@/src/store/features/organization/team-member-roles-api'
import { useChangeEmployeeAllocationsMutation } from '@/src/store/features/organization/employee-allocations-api'

const { Item: FormItem } = Form

/** The allocation a change starts from, when changing one that exists. */
export interface AllocationToChange {
  teamId: string
  teamName: string
  roleId?: string
  roleName?: string
  start: CalendarDate
  end?: CalendarDate
  percent: number
}

interface Props {
  employeeId: string
  /** Absent when allocating the employee to a team afresh. */
  allocation?: AllocationToChange
  onFormComplete: () => void
  onFormCancel: () => void
}

interface FormValues {
  teamId: string
  roleId?: string
  start: Dayjs
  end?: Dayjs | null
  percent: number
}

/**
 * The changes that turn `allocation` into the values entered. The API sets a
 * percent over a date range and leaves other days alone, so moving the start
 * later or the end earlier would otherwise leave the days dropped as they
 * were: the original range is cleared first, in the same request.
 */
export const changesFor = (
  allocation: AllocationToChange | undefined,
  values: FormValues,
) => {
  const entered = {
    teamId: allocation?.teamId ?? values.teamId,
    roleId: allocation ? allocation.roleId : values.roleId,
    start: toCalendarDate(values.start),
    end: values.end ? toCalendarDate(values.end) : undefined,
    percent: values.percent,
  }

  return allocation
    ? [
        {
          teamId: allocation.teamId,
          roleId: allocation.roleId,
          start: allocation.start,
          end: allocation.end,
          percent: 0,
        },
        entered,
      ]
    : [entered]
}

const ChangeAllocationForm = ({
  employeeId,
  allocation,
  onFormComplete,
  onFormCancel,
}: Props) => {
  const messageApi = useMessage()
  const { data: teamOptions } = useGetTeamOptionsQuery(false)
  const { data: roles } = useGetTeamMemberRolesQuery(false)
  const [changeAllocations] = useChangeEmployeeAllocationsMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<FormValues>({
      onSubmit: async (values) => {
        try {
          const response = await changeAllocations({
            employeeId,
            request: { changes: changesFor(allocation, values) },
          })
          if (response.error) throw response.error
          messageApi.success('Allocation saved.')
          return true
        } catch (error) {
          const apiError: ApiError = isApiError(error) ? error : {}
          messageApi.error(apiError.detail ?? 'Failed to save the allocation.')
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage: 'Failed to save the allocation.',
      permission: 'Permissions.Teams.Update',
    })

  const roleOptions = roles?.map((r) => ({ label: r.name, value: r.id })) ?? []

  return (
    <Modal
      title={allocation ? 'Change Allocation' : 'Allocate to a Team'}
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Save"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="change-allocation-form"
        initialValues={{
          teamId: allocation?.teamId,
          roleId: allocation?.roleId,
          start: allocation ? parseCalendarDate(allocation.start) : undefined,
          end: allocation?.end ? parseCalendarDate(allocation.end) : undefined,
          percent: allocation?.percent,
        }}
      >
        {allocation ? (
          // Fixed when changing an allocation: Move is how time goes to
          // another team or role.
          <>
            <FormItem label="Team">{allocation.teamName}</FormItem>
            <FormItem label="Role">{allocation.roleName ?? 'No role'}</FormItem>
          </>
        ) : (
          <>
            <FormItem name="teamId" label="Team" rules={[{ required: true }]}>
              <Select
                showSearch={{ optionFilterProp: 'label' }}
                placeholder="Select a team or team of teams"
                options={teamOptions}
              />
            </FormItem>
            <FormItem
              name="roleId"
              label="Role"
              extra="What the time is spent on. It need not match a role the person holds as a member."
            >
              <Select
                allowClear
                showSearch={{ optionFilterProp: 'label' }}
                placeholder="No role"
                options={roleOptions}
              />
            </FormItem>
          </>
        )}
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
          label="Percent of their time"
          extra={
            allocation
              ? 'Enter 0 to remove this allocation.'
              : 'Replaces what this team and role have on these dates.'
          }
          rules={[{ required: true }]}
        >
          <InputNumber min={0} max={100} precision={0} suffix="%" />
        </FormItem>
      </Form>
    </Modal>
  )
}

export default ChangeAllocationForm
