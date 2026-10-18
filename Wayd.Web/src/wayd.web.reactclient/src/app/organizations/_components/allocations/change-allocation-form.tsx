'use client'

import { DatePicker, Form, InputNumber, Modal, Select } from 'antd'
import type { Dayjs } from 'dayjs'
import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import {
  isApiError,
  parseCalendarDate,
  toCalendarDate,
  toFormErrors,
  type ApiError,
  type CalendarDate,
} from '@/src/utils'
import { useGetEmployeeOptionsQuery } from '@/src/store/features/organizations/employee-api'
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

/** The dates a change covers, as the form first shows them. */
export interface AllocationDates {
  start: CalendarDate
  /** Absent for no end. */
  end?: CalendarDate
}

interface Props {
  /** Absent when the person is chosen in the form. */
  employeeId?: string
  /** Shown in place of the picker when the person is fixed and worth naming. */
  employeeName?: string
  /** The team a new allocation is for, when the form does not offer the choice. */
  team?: { id: string; name: string }
  /** Absent when allocating the employee to a team afresh. */
  allocation?: AllocationToChange
  /** The dates the form opens with. Defaults to the allocation's own. */
  initialDates?: AllocationDates
  /** The end date to offer once a person is chosen; undefined for no end. */
  endDateFor?: (employeeId: string) => CalendarDate | undefined
  onFormComplete: () => void
  onFormCancel: () => void
}

interface FormValues {
  employeeId?: string
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
  values: Omit<FormValues, 'employeeId'>,
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

/**
 * Shows the server's errors for a change on the field that set it. Every change
 * in a request comes from the same fields, so the index does not matter.
 */
export const fieldForError = (key: string) =>
  /^changes\[\d+\]\.(\w+)$/.exec(key)?.[1]

const ChangeAllocationForm = ({
  employeeId,
  employeeName,
  team,
  allocation,
  initialDates,
  endDateFor,
  onFormComplete,
  onFormCancel,
}: Props) => {
  const messageApi = useMessage()
  const { data: employeeOptions } = useGetEmployeeOptionsQuery(false, {
    skip: !!employeeId,
  })
  const { data: teamOptions } = useGetTeamOptionsQuery(false, {
    skip: !!allocation || !!team,
  })
  const { data: roles } = useGetTeamMemberRolesQuery(false)
  const [changeAllocations] = useChangeEmployeeAllocationsMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<FormValues>({
      onSubmit: async (values, form) => {
        try {
          const response = await changeAllocations({
            employeeId: employeeId ?? values.employeeId!,
            request: {
              changes: changesFor(allocation, {
                ...values,
                teamId: team?.id ?? values.teamId,
              }),
            },
          })
          if (response.error) throw response.error
          messageApi.success('Allocation saved.')
          return true
        } catch (error) {
          const apiError: ApiError = isApiError(error) ? error : {}
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(
              toFormErrors(apiError.errors, {
                fieldFor: fieldForError,
              }),
            )
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(apiError.detail ?? 'Failed to save the allocation.')
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage: 'Failed to save the allocation.',
      permission: 'Permissions.Teams.Update',
    })

  const roleOptions = roles?.map((r) => ({ label: r.name, value: r.id })) ?? []
  const dates = initialDates ?? allocation

  return (
    <Modal
      title={
        allocation
          ? 'Change Allocation'
          : team
            ? 'Allocate a Person'
            : 'Allocate to a Team'
      }
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
          start: dates ? parseCalendarDate(dates.start) : undefined,
          end: dates?.end ? parseCalendarDate(dates.end) : undefined,
          percent: allocation?.percent,
        }}
      >
        {employeeId ? (
          employeeName && <FormItem label="Person">{employeeName}</FormItem>
        ) : (
          <FormItem
            name="employeeId"
            label="Person"
            rules={[{ required: true }]}
          >
            <Select
              showSearch={{ optionFilterProp: 'label' }}
              placeholder="Select a person"
              options={employeeOptions}
              onChange={(value: string) => {
                if (!endDateFor) return
                const end = endDateFor(value)
                form.setFieldValue('end', end ? parseCalendarDate(end) : null)
              }}
            />
          </FormItem>
        )}
        {allocation ? (
          // Fixed when changing an allocation: Move is how time goes to
          // another team or role.
          <>
            <FormItem label="Team">{allocation.teamName}</FormItem>
            <FormItem label="Role">{allocation.roleName ?? 'No role'}</FormItem>
          </>
        ) : (
          <>
            {team ? (
              <FormItem label="Team">{team.name}</FormItem>
            ) : (
              <FormItem name="teamId" label="Team" rules={[{ required: true }]}>
                <Select
                  showSearch={{ optionFilterProp: 'label' }}
                  placeholder="Select a team or team of teams"
                  options={teamOptions}
                />
              </FormItem>
            )}
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
