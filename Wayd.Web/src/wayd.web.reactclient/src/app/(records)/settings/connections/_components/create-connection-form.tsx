import { useMessage } from '@/src/components/contexts/messaging'
import {
  EmployeeMatchProperty,
  WorkdayOrgExclusionRequest,
  WorkdayWorkerKey,
} from '@/src/services/wayd-api'
import { ConnectorType, CONNECTOR_NAMES } from '@/src/types/connectors'
import { toFormErrors } from '@/src/utils'
import { Form, Modal } from 'antd'
import { useState } from 'react'
import { ConnectionFormBase } from './connection-form-base'
import { ConnectorTypeSelector } from './connector-type-selector'
import { useCreateConnectionMutation } from '@/src/store/features/app-integration/connections-api'
import { useModalForm } from '@/src/hooks'

export interface CreateConnectionFormProps {
  onFormCreate: () => void
  onFormCancel: () => void
}

interface CreateConnectionFormValues {
  name: string
  description?: string
  // Azure DevOps specific
  organization?: string
  personalAccessToken?: string
  // Azure OpenAI specific
  baseUrl?: string
  apiKey?: string
  deploymentName?: string
  // Entra specific
  tenantId?: string
  clientId?: string
  clientSecret?: string
  allUsersGroupObjectId?: string
  includeDisabledUsers?: boolean
  // Workday specific
  wsdlUrl?: string
  isuUsername?: string
  isuPassword?: string
  workerKey?: WorkdayWorkerKey
  includeInactive?: boolean
  useUserIdAsEmailFallback?: boolean
  usePreferredName?: boolean
  normalizeNameCasing?: boolean
  departmentOrganizationTypeId?: string
  orgExclusions?: WorkdayOrgExclusionRequest[]
  // PeopleSync (Entra + Workday)
  matchBy?: EmployeeMatchProperty
}

export const getDiscriminator = (connector: ConnectorType): string => {
  switch (connector) {
    case ConnectorType.AzureDevOps:
      return 'azure-devops'
    case ConnectorType.AzureOpenAI:
      return 'azure-openai'
    case ConnectorType.Entra:
      return 'entra'
    case ConnectorType.Workday:
      return 'workday'
    default:
      // A silent fallback would submit a request the backend can't deserialize —
      // fail loudly so a new connector can't ship without its discriminator.
      throw new Error(`No request discriminator is registered for connector type '${connector}'.`)
  }
}

const CreateConnectionForm = ({
  onFormCreate,
  onFormCancel,
}: CreateConnectionFormProps) => {
  const [selectedConnector, setSelectedConnector] =
    useState<ConnectorType | null>(null)
  const messageApi = useMessage()

  const [createConnection] = useCreateConnectionMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel: hookCancel } =
    useModalForm<CreateConnectionFormValues>({
      onSubmit: async (values: CreateConnectionFormValues, form) => {
          if (!selectedConnector) return false

          try {
            // System.Text.Json polymorphism requires the discriminator to be the FIRST property
            // in the JSON payload — otherwise it throws "must specify a type discriminator" even
            // when the discriminator is present. Spread $type before the form values so it
            // serializes first.
            //
            // Form.List leaves orgExclusions undefined when the admin never adds a row, and
            // partially-filled rows (the admin opened a slot then canceled) come back with empty
            // strings. Strip those before submit so they don't fail backend validation.
            const cleanedExclusions = (values.orgExclusions ?? []).filter(
              (e) =>
                !!e?.organizationTypeId?.trim() &&
                !!e?.organizationReference?.trim(),
            )

            const request = {
              $type: getDiscriminator(selectedConnector),
              ...values,
              orgExclusions: cleanedExclusions,
            }

            const response = await createConnection(request)
            if (response.error) {
              throw response.error
            }
            setSelectedConnector(null)
            messageApi.success('Successfully created connection.')
            return true
          } catch (error: any) {
            if (error.status === 422 && error.errors) {
              const formErrors = toFormErrors(error.errors)
              form.setFields(formErrors)
              messageApi.error('Correct the validation error(s) to continue.')
            } else {
              messageApi.error('An error occurred while creating the connection.')
            }
            return false
          }
        },
      onComplete: onFormCreate,
      onCancel: () => {
        setSelectedConnector(null)
        onFormCancel()
      },
      errorMessage: 'An error occurred while creating the connection.',
      permission: 'Permissions.Connections.Create',
    })

  const handleBack = () => {
    setSelectedConnector(null)
    form.resetFields()
  }

  const handleCancel = () => {
    setSelectedConnector(null)
    hookCancel()
  }

  return (
    <Modal
      title={
        selectedConnector === null
          ? 'Create Connection'
          : `Create ${CONNECTOR_NAMES[selectedConnector]} Connection`
      }
      open={isOpen}
      onOk={selectedConnector === null ? undefined : handleOk}
      onCancel={handleCancel}
      okButtonProps={{
        disabled: !isValid,
        hidden: selectedConnector === null,
      }}
      okText="Create"
      confirmLoading={isSaving}
      keyboard={false}
      destroyOnHidden={true}
      // Viewport-bounded width: 95vw on mobile, capped at the form-specific max on desktop. The
      // selector step is narrower (600) since it only renders connector tiles; the config step
      // expands to 800 to fit the exclusion picker comfortably.
      width="95vw"
      style={{ maxWidth: selectedConnector === null ? 600 : 800 }}
      footer={
        selectedConnector === null
          ? null
          : selectedConnector !== null
            ? undefined
            : null
      }
    >
      {selectedConnector === null ? (
        <ConnectorTypeSelector onSelect={setSelectedConnector} />
      ) : (
        <Form
          form={form}
          size="middle"
          layout="vertical"
          name="create-connection-form"
        >
          <ConnectionFormBase
            connector={selectedConnector}
            mode="create"
            form={form}
          />
        </Form>
      )}
    </Modal>
  )
}

export default CreateConnectionForm
