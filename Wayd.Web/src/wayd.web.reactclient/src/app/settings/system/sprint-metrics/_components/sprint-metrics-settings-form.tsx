'use client'

import { Button, Form, InputNumber, Skeleton } from 'antd'
import { useEffect, useState } from 'react'
import { useMessage } from '@/src/components/contexts/messaging'
import { toFormErrors } from '@/src/utils'
import { SprintMetricsSettingsDto } from '@/src/services/wayd-api'
import { useUpdateSprintMetricsSettingsMutation } from '@/src/store/features/admin/system-settings-api'
import {
  MAX_SPRINT_WINDOW,
  MIN_SPRINT_WINDOW,
} from '@/src/components/common/work/sprint-metrics/team-sprint-metrics-formatting'

interface SprintMetricsSettingsFormValues {
  sprintWindow: number
}

export interface SprintMetricsSettingsFormProps {
  settings: SprintMetricsSettingsDto | undefined
  isLoading: boolean
  canUpdate: boolean
}

const SprintMetricsSettingsForm = ({
  settings,
  isLoading,
  canUpdate,
}: SprintMetricsSettingsFormProps) => {
  const [form] = Form.useForm<SprintMetricsSettingsFormValues>()
  const [isDirty, setIsDirty] = useState(false)
  const messageApi = useMessage()
  const [updateSprintMetricsSettings, { isLoading: isSaving }] =
    useUpdateSprintMetricsSettingsMutation()

  // A fresh copy of the settings replaces the form's values, so nothing is
  // left unsaved.
  const [loadedSettings, setLoadedSettings] = useState(settings)
  if (settings !== loadedSettings) {
    setLoadedSettings(settings)
    if (settings) setIsDirty(false)
  }

  useEffect(() => {
    if (!settings) return
    form.setFieldsValue({ sprintWindow: settings.sprintWindow })
  }, [settings, form])

  const onFinish = async (values: SprintMetricsSettingsFormValues) => {
    try {
      const response = await updateSprintMetricsSettings({
        sprintWindow: values.sprintWindow,
      })
      if (response.error) {
        throw response.error
      }
      messageApi.success('Sprint metrics settings saved.')
      setIsDirty(false)
    } catch (error: any) {
      if (error?.status === 422 && error.errors) {
        form.setFields(toFormErrors(error.errors))
        messageApi.error('Correct the validation error(s) to continue.')
      } else {
        messageApi.error(
          error?.detail ??
            'An error occurred while saving the sprint metrics settings.',
        )
      }
    }
  }

  if (isLoading) {
    return <Skeleton active paragraph={{ rows: 1 }} />
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
        name="sprintWindow"
        label="Sprint window (sprints)"
        extra="How many of a team's most recent completed standard sprints its velocity, predictability and say/do are averaged over. The same for every team; changing it changes every team's figures on the next read."
        rules={[{ required: true, message: 'Enter the sprint window.' }]}
      >
        <InputNumber
          min={MIN_SPRINT_WINDOW}
          max={MAX_SPRINT_WINDOW}
          precision={0}
          aria-label="Sprint window (sprints)"
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

export default SprintMetricsSettingsForm
