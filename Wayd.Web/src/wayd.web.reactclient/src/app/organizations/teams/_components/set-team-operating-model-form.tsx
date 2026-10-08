'use client'

import { DatePicker, Form, InputNumber, Modal, Radio } from 'antd'
import { useEffect, useState } from 'react'
import {
  IsoDayOfWeek,
  Methodology,
  SetTeamOperatingModelRequest,
  SizingMethod,
} from '@/src/services/wayd-api'
import {
  toFormErrors,
  isApiError,
  sizingMethodOptions,
  type ApiError,
} from '@/src/utils'
import {
  useGetOperatingModelDefaultsQuery,
  useSetTeamOperatingModelMutation,
} from '@/src/store/features/organizations/team-api'
import {
  MAX_COMMITMENT_GRACE_DAYS,
  TimeZoneSelect,
  HolidayCalendarSelect,
  WorkingDaysSelect,
  workingDaysRule,
} from '@/src/components/common/scheduling'
import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { type Dayjs } from 'dayjs'

const { Item: FormItem } = Form
const { Group: RadioGroup } = Radio

export interface SetTeamOperatingModelFormProps {
  teamId: string
  onFormComplete: () => void
  onFormCancel: () => void
}

interface SetTeamOperatingModelFormValues {
  startDate: Dayjs
  methodology: Methodology
  sizingMethod: SizingMethod
  timeZone: string
  commitmentGraceDays: number
  workingDays: IsoDayOfWeek[]
  holidayCalendarId?: string
}

const methodologyOptions = [
  { value: Methodology.Scrum, label: 'Scrum' },
  { value: Methodology.Kanban, label: 'Kanban' },
]

const mapToRequestValues = (
  values: SetTeamOperatingModelFormValues,
): SetTeamOperatingModelRequest => {
  return {
    startDate: values.startDate?.format('YYYY-MM-DD'),
    methodology: values.methodology,
    sizingMethod: values.sizingMethod,
    timeZone: values.timeZone,
    commitmentGraceDays: values.commitmentGraceDays,
    workingDays: values.workingDays,
    holidayCalendarId: values.holidayCalendarId,
  } as SetTeamOperatingModelRequest
}

const SetTeamOperatingModelForm = ({
  teamId,
  onFormComplete,
  onFormCancel,
}: SetTeamOperatingModelFormProps) => {
  const messageApi = useMessage()

  const [setOperatingModel] = useSetTeamOperatingModelMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<SetTeamOperatingModelFormValues>({
      onSubmit: async (values: SetTeamOperatingModelFormValues, form) => {
        try {
          const request = mapToRequestValues(values)
          await setOperatingModel({ teamId, request }).unwrap()
          messageApi.success('Successfully set operating model.')
          return true
        } catch (error) {
          const apiError: ApiError = isApiError(error) ? error : {}
          if (apiError.status === 422 && apiError.errors) {
            const formErrors = toFormErrors(apiError.errors)
            form.setFields(formErrors)
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ??
                'An unexpected error occurred while setting the operating model.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An unexpected error occurred while setting the operating model.',
      permission: 'Permissions.Teams.Update',
    })

  // The suggested zone is the parent's on the start date, so it follows the date.
  const startDate = Form.useWatch('startDate', form)
  const { data: defaults } = useGetOperatingModelDefaultsQuery(
    {
      teamId,
      teamType: 'Team',
      startDate: startDate?.format('YYYY-MM-DD') ?? '',
    },
    { skip: !startDate },
  )

  // Pre-fill only what the user has not chosen themselves. The form's touched
  // flag cannot tell: setting a value programmatically marks it touched too,
  // which would stop the suggestion following later start dates.
  const [userChosen, setUserChosen] = useState<
    Partial<Record<keyof SetTeamOperatingModelFormValues, boolean>>
  >({})
  useEffect(() => {
    if (!defaults) return
    if (!userChosen.timeZone) {
      form.setFieldsValue({ timeZone: defaults.timeZone })
    }
    if (!userChosen.commitmentGraceDays) {
      form.setFieldsValue({
        commitmentGraceDays: defaults.commitmentGraceDays,
      })
    }
    if (!userChosen.workingDays) {
      form.setFieldsValue({ workingDays: defaults.workingDays })
    }
  }, [defaults, form, userChosen])

  return (
    <Modal
      title="Set Operating Model"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Save"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false} // disable esc key to close modal
      destroyOnHidden
    >
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="set-team-operating-model-form"
        onValuesChange={(changed) =>
          setUserChosen((chosen) => ({
            ...chosen,
            ...Object.fromEntries(Object.keys(changed).map((k) => [k, true])),
          }))
        }
      >
        <FormItem
          name="startDate"
          label="Start Date"
          rules={[{ required: true, message: 'Start date is required' }]}
        >
          <DatePicker style={{ width: '100%' }} />
        </FormItem>
        <FormItem
          name="methodology"
          label="Methodology"
          rules={[{ required: true, message: 'Methodology is required' }]}
        >
          <RadioGroup
            options={methodologyOptions}
            optionType="button"
            buttonStyle="solid"
          />
        </FormItem>
        <FormItem
          name="sizingMethod"
          label="Sizing Method"
          rules={[{ required: true, message: 'Sizing method is required' }]}
        >
          <RadioGroup
            options={sizingMethodOptions}
            optionType="button"
            buttonStyle="solid"
          />
        </FormItem>
        <FormItem
          name="timeZone"
          label="Time Zone"
          extra={
            <>
              The zone the team&apos;s sprint days are counted in. Sprints
              planned before this model&apos;s start keep the previous
              model&apos;s zone.
              {defaults?.timeZoneSource && (
                <> Suggested from {defaults.timeZoneSource}.</>
              )}
            </>
          }
          rules={[{ required: true, message: 'Time zone is required' }]}
        >
          <TimeZoneSelect aria-label="Time Zone" />
        </FormItem>
        <FormItem
          name="commitmentGraceDays"
          label="Commitment Grace Period (days)"
          extra="How long after a sprint's planned start its commitment is taken when the team does not start it. 1 is the end of the first planned day."
          rules={[
            { required: true, message: 'Commitment grace period is required' },
          ]}
        >
          <InputNumber
            min={0}
            max={MAX_COMMITMENT_GRACE_DAYS}
            precision={0}
            aria-label="Commitment Grace Period (days)"
          />
        </FormItem>
        <FormItem
          name="workingDays"
          label="Working Days"
          extra="The days of the week the team works. A sprint's ideal burn-down stays flat on the other days."
          rules={[workingDaysRule]}
        >
          <WorkingDaysSelect />
        </FormItem>
        <FormItem
          name="holidayCalendarId"
          label="Holiday Calendar"
          extra="The days the whole team is off. Leave empty to use the system default calendar, which follows the scheduling settings."
        >
          <HolidayCalendarSelect
            emptyLabel="System default"
            aria-label="Holiday Calendar"
          />
        </FormItem>
      </Form>
    </Modal>
  )
}

export default SetTeamOperatingModelForm
