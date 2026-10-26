'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { IntakeDeskWorkflowDto } from '@/src/services/wayd-api'
import {
  useChangeIntakeDeskWorkflowMutation,
  useGetIntakeWorkflowChoicesQuery,
} from '@/src/store/features/demand-management/demand-management-api'
import { toApiError, toFormErrors } from '@/src/utils'
import { Form, Modal, Select, Typography } from 'antd'
import { FC, useState } from 'react'

const { Item } = Form
const { Paragraph } = Typography

export interface ChangeIntakeDeskWorkflowFormProps {
  deskId: string
  deskName: string
  workflow: IntakeDeskWorkflowDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface ChangeWorkflowFormValues {
  workflowId: string
}

/**
 * Points a desk at another published intake workflow. Offered where the desk workflow's `canChange` says so:
 * to its Desk Admins and the Demand Management Admin, while no request is on it.
 */
const ChangeIntakeDeskWorkflowForm: FC<ChangeIntakeDeskWorkflowFormProps> = ({
  deskId,
  deskName,
  workflow,
  onFormComplete,
  onFormCancel,
}) => {
  const messageApi = useMessage()
  // Saving the workflow the desk already follows changes nothing, so the change waits for another choice.
  const [chosenId, setChosenId] = useState<string | undefined>(workflow.workflow.id)
  const [changeWorkflow] = useChangeIntakeDeskWorkflowMutation()
  const { data: choices, isLoading } = useGetIntakeWorkflowChoicesQuery()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<ChangeWorkflowFormValues>({
      onSubmit: async (values, form) => {
        try {
          await changeWorkflow({
            deskId,
            workflowId: values.workflowId,
          }).unwrap()
          messageApi.success('Workflow changed.')
          return true
        } catch (error) {
          const apiError = toApiError(error)
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ?? 'Failed to change the workflow.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage: 'Failed to change the workflow.',
    })

  return (
    <Modal
      title="Change Workflow"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{
        disabled: !isValid || chosenId === workflow.workflow.id,
      }}
      okText="Change"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Paragraph type="secondary">
        The stages {deskName} reviews its requests in. Choosing a workflow other
        than the one it follows gives the desk its own, so a later change to the
        organization&apos;s default no longer reaches it. Workflows are built in
        Settings, under Status Workflows.
      </Paragraph>
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="change-intake-desk-workflow-form"
        initialValues={{ workflowId: workflow.workflow.id }}
        onValuesChange={(changed) => {
          if ('workflowId' in changed) setChosenId(changed.workflowId)
        }}
      >
        <Item
          name="workflowId"
          label="Workflow"
          rules={[{ required: true, message: 'Choose a workflow.' }]}
        >
          <Select
            loading={isLoading}
            options={(choices ?? []).map((w) => ({
              value: w.id,
              label: w.name,
            }))}
          />
        </Item>
      </Form>
    </Modal>
  )
}

export default ChangeIntakeDeskWorkflowForm
