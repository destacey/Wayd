'use client'

import { PageTitle } from '@/src/components/common'
import useAuth from '@/src/components/contexts/auth'
import { authorizePage } from '@/src/components/hoc'
import { useDocumentTitle, useLocalStorageState } from '@/src/hooks'
import { useGetProjectsQuery } from '@/src/store/features/ppm/projects-api'
import { Button } from 'antd'
import { FC, useEffect, useState } from 'react'
import { CreateProjectForm } from './_components'
import { ProjectsFilterBar, ProjectsGrid } from '../_components'
import { useMessage } from '@/src/components/contexts/messaging'

// Project status enum values matching the backend
const PROJECT_STATUS = {
  Proposed: 1,
  Approved: 5,
  Active: 2,
  Completed: 3,
  Cancelled: 4,
} as const

const DEFAULT_STATUSES = [PROJECT_STATUS.Approved, PROJECT_STATUS.Active]

const ALL_ROLES = [1, 2, 3, 4, 5]

const getRoleFilterValues = (
  selectedRole: string | undefined,
): number[] | undefined => {
  if (!selectedRole) return undefined
  if (selectedRole === 'all') return ALL_ROLES
  return [parseInt(selectedRole)]
}

const ProjectsPage: FC = () => {
  useDocumentTitle('Projects')
  const [openCreateProjectForm, setOpenCreateProjectForm] =
    useState<boolean>(false)
  const [selectedStatuses, setSelectedStatuses] = useLocalStorageState<
    number[]
  >('projects-filter-statuses', DEFAULT_STATUSES)
  const [selectedPortfolioId, setSelectedPortfolioId] = useLocalStorageState<
    string | null
  >('projects-filter-portfolio', null)
  const [selectedRole, setSelectedRole] = useLocalStorageState<
    string | null
  >('projects-filter-role', null)
  const messageApi = useMessage()

  const { hasPermissionClaim } = useAuth()
  const canCreateProject = hasPermissionClaim('Permissions.Projects.Create')
  const showActions = canCreateProject

  const {
    data: projectData,
    isLoading,
    error,
    refetch,
  } = useGetProjectsQuery({
    status: selectedStatuses.length > 0 ? selectedStatuses : undefined,
    portfolioId: selectedPortfolioId ?? undefined,
    role: getRoleFilterValues(selectedRole ?? undefined),
  })

  useEffect(() => {
    if (error) {
      console.error(error)
      messageApi.error('Failed to load projects.')
    }
  }, [error, messageApi])

  const handleResetFilters = () => {
    setSelectedStatuses(DEFAULT_STATUSES)
    setSelectedPortfolioId(null)
    setSelectedRole(null)
  }

  const actions = !showActions ? null : (
      <>
        {canCreateProject && (
          <Button onClick={() => setOpenCreateProjectForm(true)}>
            Create Project
          </Button>
        )}
      </>
    )

  const onCreateProjectFormClosed = (wasCreated: boolean) => {
    setOpenCreateProjectForm(false)
    if (wasCreated) {
      refetch()
    }
  }

  return (
    <>
      <PageTitle title="Projects" actions={actions} />
      <ProjectsFilterBar
        selectedStatuses={selectedStatuses}
        onStatusChange={setSelectedStatuses}
        selectedPortfolioId={selectedPortfolioId}
        onPortfolioChange={setSelectedPortfolioId}
        selectedRole={selectedRole}
        onRoleChange={setSelectedRole}
        onReset={handleResetFilters}
      />
      <ProjectsGrid
        projects={projectData ?? []}
        isLoading={isLoading}
        refetch={refetch}
        persistStateKey="ppm-projects"
      />
      {openCreateProjectForm && (
        <CreateProjectForm
          onFormComplete={() => onCreateProjectFormClosed(true)}
          onFormCancel={() => onCreateProjectFormClosed(false)}
        />
      )}
    </>
  )
}

const ProjectsPageWithAuthorization = authorizePage(
  ProjectsPage,
  'Permission',
  'Permissions.Projects.View',
)

export default ProjectsPageWithAuthorization
