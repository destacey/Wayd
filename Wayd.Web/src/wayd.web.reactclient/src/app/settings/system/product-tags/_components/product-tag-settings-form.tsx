'use client'

import { Button, Form, Skeleton, Switch } from 'antd'
import { useEffect, useState } from 'react'
import { useMessage } from '@/src/components/contexts/messaging'
import { toFormErrors } from '@/src/utils'
import { ProductTagSettingsDto } from '@/src/services/wayd-api'
import { useUpdateProductTagSettingsMutation } from '@/src/store/features/admin/system-settings-api'

interface ProductTagSettingsFormValues {
  allowsFreeTags: boolean
}

export interface ProductTagSettingsFormProps {
  settings: ProductTagSettingsDto | undefined
  isLoading: boolean
  canUpdate: boolean
}

const ProductTagSettingsForm = ({
  settings,
  isLoading,
  canUpdate,
}: ProductTagSettingsFormProps) => {
  const [form] = Form.useForm<ProductTagSettingsFormValues>()
  const [isDirty, setIsDirty] = useState(false)
  const messageApi = useMessage()
  const [updateProductTagSettings, { isLoading: isSaving }] =
    useUpdateProductTagSettingsMutation()

  // A fresh copy of the settings replaces the form's values, so nothing is
  // left unsaved.
  const [loadedSettings, setLoadedSettings] = useState(settings)
  if (settings !== loadedSettings) {
    setLoadedSettings(settings)
    if (settings) setIsDirty(false)
  }

  useEffect(() => {
    if (!settings) return
    form.setFieldsValue({ allowsFreeTags: settings.allowsFreeTags })
  }, [settings, form])

  const onFinish = async (values: ProductTagSettingsFormValues) => {
    try {
      const response = await updateProductTagSettings({
        allowsFreeTags: values.allowsFreeTags,
      })
      if (response.error) {
        throw response.error
      }
      messageApi.success('Product tag settings saved.')
      setIsDirty(false)
    } catch (error: any) {
      if (error?.status === 422 && error.errors) {
        form.setFields(toFormErrors(error.errors))
        messageApi.error('Correct the validation error(s) to continue.')
      } else {
        messageApi.error(
          error?.detail ??
            'An error occurred while saving the product tag settings.',
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
        name="allowsFreeTags"
        label="Allow free tags on products"
        valuePropName="checked"
        extra="Lets anyone who can tag a product add free tags — informal labels on no category, typed in while tagging. Turning it off stops new free tags; products keep the ones they have."
      >
        <Switch aria-label="Allow free tags on products" />
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

export default ProductTagSettingsForm
