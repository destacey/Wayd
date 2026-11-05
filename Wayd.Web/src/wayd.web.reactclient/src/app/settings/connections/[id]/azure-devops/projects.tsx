'use client'

import useAuth from '@/src/components/contexts/auth'
import { useMessage } from '@/src/components/contexts/messaging'
import {
  AzureDevOpsWorkProcessDto,
  AzureDevOpsWorkspaceDto,
} from '@/src/services/wayd-api'
import {
  useSetAzdoWorkspaceSyncMutation,
  useSyncAzdoConnectionOrganizationMutation,
} from '@/src/store/features/app-integration/azdo-integration-api'
import { caseInsensitiveCompare } from '@/src/components/common/wayd-grid'
import { isApiError, type ApiError } from '@/src/utils'
import { ExportOutlined } from '@ant-design/icons'
import {
  Alert,
  Button,
  Flex,
  Space,
  Switch,
  Table,
  TableColumnsType,
  Tag,
  Typography,
} from 'antd'
import Link from 'next/link'
import { useContext, useState } from 'react'
import { AzdoConnectionContext } from './connection-context'
import InitWorkspaceIntegrationForm from './init-workspace-integration-form'
import MapAzdoWorkspaceTeamsForm from './map-workspace-teams-form'

const { Text } = Typography

interface AzdoProjectsProps {
  workProcesses: AzureDevOpsWorkProcessDto[]
  workspaces: AzureDevOpsWorkspaceDto[]
  /** False when the connection's credentials failed their last check; Azure DevOps can't be read then. */
  isValidConfiguration: boolean
}

interface ProjectRow {
  project: AzureDevOpsWorkspaceDto
  process: AzureDevOpsWorkProcessDto | undefined
}

/**
 * The organization's Azure DevOps projects, where an admin picks which ones to sync. Setting up a
 * project sets up its process too, so processes are shown here only for information.
 */
const errorDetail = (error: unknown) => {
  const apiError: ApiError = isApiError(error) ? error : {}
  return apiError.detail ?? 'Unexpected error.'
}

