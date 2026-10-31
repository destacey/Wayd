import { act, fireEvent, render, screen, within } from '@testing-library/react'
import {
  AzureDevOpsWorkProcessDto,
  AzureDevOpsWorkspaceDto,
} from '@/src/services/wayd-api'
import { AzdoConnectionContext } from './connection-context'
import AzdoProjects from './projects'

const mockHasPermissionClaim = jest.fn()
const mockSuccess = jest.fn()
const mockError = jest.fn()
const mockSetWorkspaceSync = jest.fn()
const mockRefreshProjects = jest.fn()
const mockSetUpFormProps = jest.fn()

jest.mock('@/src/components/contexts/auth', () => ({
  __esModule: true,
  default: () => ({ hasPermissionClaim: mockHasPermissionClaim }),
}))

jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ success: mockSuccess, error: mockError }),
}))

jest.mock('@/src/store/features/app-integration/azdo-integration-api', () => ({
  useSetAzdoWorkspaceSyncMutation: () => [mockSetWorkspaceSync],
  useSyncAzdoConnectionOrganizationMutation: () => [
    mockRefreshProjects,
    { isLoading: false },
  ],
}))

// The dialogs are covered on their own; here only what the tab hands them is under test.
jest.mock('./init-workspace-integration-form', () => ({
  __esModule: true,
  default: (props: unknown) => {
    mockSetUpFormProps(props)
    return null
  },
}))
jest.mock('./map-workspace-teams-form', () => ({
  __esModule: true,
  default: () => null,
}))

const agile: AzureDevOpsWorkProcessDto = {
  externalId: 'process-agile',
  name: 'Agile',
  integrationState: { internalId: 'wayd-agile', isActive: true },
}
const scrum: AzureDevOpsWorkProcessDto = {
  externalId: 'process-scrum',
  name: 'Scrum',
}

const project = (
  name: string,
  process: AzureDevOpsWorkProcessDto,
  syncState: 'not-set-up' | 'syncing' | 'paused',
): AzureDevOpsWorkspaceDto => ({
  externalId: `project-${name}`,
  name,
  workProcessId: process.externalId,
  integrationState:
    syncState === 'not-set-up'
      ? undefined
      : { internalId: `wayd-${name}`, isActive: syncState === 'syncing' },
})

const reloadConnectionData = jest.fn()

const renderProjects = (
  workspaces: AzureDevOpsWorkspaceDto[],
  isValidConfiguration = true,
) =>
  render(
    <AzdoConnectionContext.Provider
      value={{
        connectionId: 'connection-1',
        organizationUrl: 'https://dev.azure.com/acme',
        reloadConnectionData: reloadConnectionData as never,
      }}
    >
      <AzdoProjects
        workProcesses={[agile, scrum]}
        workspaces={workspaces}
        isValidConfiguration={isValidConfiguration}
      />
    </AzdoConnectionContext.Provider>,
  )

const rowOf = (name: string) => screen.getByText(name).closest('tr')!

describe('AzdoProjects', () => {
  beforeEach(() => {
    jest.clearAllMocks()
    mockHasPermissionClaim.mockReturnValue(true)
    reloadConnectionData.mockResolvedValue(undefined)
  })

  it('shows each project with its process and sync state', () => {
    // Arrange / Act
    renderProjects([
      project('Mobile', agile, 'syncing'),
      project('Web', agile, 'paused'),
      project('Data', scrum, 'not-set-up'),
    ])

    // Assert
    expect(within(rowOf('Mobile')).getByText('Syncing')).toBeInTheDocument()
    expect(within(rowOf('Web')).getByText('Paused')).toBeInTheDocument()
    expect(within(rowOf('Data')).getByText('Not set up')).toBeInTheDocument()
    expect(within(rowOf('Data')).getByText('Scrum')).toBeInTheDocument()
    expect(
      within(rowOf('Data')).getByRole('button', { name: 'Set Up' }),
    ).toBeInTheDocument()
    expect(
      within(rowOf('Mobile')).queryByRole('button', { name: 'Set Up' }),
    ).not.toBeInTheDocument()
  })

  it('tells the set-up dialog when the project brings a process Wayd does not have', () => {
    // Arrange
    renderProjects([
      project('Data', scrum, 'not-set-up'),
      project('Mobile', agile, 'not-set-up'),
    ])

    // Act
    fireEvent.click(
      within(rowOf('Data')).getByRole('button', { name: 'Set Up' }),
    )

    // Assert
    expect(mockSetUpFormProps).toHaveBeenLastCalledWith(
      expect.objectContaining({
        workspaceName: 'Data',
        newProcessName: 'Scrum',
      }),
    )
  })

  it('names no new process when the project uses one Wayd already has', () => {
    // Arrange
    renderProjects([project('Mobile', agile, 'not-set-up')])

    // Act
    fireEvent.click(screen.getByRole('button', { name: 'Set Up' }))

    // Assert
    expect(mockSetUpFormProps).toHaveBeenLastCalledWith(
      expect.objectContaining({ newProcessName: undefined }),
    )
  })

  it('pauses a project and reloads the list before confirming', async () => {
    // Arrange
    mockSetWorkspaceSync.mockResolvedValue({ data: undefined })
    renderProjects([project('Mobile', agile, 'syncing')])

    // Act
    await act(async () => {
      fireEvent.click(screen.getByRole('switch', { name: 'Sync Mobile' }))
    })

    // Assert
    expect(mockSetWorkspaceSync).toHaveBeenCalledWith({
      connectionId: 'connection-1',
      workspaceExternalId: 'project-Mobile',
      isEnabled: false,
    })
    expect(reloadConnectionData).toHaveBeenCalled()
    expect(mockSuccess).toHaveBeenCalledWith(
      'Mobile is paused. Its work items stay in Wayd.',
    )
  })

  it('reports a failed toggle with the API detail', async () => {
    // Arrange
    mockSetWorkspaceSync.mockResolvedValue({
      error: { status: 400, detail: 'Workspace Mobile has not been set up.' },
    })
    renderProjects([project('Mobile', agile, 'syncing')])

    // Act
    await act(async () => {
      fireEvent.click(screen.getByRole('switch', { name: 'Sync Mobile' }))
    })

    // Assert
    expect(mockError).toHaveBeenCalledWith(
      'Failed to update Mobile. Error: Workspace Mobile has not been set up.',
    )
    expect(reloadConnectionData).not.toHaveBeenCalled()
  })

  it('disables Refresh Projects while the connection configuration is invalid', () => {
    // Arrange / Act
    renderProjects([], false)

    // Assert
    expect(
      screen.getByRole('button', { name: 'Refresh Projects' }),
    ).toBeDisabled()
  })

  it('confirms a successful refresh', async () => {
    // Arrange
    mockRefreshProjects.mockResolvedValue({ data: undefined })
    renderProjects([])

    // Act
    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: 'Refresh Projects' }))
    })

    // Assert
    expect(mockRefreshProjects).toHaveBeenCalledWith('connection-1')
    expect(mockSuccess).toHaveBeenCalledWith(
      'Refreshed the projects from Azure DevOps.',
    )
  })

  it('lets a viewer without update permission change nothing', () => {
    // Arrange
    mockHasPermissionClaim.mockReturnValue(false)

    // Act
    renderProjects([
      project('Mobile', agile, 'syncing'),
      project('Data', scrum, 'not-set-up'),
    ])

    // Assert
    expect(screen.getByRole('button', { name: 'Set Up' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Team Mappings' })).toBeDisabled()
    expect(screen.getByRole('switch', { name: 'Sync Mobile' })).toBeDisabled()
  })
})
