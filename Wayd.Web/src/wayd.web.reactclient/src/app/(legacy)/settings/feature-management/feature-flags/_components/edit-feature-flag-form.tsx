'use client'

import { Form, Input, Modal, Spin } from 'antd'
import { useEffect } from 'react'
import { toFormErrors } from '@/src/utils'
import {
  useGetFeatureFlagQuery,
  useUpdateFeatureFlagMutation,
} from '@/src/store/features/admin/feature-flags-api'
import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'

const { Item } = Form
const { TextArea } = Input

interface EditFeatureFlagFormProps {
  featureFlagId: number
  onFormSave: () => void
  onFormCancel: () => void
}

interface EditFeatureFlagFormValues {
  displayName: string
  description?: string
}

const EditFeatureFlagForm = ({
  featureFlagId,
  onFormSave,
  onFormCancel,
}: EditFeatureFlagFormProps) => {
  const messageApi = useMessage()

  const { data: featureFlag, isLoading } =
    useGetFeatureFlagQuery(featureFlagId)
  const [updateFeatureFlag] = useUpdateFeatureFlagMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<EditFeatureFlagFormValues>({
      onSubmit: async (values: EditFeatureFlagFormValues, form) => {
          try {
            const response = await updateFeatureFlag({
              id: featureFlagId,
              displayName: values.displayName,
              description: values.description || undefined,
            })

            if (response.error) {
              throw response.error
            }

            messageApi.success('Successfully updated feature flag.')
            onFormSave()
            return false
          } catch (error: any) {
            if (error.status === 422 && error.errors) {
              const formErrors = toFormErrors(error.errors)
              form.setFields(formErrors)
              messageApi.error('Correct the validation error(s) to continue.')
            } else {
              messageApi.error(
                error?.detail ||
                  'An unexpected error occurred while updating the feature flag.',
              )
            }
            return false
          }
        },
      onComplete: () => {},
      onCancel: onFormCancel,
      errorMessage:
        'An unexpected error occurred while updating the feature flag.',
      permission: 'Permissions.FeatureFlags.Update',
    })

  useEffect(() => {
    if (featureFlag) {
      form.setFieldsValue({
        displayName: featureFlag.displayName,
        description: featureFlag.description ?? undefined,
      })
    }
  }, [featureFlag, form])

  if (isLoading) {
    return <Spin />
  }

  return (
    <Modal
      title={`Edit Feature Flag: ${featureFlag?.name ?? ''}`}
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
        name="edit-feature-flag-form"
      >
        <Item
          label="Display Name"
          name="displayName"
          rules={[{ required: true }]}
        >
          <Input showCount maxLength={128} />
        </Item>

        <Item name="description" label="Description">
          <TextArea
            autoSize={{ minRows: 3, maxRows: 6 }}
            showCount
            maxLength={1024}
          />
        </Item>
      </Form>
    </Modal>
  )
}

export default EditFeatureFlagForm
