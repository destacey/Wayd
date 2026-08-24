'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { ChangeProjectKeyRequest } from '@/src/services/wayd-api'
import {
  useChangeProjectKeyMutation,
  useGetProjectQuery,
} from '@/src/store/features/ppm/projects-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Flex, Form, Input, Modal, Typography } from 'antd'
import { useEffect } from 'react'

const { Item } = Form
const { Text } = Typography

export interface ChangeProjectKeyFormProps {
  projectKey: string
  onFormComplete: (newKey: string) => void
  onFormCancel: () => void
}

interface ChangeProjectKeyFormValues {
  key: string
}

const keyPattern = /^[A-Z0-9]{2,20}$/

const ChangeProjectKeyForm = ({
  projectKey,
  onFormComplete,
  onFormCancel,
}: ChangeProjectKeyFormProps) => {
  const messageApi = useMessage()

  const { data: projectData } = useGetProjectQuery(projectKey)

  const [changeProjectKey] = useChangeProjectKeyMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<ChangeProjectKeyFormValues>({
      onSubmit: async (values: ChangeProjectKeyFormValues, form) => {
          try {
            const newKey = values.key.trim().toUpperCase()
            const request: ChangeProjectKeyRequest = {
              key: newKey,
            }

            const response = await changeProjectKey({
              id: projectData!.id,
              request,
            })

            if (response.error) throw response.error

            messageApi.success('Project key changed successfully.')
            // Pass the new key back to the parent via onFormComplete
            onFormComplete(newKey)
            return false // Don't call onComplete again from the hook
          } catch (error) {
            const apiError: ApiError = isApiError(error) ? error : {}
            if (apiError.status === 422 && apiError.errors) {
              const formErrors = toFormErrors(apiError.errors)
              form.setFields(formErrors)
              messageApi.error('Correct the validation error(s) to continue.')
            } else {
              messageApi.error(
                apiError.detail ??
                  'An error occurred while updating the project key. Please try again.',
              )
            }
            return false
          }
        },
      onComplete: () => {}, // Handled inside onSubmit since we need to pass newKey
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while changing the project key. Please try again.',
      permission: 'Permissions.Projects.Update',
    })

  // Initialize form values when data is loaded
  useEffect(() => {
    if (!projectData) return

    form.setFieldsValue({
      key: projectData.key,
    })
  }, [projectData, form])

  return (
    <Modal
      title="Change Project Key"
      open={isOpen}
      width={'40vw'}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Save"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Flex vertical gap="small">
        <Text type="secondary">Enter a new key for this project.</Text>
        <Form
          form={form}
          size="small"
          layout="vertical"
          name="change-project-key-form"
        >
          <Item
            name="key"
            label="Key"
            extra="2-20 uppercase alphanumeric characters (A-Z, 0-9)"
            rules={[
              { required: true, message: 'Key is required.' },
              {
                pattern: keyPattern,
                message:
                  'Key must be 2-20 uppercase alphanumeric characters (A-Z, 0-9).',
              },
            ]}
            normalize={(value) => (value ?? '').toUpperCase()}
          >
            <Input
              placeholder="Enter new key"
              autoComplete="off"
              showCount
              maxLength={20}
            />
          </Item>
        </Form>
      </Flex>
    </Modal>
  )
}

export default ChangeProjectKeyForm
