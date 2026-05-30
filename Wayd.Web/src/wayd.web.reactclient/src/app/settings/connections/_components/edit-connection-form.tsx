'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import {
  AzureDevOpsConnectionDetailsDto,
  AzureOpenAIConnectionDetailsDto,
  ConnectionDetailsDto,
  EmployeeMatchProperty,
  EntraConnectionDetailsDto,
  UpdateAzureDevOpsConnectionRequest,
  UpdateAzureOpenAIConnectionRequest,
  UpdateConnectionRequest,
  UpdateEntraConnectionRequest,
  UpdateWorkdayConnectionRequest,
  WorkdayConnectionDetailsDto,
  WorkdayWorkerKey,
} from '@/src/services/wayd-api'
import { useUpdateConnectionMutation } from '@/src/store/features/app-integration/connections-api'
import { ConnectorType } from '@/src/types/connectors'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Form, Modal } from 'antd'
import { useEffect } from 'react'
import { useModalForm } from '@/src/hooks'
import { ConnectionFormBase } from './connection-form-base'

export interface EditConnectionFormProps {
  id: string
  /**
   * Pre-loaded connection passed by the page shell. The edit form re-uses
   * this rather than refetching, so the registry doesn't need to wire a
   * separate query.
   */
  connection: ConnectionDetailsDto
  onFormUpdate: () => void
  onFormCancel: () => void
}

interface EditConnectionFormValues {
  id: string
  name: string
  description?: string | null
  // Azure DevOps
  organization?: string | null
  personalAccessToken?: string | null
  // Azure OpenAI
  baseUrl?: string | null
  apiKey?: string | null
  deploymentName?: string | null
  // Entra
  tenantId?: string | null
  clientId?: string | null
  clientSecret?: string | null
  allUsersGroupObjectId?: string | null
  includeDisabledUsers?: boolean | null
  // Workday
  wsdlUrl?: string | null
  isuUsername?: string | null
  isuPassword?: string | null
  workerKey?: WorkdayWorkerKey | null
  includeInactive?: boolean | null
  incrementalSyncEnabled?: boolean | null
  useUserIdAsEmailFallback?: boolean | null
  // PeopleSync (Entra + Workday)
  matchBy?: EmployeeMatchProperty | null
}

const connectorTypeFromName = (
  name: string | undefined,
): ConnectorType | null => {
  switch (name) {
    case 'Azure DevOps':
      return ConnectorType.AzureDevOps
    case 'Azure OpenAI':
      return ConnectorType.AzureOpenAI
    case 'Entra':
      return ConnectorType.Entra
    case 'Workday':
      return ConnectorType.Workday
    // OpenAI is deliberately omitted: the backend has no Create/Update command
    // for it yet, so offering an edit form would only ever fail at submit.
    // Add the case back once UpdateOpenAIConnectionCommand ships.
    default:
      return null
  }
}

// Connection id comes from the loaded connection prop, not the form: Form.validateFields()
// only returns values for registered Form.Items, so a hidden id seeded via setFieldsValue
// is silently dropped on submit.
const buildRequest = (
  connection: ConnectionDetailsDto,
  values: EditConnectionFormValues,
): UpdateConnectionRequest | null => {
  switch (connection.connector?.name) {
    case 'Azure DevOps':
      return {
        $type: 'azure-devops',
        id: connection.id,
        name: values.name,
        description: values.description ?? undefined,
        organization: values.organization ?? '',
        personalAccessToken: values.personalAccessToken ?? '',
      } as UpdateAzureDevOpsConnectionRequest
    case 'Azure OpenAI':
      return {
        $type: 'azure-openai',
        id: connection.id,
        name: values.name,
        description: values.description ?? undefined,
        baseUrl: values.baseUrl ?? '',
        apiKey: values.apiKey ?? '',
        deploymentName: values.deploymentName ?? '',
      } as UpdateAzureOpenAIConnectionRequest
    case 'Entra':
      return {
        $type: 'entra',
        id: connection.id,
        name: values.name,
        description: values.description ?? undefined,
        tenantId: values.tenantId ?? '',
        clientId: values.clientId ?? '',
        clientSecret: values.clientSecret ?? '',
        allUsersGroupObjectId: values.allUsersGroupObjectId ?? undefined,
        includeDisabledUsers: values.includeDisabledUsers ?? false,
        matchBy: values.matchBy ?? EmployeeMatchProperty.Email,
      } as UpdateEntraConnectionRequest
    case 'Workday':
      return {
        $type: 'workday',
        id: connection.id,
        name: values.name,
        description: values.description ?? undefined,
        wsdlUrl: values.wsdlUrl ?? '',
        isuUsername: values.isuUsername ?? '',
        isuPassword: values.isuPassword ?? '',
        workerKey: values.workerKey ?? WorkdayWorkerKey.EmployeeId,
        includeInactive: values.includeInactive ?? false,
        incrementalSyncEnabled: values.incrementalSyncEnabled ?? true,
        matchBy: values.matchBy ?? EmployeeMatchProperty.Email,
        useUserIdAsEmailFallback: values.useUserIdAsEmailFallback ?? false,
      } as UpdateWorkdayConnectionRequest
    default:
      return null
  }
}

