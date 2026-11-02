'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import {
  EmployeeMatchProperty,
  WorkdayOrgExclusionRequest,
  WorkdayWorkerKey,
} from '@/src/services/wayd-api'
import { ConnectorType, CONNECTOR_NAMES } from '@/src/types/connectors'
import { toApiError, toFormErrors } from '@/src/utils'
import { Button, Flex, Form, Modal } from 'antd'
import { useState } from 'react'
import { ConnectionFormBase } from './connection-form-base'
import { ConnectorTypeSelector } from './connector-type-selector'
import {
  useCreateConnectionMutation,
  useVerifyTeamsConnectionMutation,
} from '@/src/store/features/app-integration/connections-api'
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
    case ConnectorType.Teams:
      return 'teams'
    default:
      // A silent fallback would submit a request the backend can't deserialize —
      // fail loudly so a new connector can't ship without its discriminator.
      throw new Error(`No request discriminator is registered for connector type '${connector}'.`)
  }
}

const TEAMS_CREDENTIAL_FIELDS = ['tenantId', 'clientId', 'clientSecret'] as const

const CreateConnectionForm = ({
  onFormCreate,
  onFormCancel,
}: CreateConnectionFormProps) => {
  const [selectedConnector, setSelectedConnector] =
    useState<ConnectorType | null>(null)
  const [teamsVerifyState, setTeamsVerifyState] = useState<{
    fingerprint: string
    ok: boolean
    error?: string
  } | null>(null)
  const [isVerifyingTeams, setIsVerifyingTeams] = useState(false)
  const messageApi = useMessage()

  const [createConnection] = useCreateConnectionMutation()
  const [verifyTeamsConnection] = useVerifyTeamsConnectionMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel: hookCancel } =
    useModalForm<CreateConnectionFormValues>({
      onSubmit: async (values: CreateConnectionFormValues, form) => {
          if (!selectedConnector) return false

          if (selectedConnector === ConnectorType.Teams && !teamsVerified) {
            setTeamsVerifyState({
              fingerprint: teamsCredentialFingerprint,
              ok: false,
              error: 'Verify the connection before saving.',
            })
            return false
          }

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
            resetTeamsVerification()
            messageApi.success('Successfully created connection.')
            return true
          } catch (error: unknown) {
            const apiError = toApiError(error)
            if (apiError.status === 422 && apiError.errors) {
              const formErrors = toFormErrors(apiError.errors)
              form.setFields(formErrors)
              messageApi.error('Correct the validation error(s) to continue.')
            } else if (apiError.detail) {
              setTeamsVerifyState({
                fingerprint: teamsCredentialFingerprint,
                ok: false,
                error: apiError.detail,
              })
              messageApi.error(apiError.detail)
            } else {
              messageApi.error('An error occurred while creating the connection.')
            }
            return false
          }
        },
      onComplete: onFormCreate,
      onCancel: () => {
        setSelectedConnector(null)
        resetTeamsVerification()
        onFormCancel()
      },
      errorMessage: 'An error occurred while creating the connection.',
      permission: 'Permissions.Connections.Create',
    })

  const teamsTenantId = Form.useWatch('tenantId', form)
  const teamsClientId = Form.useWatch('clientId', form)
  const teamsClientSecret = Form.useWatch('clientSecret', form)

  const teamsCredentialFingerprint = [
    teamsTenantId ?? '',
    teamsClientId ?? '',
    teamsClientSecret ?? '',
  ].join('\0')

  const teamsVerified =
    teamsVerifyState?.ok === true &&
    teamsVerifyState.fingerprint === teamsCredentialFingerprint

  const teamsVerifyError =
    teamsVerifyState?.ok === false &&
    teamsVerifyState.fingerprint === teamsCredentialFingerprint
      ? (teamsVerifyState.error ?? null)
      : null

  const resetTeamsVerification = () => setTeamsVerifyState(null)

  const handleCancel = () => {
    setSelectedConnector(null)
    resetTeamsVerification()
    hookCancel()
  }

  const handleTeamsVerify = async () => {
    try {
      await form.validateFields(['name', ...TEAMS_CREDENTIAL_FIELDS])
    } catch {
      return
    }

    const values = form.getFieldsValue()
    const fingerprint = [
      values.tenantId ?? '',
      values.clientId ?? '',
      values.clientSecret ?? '',
    ].join('\0')

    setIsVerifyingTeams(true)
    try {
      const response = await verifyTeamsConnection({
        tenantId: values.tenantId!.trim(),
        clientId: values.clientId!.trim(),
        clientSecret: values.clientSecret!.trim(),
      })
      if (response.error) {
        throw response.error
      }
      setTeamsVerifyState({ fingerprint, ok: true })
      messageApi.success(
        'Connection verified. Check Teams for a chat named Wayd connection test and your calendar for the test event, then create the connection.',
      )
    } catch (error: unknown) {
      const apiError = toApiError(error)
      const detail =
        apiError.detail ??
        'The connection test failed. Confirm admin consent, permissions, and that your Wayd email exists in the tenant.'
      setTeamsVerifyState({ fingerprint, ok: false, error: detail })
    } finally {
      setIsVerifyingTeams(false)
    }
  }

  const isTeamsCreate =
    selectedConnector === ConnectorType.Teams && selectedConnector !== null

  return (
    <Modal
      title={
        selectedConnector === null
          ? 'Create Connection'
          : `Create ${CONNECTOR_NAMES[selectedConnector]} Connection`
      }
      open={isOpen}
      onOk={isTeamsCreate ? undefined : handleOk}
      onCancel={handleCancel}
      okButtonProps={{
        disabled: !isValid || (isTeamsCreate && !teamsVerified),
        hidden: selectedConnector === null || isTeamsCreate,
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
          : isTeamsCreate
            ? (
                <Flex justify="flex-end" gap={8}>
                  <Button onClick={handleCancel}>Cancel</Button>
                  {teamsVerified ? (
                    <Button
                      type="primary"
                      loading={isSaving}
                      disabled={!isValid}
                      onClick={() => void handleOk()}
                    >
                      Create
                    </Button>
                  ) : (
                    <Button
                      type="primary"
                      loading={isVerifyingTeams}
                      disabled={!isValid}
                      onClick={() => void handleTeamsVerify()}
                    >
                      Verify
                    </Button>
                  )}
                </Flex>
              )
            : undefined
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
            teamsVerifyError={isTeamsCreate ? teamsVerifyError : null}
          />
        </Form>
      )}
    </Modal>
  )
}

export default CreateConnectionForm
