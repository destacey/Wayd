'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { InitWorkspaceIntegrationRequest } from '@/src/services/wayd-api'
import { useInitAzdoConnectionWorkspaceMutation } from '@/src/store/features/app-integration/azdo-integration-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Alert, Form, Input, Modal } from 'antd'
import { useModalForm } from '@/src/hooks'
import { suggestWorkspaceKey, workItemUrlTemplate } from './project-defaults'

const { Item } = Form
const { TextArea } = Input

export interface InitWorkspaceIntegrationFormProps {
  connectionId: string
  externalId: string
  workspaceName: string
  organizationUrl?: string
  /** The project's process name when Wayd has not set that process up yet. */
  newProcessName?: string
  onFormSave: () => void
  onFormCancel: () => void
}

interface InitWorkspaceIntegrationFormValues {
  workspaceKey: string
  workspaceName: string
  viewWorkItemUrlTemplate?: string
}

const mapToRequestValues = (values: InitWorkspaceIntegrationFormValues) => {
  return {
    workspaceKey: values.workspaceKey,
    workspaceName: values.workspaceName,
    externalViewWorkItemUrlTemplate: values.viewWorkItemUrlTemplate,
  } as InitWorkspaceIntegrationRequest
}

const InitWorkspaceIntegrationForm = ({
  connectionId,
  externalId,
  workspaceName,
  organizationUrl,
  newProcessName,
  onFormSave,
  onFormCancel,
}: InitWorkspaceIntegrationFormProps) => {
  const messageApi = useMessage()

  const [initAzdoConnectionWorkspace] = useInitAzdoConnectionWorkspaceMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<InitWorkspaceIntegrationFormValues>({
      onSubmit: async (values: InitWorkspaceIntegrationFormValues, form) => {
        try {
          const request = mapToRequestValues(values)
          request.id = connectionId
          request.externalId = externalId

          const response = await initAzdoConnectionWorkspace(request)
          if (response.error) {
            throw response.error
          }
          messageApi.success(
            `${workspaceName} is set up. Its work items sync on the next work sync; start one from Sync History.`,
          )
          return true
        } catch (error) {
          const apiError: ApiError = isApiError(error) ? error : {}
          if (apiError.status === 422 && apiError.errors) {
            const formErrors = toFormErrors(apiError.errors)
            form.setFields(formErrors)
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              `Failed to set up ${workspaceName}. Error: ${apiError.detail}`,
            )
            console.error(error)
          }
          return false
        }
      },
      onComplete: onFormSave,
      onCancel: onFormCancel,
      errorMessage: `Failed to set up ${workspaceName}.`,
      permission: 'Permissions.Connections.Update',
    })

  return (
    <Modal
      title={`Set Up ${workspaceName}`}
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Set Up"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      {newProcessName && (
        <Alert
          type="info"
          showIcon
          style={{ marginBottom: 16 }}
          title={`This also sets up the ${newProcessName} process in Wayd: its work types, statuses and workflows.`}
        />
      )}
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="init-workspace-form"
        initialValues={{
          workspaceName,
          workspaceKey: suggestWorkspaceKey(workspaceName),
          viewWorkItemUrlTemplate: organizationUrl
            ? workItemUrlTemplate(organizationUrl, workspaceName)
            : undefined,
        }}
      >
        <Item
          name="workspaceKey"
          label="Workspace Key"
          extra="The workspace key is a unique identifier for the workspace. Workspace key's must be uppercase letters and numbers only, start with an uppercase letter, and be between 2-20 characters."
          rules={[
            {
              required: true,
              message: 'The Workspace Key field is required.',
            },
            {
              min: 2,
              max: 20,
              message:
                'The Workspace Key field must be between 2-20 characters.',
            },
            {
              pattern: /^([A-Z][A-Z0-9]{1,19})$/,
              message:
                "The Workspace Key field is invalid. Workspace key's must be uppercase letters and numbers only and start with an uppercase letter.",
            },
          ]}
        >
          <Input
            showCount
            maxLength={20}
            onInput={(e) =>
              ((e.target as HTMLInputElement).value = (
                e.target as HTMLInputElement
              ).value.toUpperCase())
            }
          />
        </Item>
        <Item
          label="Workspace Name"
          name="workspaceName"
          rules={[{ required: true }]}
        >
          <Input showCount maxLength={64} />
        </Item>
        <Item
          label="Work Item Link"
          name="viewWorkItemUrlTemplate"
          extra="Wayd adds each work item's Azure DevOps id to this link to open the item in Azure DevOps."
        >
          <TextArea
            autoSize={{ minRows: 1, maxRows: 4 }}
            showCount
            maxLength={256}
          />
        </Item>
      </Form>
    </Modal>
  )
}

export default InitWorkspaceIntegrationForm