const AzdoProjects = ({
  workProcesses,
  workspaces,
  isValidConfiguration,
}: AzdoProjectsProps) => {
  const azdoConnection = useContext(AzdoConnectionContext)
  const { hasPermissionClaim } = useAuth()
  const canUpdate = hasPermissionClaim('Permissions.Connections.Update')
  const messageApi = useMessage()

  const [setUpProject, setSetUpProject] = useState<ProjectRow | null>(null)
  const [teamsProject, setTeamsProject] =
    useState<AzureDevOpsWorkspaceDto | null>(null)
  // The requested sync state of each project whose switch was just flipped, held until the
  // reloaded project list shows it, so the switch neither snaps back nor stops spinning early.
  const [pendingSync, setPendingSync] = useState<Record<string, boolean>>({})

  const [setWorkspaceSync] = useSetAzdoWorkspaceSyncMutation()
  const [refreshProjects, { isLoading: isRefreshing }] =
    useSyncAzdoConnectionOrganizationMutation()

  const refresh = async () => {
    if (!azdoConnection) return
    const response = await refreshProjects(azdoConnection.connectionId)
    if (response.error) {
      messageApi.error(
        `Failed to refresh the projects. Error: ${errorDetail(response.error)}`,
      )
      return
    }
    messageApi.success('Refreshed the projects from Azure DevOps.')
  }

  const rows: ProjectRow[] = workspaces
    .map((project) => ({
      project,
      process: workProcesses.find(
        (p) => p.externalId === project.workProcessId,
      ),
    }))
    .sort((a, b) => caseInsensitiveCompare(a.project.name, b.project.name))

  const toggleSync = async (
    project: AzureDevOpsWorkspaceDto,
    isEnabled: boolean,
  ) => {
    if (!azdoConnection) return
    const id = project.externalId
    setPendingSync((pending) => ({ ...pending, [id]: isEnabled }))
    try {
      const response = await setWorkspaceSync({
        connectionId: azdoConnection.connectionId,
        workspaceExternalId: id,
        isEnabled,
      })
      if (response.error) throw response.error
      await azdoConnection.reloadConnectionData()
      messageApi.success(
        isEnabled
          ? `${project.name} syncs again from the next work sync.`
          : `${project.name} is paused. Its work items stay in Wayd.`,
      )
    } catch (error) {
      messageApi.error(
        `Failed to update ${project.name}. Error: ${errorDetail(error)}`,
      )
    } finally {
      setPendingSync((pending) =>
        Object.fromEntries(
          Object.entries(pending).filter(([key]) => key !== id),
        ),
      )
    }
  }

  const columns: TableColumnsType<ProjectRow> = [
    {
      title: 'Project',
      key: 'project',
      render: (_, { project }) => (
        <Flex vertical>
          <Space size={4}>
            <Text strong>{project.name}</Text>
            {azdoConnection?.organizationUrl && (
              <Link
                href={`${azdoConnection.organizationUrl}/${encodeURIComponent(project.name)}`}
                target="_blank"
                title="Open in Azure DevOps"
              >
                <ExportOutlined />
              </Link>
            )}
          </Space>
          {project.description && (
            <Text type="secondary">{project.description}</Text>
          )}
        </Flex>
      ),
    },
    {
      title: 'Process',
      key: 'process',
      render: (_, { process }) =>
        process ? (
          <Text>{process.name}</Text>
        ) : (
          <Text type="secondary">Unknown</Text>
        ),
    },
    {
      title: 'Sync',
      key: 'sync',
      render: (_, { project }) => {
        if (!project.integrationState) return <Tag>Not set up</Tag>
        return project.integrationState.isActive ? (
          <Tag color="success">Syncing</Tag>
        ) : (
          <Tag color="warning">Paused</Tag>
        )
      },
    },
    {
      title: 'Actions',
      key: 'actions',
      align: 'right',
      render: (_, row) => {
        const { project } = row
        if (!project.integrationState) {
          return (
            <Button
              size="small"
              type="primary"
              disabled={!canUpdate}
              onClick={() => setSetUpProject(row)}
            >
              Set Up
            </Button>
          )
        }
        return (
          <Space>
            <Button
              size="small"
              disabled={!canUpdate}
              onClick={() => setTeamsProject(project)}
            >
              Team Mappings
            </Button>
            <Switch
              size="small"
              checked={
                pendingSync[project.externalId] ??
                project.integrationState.isActive
              }
              loading={project.externalId in pendingSync}
              disabled={!canUpdate}
              onChange={(checked) => toggleSync(project, checked)}
              aria-label={`Sync ${project.name}`}
            />
          </Space>
        )
      },
    },
  ]

  const anySetUp = workspaces.some((w) => !!w.integrationState)

  return (
    <Flex vertical gap="middle">
      <Text>
        Choose the Azure DevOps projects to sync. Setting up a project also sets
        up its process in Wayd, and every sync keeps the process up to date.
      </Text>
      {workspaces.length === 0 ? (
        <Alert
          type="warning"
          showIcon
          title="No projects loaded yet."
          description="Load the organization's projects from Azure DevOps. The personal access token needs Work Items (Read) and Project and Team (Read)."
          action={
            canUpdate && (
              <Button
                size="small"
                loading={isRefreshing}
                disabled={!isValidConfiguration}
                title={
                  isValidConfiguration
                    ? undefined
                    : 'Fix the connection’s organization or personal access token first.'
                }
                onClick={refresh}
              >
                Refresh Projects
              </Button>
            )
          }
        />
      ) : (
        !anySetUp && (
          <Alert
            type="info"
            showIcon
            title="No projects are set up yet, so a sync has nothing to read."
          />
        )
      )}
      <Table<ProjectRow>
        size="small"
        rowKey={(row) => row.project.externalId}
        columns={columns}
        dataSource={rows}
        pagination={false}
        locale={{ emptyText: 'No projects found.' }}
      />
      {setUpProject && azdoConnection && (
        <InitWorkspaceIntegrationForm
          connectionId={azdoConnection.connectionId}
          externalId={setUpProject.project.externalId}
          workspaceName={setUpProject.project.name}
          organizationUrl={azdoConnection.organizationUrl}
          newProcessName={
            setUpProject.process && !setUpProject.process.integrationState
              ? setUpProject.process.name
              : undefined
          }
          onFormSave={() => setSetUpProject(null)}
          onFormCancel={() => setSetUpProject(null)}
        />
      )}
      {teamsProject && azdoConnection && (
        <MapAzdoWorkspaceTeamsForm
          connectionId={azdoConnection.connectionId}
          workspaceId={teamsProject.externalId}
          workspaceName={teamsProject.name}
          onFormSave={() => setTeamsProject(null)}
          onFormCancel={() => setTeamsProject(null)}
        />
      )}
    </Flex>
  )
}

export default AzdoProjects
