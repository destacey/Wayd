'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { ProjectDetailsDto, ProjectStatus } from '@/src/services/wayd-api'
import { useRevertProjectStatusMutation } from '@/src/store/features/ppm/projects-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Alert, Flex, Form, Input, Modal, Select, Typography } from 'antd'
import { useEffect } from 'react'

const { Item } = Form
const { TextArea } = Input
const { Text } = Typography

export interface RevertProjectStatusFormProps {
  project: ProjectDetailsDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface RevertProjectStatusFormValues {
  toStatus: ProjectStatus
  reason: string
}

const RevertProjectStatusForm = ({
  project,
  onFormComplete,
  onFormCancel,
}: RevertProjectStatusFormProps) => {
  const messageApi = useMessage()

  const [revertProjectStatus] = useRevertProjectStatusMutation()

  // Server-supplied, and already filtered by the project's lifecycle and dates as well as its status.
  // Do not re-derive it here, or the options drift from what the aggregate accepts.
  const statusOptions = (project.backwardStatusTargets ?? []).map((status) => ({
    value: status.name,
    label: status.name,
  }))

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<RevertProjectStatusFormValues>({
      onSubmit: async (values: RevertProjectStatusFormValues, form) => {
        try {
          const response = await revertProjectStatus({
            id: project.id,
            cacheKey: project.key,
            toStatus: values.toStatus,
            reason: values.reason.trim(),
          })

          if (response.error) throw response.error

          messageApi.success(`Project reverted to ${values.toStatus}.`)
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
                'An error occurred while reverting the project status. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while reverting the project status. Please try again.',
      permission: 'Permissions.Projects.Update',
    })

  // Preselect when there is only one place to go back to.
  useEffect(() => {
    if (statusOptions.length === 1) {
      form.setFieldsValue({ toStatus: statusOptions[0].value as ProjectStatus })
    }
  }, [statusOptions, form])

  return (
    <Modal
      title="Revert Project Status"
      open={isOpen}
      width={'40vw'}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Revert"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Flex vertical gap="small">
        <Text type="secondary">
          {project.key} - {project.name}
        </Text>
        <Alert
          title={`This project is currently ${project.status.name}.`}
          description="Reverting moves it back to an earlier status. The change and your reason are recorded in the project's status history."
          type="warning"
          showIcon
        />
        <Form
          form={form}
          size="small"
          layout="vertical"
          name="revert-project-status-form"
        >
          <Item
            name="toStatus"
            label="Revert to"
            rules={[{ required: true, message: 'A status is required.' }]}
          >
            <Select
              placeholder="Select a status"
              options={statusOptions}
              allowClear
            />
          </Item>
          <Item
            name="reason"
            label="Reason"
            extra="Explain why this project is being reverted."
            rules={[
              { required: true, message: 'A reason is required.' },
              {
                whitespace: true,
                message: 'A reason is required.',
              },
            ]}
          >
            <TextArea
              placeholder="Why is this project being reverted?"
              autoSize={{ minRows: 3, maxRows: 6 }}
              showCount
              maxLength={1024}
            />
          </Item>
        </Form>
      </Flex>
    </Modal>
  )
}

export default RevertProjectStatusForm
