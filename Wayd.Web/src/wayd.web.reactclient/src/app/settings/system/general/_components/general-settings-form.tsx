'use client'

import { Button, Form, Skeleton } from 'antd'
import { useEffect, useState } from 'react'
import { useMessage } from '@/src/components/contexts/messaging'
import { toFormErrors } from '@/src/utils'
import { GeneralSettingsDto } from '@/src/services/wayd-api'
import { useUpdateGeneralSettingsMutation } from '@/src/store/features/admin/system-settings-api'
import { TimeZoneSelect } from '@/src/components/common/scheduling'

interface GeneralSettingsFormValues {
  timeZone: string
}

export interface GeneralSettingsFormProps {
  settings: GeneralSettingsDto | undefined
  isLoading: boolean
  canUpdate: boolean
}

const GeneralSettingsForm = ({
  settings,
  isLoading,
  canUpdate,
}: GeneralSettingsFormProps) => {
  const [form] = Form.useForm<GeneralSettingsFormValues>()
  const [isDirty, setIsDirty] = useState(false)
  const messageApi = useMessage()
  const [updateGeneralSettings, { isLoading: isSaving }] =
    useUpdateGeneralSettingsMutation()

  // A fresh copy of the settings replaces the form's values, so nothing is
  // left unsaved.
  const [loadedSettings, setLoadedSettings] = useState(settings)
  if (settings !== loadedSettings) {
    setLoadedSettings(settings)
    if (settings) setIsDirty(false)
  }

  useEffect(() => {
    if (!settings) return
    form.setFieldsValue({ timeZone: settings.timeZone })
  }, [settings, form])

  const onFinish = async (values: GeneralSettingsFormValues) => {
    try {
      const response = await updateGeneralSettings({
        timeZone: values.timeZone,
      })
      if (response.error) {
        throw response.error
      }
      messageApi.success('General settings saved.')
      setIsDirty(false)
    } catch (error: any) {
      if (error?.status === 422 && error.errors) {
        form.setFields(toFormErrors(error.errors))
        messageApi.error('Correct the validation error(s) to continue.')
      } else {
        messageApi.error(
          error?.detail ??
            'An error occurred while saving the general settings.',
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
        name="timeZone"
        label="System time zone"
        extra="The organization's time zone. Wayd reads calendar days in it wherever no team's own time zone applies, such as sprints with no team and dates sent without a time, and new teams start with it. A team's own time zone always wins for that team, and changing this one does not change any team."
        rules={[{ required: true, message: 'Select a time zone.' }]}
      >
        <TimeZoneSelect aria-label="System time zone" />
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

export default GeneralSettingsForm
