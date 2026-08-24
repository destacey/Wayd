import { useMessage } from '@/src/components/contexts/messaging'
import { InitWorkspaceIntegrationRequest } from '@/src/services/wayd-api'
import { useInitAzdoConnectionWorkspaceMutation } from '@/src/store/features/app-integration/azdo-integration-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Form, Input, Modal } from 'antd'
import { useEffect } from 'react'
import { useModalForm } from '@/src/hooks'

const { Item } = Form
const { TextArea } = Input

export interface InitWorkspaceIntegrationFormProps {
  connectionId: string
  externalId: string
  workspaceName: string
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
  onFormSave,
  onFormCancel,
}: InitWorkspaceIntegrationFormProps) => {
  const messageApi = useMessage()

  const [initAzdoConnectionWorkspace] =
    useInitAzdoConnectionWorkspaceMutation()

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
            messageApi.success('Successfully initialized workspace.')
            return true
          } catch (error) {
            const apiError: ApiError = isApiError(error) ? error : {}
            if (apiError.status === 422 && apiError.errors) {
              const formErrors = toFormErrors(apiError.errors)
              form.setFields(formErrors)
              messageApi.error('Correct the validation error(s) to continue.')
            } else {
              messageApi.error(
                `Failed to initialize workspace. Error: ${apiError.detail}`,
              )
              console.error(error)
            }
            return false
          }
        },
      onComplete: onFormSave,
      onCancel: onFormCancel,
      errorMessage: 'Failed to initialize workspace.',
      permission: 'Permissions.Connections.Update',
    })

  useEffect(() => {
    form.setFieldsValue({ workspaceName })
  }, [form, workspaceName])

  return (
    <Modal
      title="Initialize Workspace"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Init"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="init-workspace-form"
        initialValues={{ workspaceName }}
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
          label="View Work Item URL Template"
          name="viewWorkItemUrlTemplate"
          extra={
            <span>
              <br />
              This template plus the work item external id will create a URL to
              view the work item in the external system.
            </span>
          }
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