const seedFormValues = (
  connection: ConnectionDetailsDto,
): EditConnectionFormValues => {
  const base: EditConnectionFormValues = {
    id: connection.id,
    name: connection.name,
    description: connection.description ?? '',
  }
  switch (connection.connector?.name) {
    case 'Azure DevOps': {
      const c = connection as AzureDevOpsConnectionDetailsDto
      return {
        ...base,
        organization: c.configuration?.organization,
        personalAccessToken: c.configuration?.personalAccessToken,
      }
    }
    case 'Azure OpenAI': {
      const c = connection as AzureOpenAIConnectionDetailsDto
      return {
        ...base,
        baseUrl: c.configuration?.baseUrl,
        apiKey: c.configuration?.apiKey,
        deploymentName: c.configuration?.deploymentName,
      }
    }
    case 'Entra': {
      const c = connection as EntraConnectionDetailsDto
      return {
        ...base,
        tenantId: c.configuration?.tenantId,
        clientId: c.configuration?.clientId,
        clientSecret: c.configuration?.clientSecret,
        allUsersGroupObjectId: c.configuration?.allUsersGroupObjectId,
        includeDisabledUsers: c.configuration?.includeDisabledUsers ?? false,
        matchBy: c.configuration?.matchBy ?? EmployeeMatchProperty.Email,
      }
    }
    case 'Workday': {
      const c = connection as WorkdayConnectionDetailsDto
      return {
        ...base,
        wsdlUrl: c.configuration?.wsdlUrl,
        isuUsername: c.configuration?.isuUsername,
        isuPassword: c.configuration?.isuPassword,
        workerKey: c.configuration?.workerKey ?? WorkdayWorkerKey.EmployeeId,
        includeInactive: c.configuration?.includeInactive ?? false,
        incrementalSyncEnabled: c.configuration?.incrementalSyncEnabled ?? true,
        matchBy: c.configuration?.matchBy ?? EmployeeMatchProperty.Email,
        useUserIdAsEmailFallback:
          c.configuration?.useUserIdAsEmailFallback ?? false,
      }
    }
    default:
      return base
  }
}

const EditConnectionForm = ({
  id,
  connection,
  onFormUpdate,
  onFormCancel,
}: EditConnectionFormProps) => {
  const messageApi = useMessage()
  const connectorType = connectorTypeFromName(connection.connector?.name)

  const [updateConnection] = useUpdateConnectionMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<EditConnectionFormValues>({
      onSubmit: async (values, form) => {
        try {
          const request = buildRequest(connection, values)
          if (!request) {
            messageApi.error(
              `Editing is not yet supported for ${connection.connector?.name} connections.`,
            )
            return false
          }
          const response = await updateConnection(request)
          if (response.error) throw response.error
          messageApi.success('Successfully updated connection.')
          return true
        } catch (error) {
          const apiError: ApiError = isApiError(error) ? error : {}
          if (apiError.status === 422 && apiError.errors) {
            const formErrors = toFormErrors(apiError.errors)
            form.setFields(formErrors)
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error('An error occurred while editing the connection.')
            console.error(error)
          }
          return false
        }
      },
      onComplete: onFormUpdate,
      onCancel: onFormCancel,
      errorMessage: 'An error occurred while editing the connection.',
      permission: 'Permissions.Connections.Update',
    })

  useEffect(() => {
    form.setFieldsValue(seedFormValues(connection))
  }, [connection, form])

  // Surface a clear message if a connector lands without a registered edit shape.
  if (connectorType === null) {
    return (
      <Modal
        title="Edit Connection"
        open={isOpen}
        onCancel={handleCancel}
        onOk={handleCancel}
        okText="Close"
      >
        Editing is not yet supported for {connection.connector?.name}{' '}
        connections.
      </Modal>
    )
  }

  return (
    <Modal
      title="Edit Connection"
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
        name="edit-connection-form"
      >
        <ConnectionFormBase
          connector={connectorType}
          mode="edit"
          form={form}
        />
      </Form>
    </Modal>
  )
}

export default EditConnectionForm
