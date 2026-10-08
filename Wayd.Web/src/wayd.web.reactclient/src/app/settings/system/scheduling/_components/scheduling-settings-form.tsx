'use client'

import { Button, Form, InputNumber, Select, Skeleton } from 'antd'
import { useEffect, useState } from 'react'
import { useMessage } from '@/src/components/contexts/messaging'
import { toFormErrors } from '@/src/utils'
import {
  IsoDayOfWeek,
  SchedulingSettingsDto,
  TimeZoneDto,
} from '@/src/services/wayd-api'
import { useUpdateSchedulingSettingsMutation } from '@/src/store/features/admin/system-settings-api'
import {
  MAX_COMMITMENT_GRACE_DAYS,
  timeZoneLabel,
  HolidayCalendarSelect,
  WorkingDaysSelect,
  workingDaysRule,
} from '@/src/components/common/scheduling'

interface SchedulingSettingsFormValues {
  defaultTimeZone: string
  defaultCommitmentGraceDays: number
  defaultWorkingDays: IsoDayOfWeek[]
  defaultHolidayCalendarId?: string
}

export interface SchedulingSettingsFormProps {
  settings: SchedulingSettingsDto | undefined
  timeZones: TimeZoneDto[] | undefined
  isLoading: boolean
  canUpdate: boolean
}

const SchedulingSettingsForm = ({
  settings,
  timeZones,
  isLoading,
  canUpdate,
}: SchedulingSettingsFormProps) => {
  const [form] = Form.useForm<SchedulingSettingsFormValues>()
  const [isDirty, setIsDirty] = useState(false)
  const messageApi = useMessage()
  const [updateSchedulingSettings, { isLoading: isSaving }] =
    useUpdateSchedulingSettingsMutation()

  useEffect(() => {
    if (!settings) return
    form.setFieldsValue({
      defaultTimeZone: settings.defaultTimeZone,
      defaultCommitmentGraceDays: settings.defaultCommitmentGraceDays,
      defaultWorkingDays: settings.defaultWorkingDays,
      defaultHolidayCalendarId: settings.defaultHolidayCalendarId,
    })
    setIsDirty(false)
  }, [settings, form])

  const onFinish = async (values: SchedulingSettingsFormValues) => {
    try {
      const response = await updateSchedulingSettings({
        defaultTimeZone: values.defaultTimeZone,
        defaultCommitmentGraceDays: values.defaultCommitmentGraceDays,
        defaultWorkingDays: values.defaultWorkingDays,
        defaultHolidayCalendarId: values.defaultHolidayCalendarId,
      })
      if (response.error) {
        throw response.error
      }
      messageApi.success('Scheduling settings saved.')
      setIsDirty(false)
    } catch (error: any) {
      if (error?.status === 422 && error.errors) {
        form.setFields(toFormErrors(error.errors))
        messageApi.error('Correct the validation error(s) to continue.')
      } else {
        messageApi.error(
          error?.detail ??
            'An error occurred while saving the scheduling settings.',
        )
      }
    }
  }

  if (isLoading) {
    return <Skeleton active paragraph={{ rows: 3 }} />
  }

  const options = (timeZones ?? []).map((zone) => ({
    value: zone.id,
    label: timeZoneLabel(zone),
  }))

  return (
    <Form
      form={form}
      layout="vertical"
      disabled={!canUpdate}
      onFinish={onFinish}
      onValuesChange={() => setIsDirty(true)}
      style={{ maxWidth: 480 }}
    >
      <Form.Item
        name="defaultTimeZone"
        label="Default time zone"
        extra="Pre-fills the time zone of new team operating models. Changing it does not change any team."
        rules={[{ required: true, message: 'Select a time zone.' }]}
      >
        <Select
          showSearch
          options={options}
          optionFilterProp="label"
          placeholder="Select a time zone"
          aria-label="Default time zone"
        />
      </Form.Item>
      <Form.Item
        name="defaultCommitmentGraceDays"
        label="Default commitment grace period (days)"
        extra="How long after a sprint's planned start its commitment is taken when the team does not start it. 1 is the end of the first planned day."
        rules={[{ required: true, message: 'Enter the grace period.' }]}
      >
        <InputNumber
          min={0}
          max={MAX_COMMITMENT_GRACE_DAYS}
          precision={0}
          aria-label="Default commitment grace period (days)"
        />
      </Form.Item>
      <Form.Item
        name="defaultWorkingDays"
        label="Default working days"
        extra="Pre-fills the working days of new team operating models. Changing it does not change any team."
        rules={[workingDaysRule]}
      >
        <WorkingDaysSelect />
      </Form.Item>
      <Form.Item
        name="defaultHolidayCalendarId"
        label="Default holiday calendar"
        extra="The holidays of every team operating model that names no calendar of its own. Unlike the defaults above, changing it changes those teams' holidays, including in past sprints."
      >
        <HolidayCalendarSelect
          emptyLabel="No holidays"
          aria-label="Default holiday calendar"
        />
      </Form.Item>
      {canUpdate && (
        <Form.Item>
          <Button
            type="primary"
            htmlType="submit"
            loading={isSaving}
            disabled={!isDirty}
          >
            Save
          </Button>
        </Form.Item>
      )}
    </Form>
  )
}

export default SchedulingSettingsForm
