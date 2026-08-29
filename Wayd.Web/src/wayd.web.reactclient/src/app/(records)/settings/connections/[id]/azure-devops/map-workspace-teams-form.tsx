'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import {
  AzdoConnectionTeamMappingsRequest,
  AzdoWorkspaceTeamMappingRequest,
} from '@/src/services/wayd-api'
import {
  useGetAzdoConnectionTeamsQuery,
  useMapAzdoConnectionTeamsMutation,
} from '@/src/store/features/app-integration/azdo-integration-api'
import { useGetTeamOptionsQuery } from '@/src/store/features/organizations/team-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Flex, Form, Input, Modal, Select, Typography } from 'antd'
import { useEffect } from 'react'
import { useModalForm } from '@/src/hooks'

const { Item, List } = Form
const { Text } = Typography

export interface MapAzdoWorkspaceTeamsFormProps {
  connectionId: string
  workspaceId: string
  workspaceName: string
  onFormSave: () => void
  onFormCancel: () => void
}

interface AzdoTeamMapping {
  teamId: string
  teamName: string
  internalTeamId: string | null
}

interface MapAzdoWorkspaceTeamsFormValues {
  teamMappings: AzdoTeamMapping[]
}

const mapToRequestValues = (
  connectionId: string,
  workspaceId: string,
  values: MapAzdoWorkspaceTeamsFormValues,
): AzdoConnectionTeamMappingsRequest => {
  return {
    connectionId: connectionId,
    teamMappings: values.teamMappings.map(
      (team) =>
        ({
          workspaceId: workspaceId,
          teamId: team.teamId,
          internalTeamId: team.internalTeamId,
        }) as AzdoWorkspaceTeamMappingRequest,
    ),
  } as AzdoConnectionTeamMappingsRequest
}

const MapAzdoWorkspaceTeamsForm = ({
  connectionId,
  workspaceId,
  workspaceName,
  onFormSave,
  onFormCancel,
}: MapAzdoWorkspaceTeamsFormProps) => {
  const messageApi = useMessage()

  const { data: connectionTeamsData } = useGetAzdoConnectionTeamsQuery({
    connectionId: connectionId,
    workspaceId: workspaceId,
  })

  const { data: teamOptionsData } = useGetTeamOptionsQuery(true)

  const [mapConnectionTeamsMutation] = useMapAzdoConnectionTeamsMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<MapAzdoWorkspaceTeamsFormValues>({
      onSubmit: async (values: MapAzdoWorkspaceTeamsFormValues, form) => {
          try {
            const request = mapToRequestValues(
              connectionId,
              workspaceId,
              values,
            )
            const response = await mapConnectionTeamsMutation(request)
            if (response.error) {
              throw response.error
            }
            messageApi.success('Successfully updated workspace team mappings.')
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
                  'An error occurred while update workspace team mappings.',
              )
              console.error(error)
            }
            return false
          }
        },
      onComplete: onFormSave,
      onCancel: onFormCancel,
      errorMessage: 'An error occurred while updating workspace team mappings.',
      permission: 'Permissions.Connections.Update',
    })

  useEffect(() => {
    if (!connectionTeamsData) return
    form.setFieldsValue({
      teamMappings: connectionTeamsData
        .slice()
        .sort((a, b) => a.teamName.localeCompare(b.teamName))
        .map((team) => ({
          teamId: team.teamId,
          teamName: team.teamName,
          internalTeamId: team.internalTeamId,
        })),
    })
  }, [connectionTeamsData, form])

  return (
    <Modal
      title="Map Workspace Teams"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Save"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Flex gap="small" vertical style={{ marginBottom: '15px' }}>
        <Text type="secondary">
          Map the Azure DevOps workspace teams to Wayd teams.
        </Text>
        <Text>Workspace: {workspaceName}</Text>
      </Flex>
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="map-workspace-teams-form"
      >
        <Item name="teamMappings">
          <List name="teamMappings">
            {(fields, { add, remove }) => (
              <>
                {fields.map(({ key, name, ...restField }) => (
                  <div key={key}>
                    <Flex align="center" justify="space-between">
                      <Flex vertical style={{ width: '90%' }}>
                        <Flex gap="small">
                          <Item
                            {...restField}
                            name={[name, 'teamId']}
                            hidden={true}
                          >
                            <Input hidden={true} />
                          </Item>
                          <Item
                            {...restField}
                            style={{ width: '50%' }}
                            name={[name, 'teamName']}
                            rules={[{ required: true }]}
                          >
                            <Input disabled={true} />
                          </Item>
                          <Item
                            {...restField}
                            style={{ width: '50%' }}
                            name={[name, 'internalTeamId']}
                          >
                            <Select
                              allowClear
                              showSearch
                              placeholder="Select a Team"
                              filterOption={(input, option) =>
                                (option?.label ?? '')
                                  .toLowerCase()
                                  .includes(input.toLowerCase())
                              }
                              options={teamOptionsData}
                            />
                          </Item>
                        </Flex>
                      </Flex>
                    </Flex>
                  </div>
                ))}
              </>
            )}
          </List>
        </Item>
      </Form>
    </Modal>
  )
}

export default MapAzdoWorkspaceTeamsForm
