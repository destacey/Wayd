'use client'

import { Button, Form, InputNumber, Skeleton } from 'antd'
import { useEffect, useState } from 'react'
import { useMessage } from '@/src/components/contexts/messaging'
import { toApiError, toFormErrors } from '@/src/utils'
import { IsoDayOfWeek, SchedulingSettingsDto } from '@/src/services/wayd-api'
import { useUpdateSchedulingSettingsMutation } from '@/src/store/features/admin/system-settings-api'
import {
  MAX_COMMITMENT_GRACE_DAYS,
  HolidayCalendarSelect,
  WorkingDaysSelect,
  workingDaysRule,
} from '@/src/components/common/scheduling'

interface SchedulingSettingsFormValues {
  defaultCommitmentGraceDays: number
  defaultWorkingDays: IsoDayOfWeek[]
  defaultHolidayCalendarId?: string
  workingDayHours: number
}

export interface SchedulingSettingsFormProps {
  settings: SchedulingSettingsDto | undefined
  isLoading: boolean
  canUpdate: boolean
}

const SchedulingSettingsForm = ({
  settings,
  isLoading,
  canUpdate,
}: SchedulingSettingsFormProps) => {
  const [form] = Form.useForm<SchedulingSettingsFormValues>()
  const [isDirty, setIsDirty] = useState(false)
  const messageApi = useMessage()
  const [updateSchedulingSettings, { isLoading: isSaving }] =
    useUpdateSchedulingSettingsMutation()

  // A fresh copy of the settings replaces the form's values, so nothing is
  // left unsaved.
  const [loadedSettings, setLoadedSettings] = useState(settings)
  if (settings !== loadedSettings) {
    setLoadedSettings(settings)
    if (settings) setIsDirty(false)
  }

  useEffect(() => {
    if (!settings) return
    form.setFieldsValue({
      defaultCommitmentGraceDays: settings.defaultCommitmentGraceDays,
      defaultWorkingDays: settings.defaultWorkingDays,
      defaultHolidayCalendarId: settings.defaultHolidayCalendar?.id,
      workingDayHours: settings.workingDayHours,
    })
  }, [settings, form])

  const onFinish = async (values: SchedulingSettingsFormValues) => {
    try {
      const response = await updateSchedulingSettings({
        defaultCommitmentGraceDays: values.defaultCommitmentGraceDays,
        defaultWorkingDays: values.defaultWorkingDays,
        defaultHolidayCalendarId: values.defaultHolidayCalendarId,
        workingDayHours: values.workingDayHours,
      })
      if (response.error) {
        throw response.error
      }
      messageApi.success('Scheduling settings saved.')
      setIsDirty(false)
    } catch (error) {
      const apiError = toApiError(error)
      if (apiError.status === 422 && apiError.errors) {
        form.setFields(toFormErrors(apiError.errors))
        messageApi.error('Correct the validation error(s) to continue.')
      } else {
        messageApi.error(
          apiError.detail ??
            'An error occurred while saving the scheduling settings.',
        )
      }
    }
  }

  if (isLoading) {
    return <Skeleton active paragraph={{ rows: 3 }} />
  }

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
          current={settings?.defaultHolidayCalendar}
          aria-label="Default holiday calendar"
        />
      </Form.Item>
      <Form.Item
        name="workingDayHours"
        label="Working day hours"
        extra="How long a working day is: a person-day of capacity, and the day of everyone without one of their own. Capacity can be read in hours, and time away and team days off entered in hours are a share of a day: 2 hours of an 8-hour day is a quarter of it. The same for every team."
        rules={[{ required: true, message: 'Enter the working day hours.' }]}
      >
        <InputNumber
          min={1}
          max={24}
          step={0.5}
          precision={2}
          suffix="h"
          aria-label="Working day hours"
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
