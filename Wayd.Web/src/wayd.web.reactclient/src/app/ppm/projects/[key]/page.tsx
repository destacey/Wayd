'use client'

import { LifecycleStatusTag, PageActions } from '@/src/components/common'
import { RecordLayout, RecordSection } from '@/src/components/common/record'
import useAuth from '@/src/components/contexts/auth'
import { authorizePage } from '@/src/components/hoc'
import { useDocumentTitle } from '@/src/hooks'
import {
  useGetProjectQuery,
  useGetProjectWorkItemsQuery,
} from '@/src/store/features/ppm/projects-api'
import { Alert, Button, Flex, MenuProps, Spin, Tooltip } from 'antd'
import { ItemType } from 'antd/es/menu/interface'
import dynamic from 'next/dynamic'
import { notFound, useRouter, useSearchParams } from 'next/navigation'
import { Suspense, use, useState } from 'react'
import {
  ChangeProjectKeyForm,
  ChangeProjectProgramForm,
  ChangeProjectStatusForm,
  CreateProjectHealthCheckForm,
  DeleteProjectForm,
  EditProjectForm,
  ProjectHealthCheckTag,
  ProjectStatusHistoryModal,
  RevertProjectStatusForm,
} from '@/src/app/ppm/projects/_components'
import AssignProjectLifecycleForm from '@/src/app/ppm/projects/_components/assign-project-lifecycle-form'
import ChangeProjectLifecycleForm from '@/src/app/ppm/projects/_components/change-project-lifecycle-form'
import { ProjectStatusAction } from '@/src/app/ppm/projects/_components/change-project-status-form'
import ProjectDetailsLoading from './loading'
import ProjectDefinition from './_components/project-definition'
import ProjectFacts from './_components/project-facts'
import ProjectOverview from './_components/project-overview'
import { canActOnPpmRecord } from '../../_components/ppm-authorization'
import ProjectTaskMetricsInline from '@/src/app/ppm/projects/_components/project-task-metrics-inline'

const ProjectPlan = dynamic(
  () => import('@/src/app/ppm/projects/_components/project-plan'),
  { ssr: false, loading: () => <Spin /> },
)

const ProjectTeamGrid = dynamic(
  () => import('@/src/app/ppm/projects/_components/project-team-grid'),
  { ssr: false, loading: () => <Spin /> },
)

const ProjectWorkItemsViewManager = dynamic(
  () =>
    import(
      '@/src/app/ppm/projects/_components/project-work-items-view-manager'
    ),
  { ssr: false, loading: () => <Spin /> },
)

const ProjectHealthReport = dynamic(
  () => import('./_components/project-health-report'),
  { ssr: false, loading: () => <Spin /> },
)

enum ProjectSections {
  Overview = 'overview',
  Definition = 'definition',
  Team = 'team',
  Plan = 'plan',
  WorkItems = 'work-items',
  HealthReport = 'health-report',
}

const reports: RecordSection[] = [
  { id: ProjectSections.HealthReport, label: 'Health Report' },
]

enum ProjectAction {
  Edit = 'Edit',
  AssignLifecycle = 'Assign Lifecycle',
  ChangeLifecycle = 'Change Lifecycle',
  ChangeProgram = 'Change Program',
  ChangeKey = 'Change Key',
  Delete = 'Delete',
  Approve = 'Approve',
  Activate = 'Activate',
  Complete = 'Complete',
  Cancel = 'Cancel',
  RevertStatus = 'Revert Status',
}

const ProjectDetailsPage = (props: { params: Promise<{ key: string }> }) => {
  const { key: projectKey } = use(props.params)

  const [openEditProjectForm, setOpenEditProjectForm] = useState<boolean>(false)
  const [openChangeProgramForm, setOpenChangeProgramForm] =
    useState<boolean>(false)
  const [openChangeKeyForm, setOpenChangeKeyForm] = useState<boolean>(false)
  const [openApproveProjectForm, setOpenApproveProjectForm] =
    useState<boolean>(false)
  const [openActivateProjectForm, setOpenActivateProjectForm] =
    useState<boolean>(false)
  const [openCompleteProjectForm, setOpenCompleteProjectForm] =
    useState<boolean>(false)
  const [openCancelProjectForm, setOpenCancelProjectForm] =
    useState<boolean>(false)
  const [openDeleteProjectForm, setOpenDeleteProjectForm] =
    useState<boolean>(false)
  const [openAssignLifecycleForm, setOpenAssignLifecycleForm] =
    useState<boolean>(false)
  const [openChangeLifecycleForm, setOpenChangeLifecycleForm] =
    useState<boolean>(false)
  const [openCreateHealthCheckForm, setOpenCreateHealthCheckForm] =
    useState<boolean>(false)
  const [openStatusHistory, setOpenStatusHistory] = useState<boolean>(false)
  const [openRevertStatusForm, setOpenRevertStatusForm] =
    useState<boolean>(false)

  const router = useRouter()

  const { hasPermissionClaim } = useAuth()
  const canUpdateProject = hasPermissionClaim('Permissions.Projects.Update')
  const canDeleteProject = hasPermissionClaim('Permissions.Projects.Delete')

  // The active section lives in the URL, owned by RecordLayout. Read here only
  // to hold back the work items query, which is the expensive one.
  const searchParams = useSearchParams()
  const activeSection = (searchParams.get('section') ??
    ProjectSections.Overview) as ProjectSections

  const {
    data: projectData,
    isLoading,
    refetch: refetchProject,
  } = useGetProjectQuery(projectKey)

  // Managing a project needs the Update permission AND delivery leadership on it — project, program, or
  // portfolio Owner/Manager, or the PPM administrator grant. The server computes the membership half
  // (canManageProject) so the UI cannot drift from the rule the aggregate enforces.
  const canManageProject = canActOnPpmRecord(
    canUpdateProject,
    projectData?.canManageProject,
  )

  // Deleting takes the same leadership, paired with its own claim rather than
  // Update's.
  const canDelete = canActOnPpmRecord(
    canDeleteProject,
    projectData?.canManageProject,
  )

  useDocumentTitle(`${projectData?.name ?? projectKey} - Project Details`)

  const {
    data: workItemsData,
    isLoading: workItemsDataIsLoading,
    refetch: refetchWorkItemsData,
  } = useGetProjectWorkItemsQuery(projectData?.id ?? '', {
    skip: !projectData?.id || activeSection !== ProjectSections.WorkItems,
  })

  const missingDates = projectData?.start === null || projectData?.end === null
  const missingLifecycle = !projectData?.projectLifecycle

  const actionsMenuItems: MenuProps['items'] = (() => {
    const currentStatus = projectData?.status.name
    const availableActions =
      currentStatus === 'Proposed'
        ? !missingDates && !missingLifecycle
          ? [
              ProjectAction.Edit,
              ProjectAction.Delete,
              ProjectAction.Approve,
              ProjectAction.Activate,
              ProjectAction.Cancel,
            ]
          : missingLifecycle
            ? [ProjectAction.Edit, ProjectAction.Delete, ProjectAction.Cancel]
            : [
                ProjectAction.Edit,
                ProjectAction.Delete,
                ProjectAction.Approve,
                ProjectAction.Cancel,
              ]
        : currentStatus === 'Approved'
          ? !missingDates
            ? [ProjectAction.Edit, ProjectAction.Activate, ProjectAction.Cancel]
            : [ProjectAction.Edit, ProjectAction.Cancel]
          : currentStatus === 'Active'
            ? [ProjectAction.Edit, ProjectAction.Complete, ProjectAction.Cancel]
            : []

    // TODO: Implement On Hold status

    const items: ItemType[] = []
    if (canManageProject && availableActions.includes(ProjectAction.Edit)) {
      items.push({
        key: 'edit',
        label: ProjectAction.Edit,
        onClick: () => setOpenEditProjectForm(true),
      })
    }
    if (canManageProject) {
      items.push({
        key: 'change-program',
        label: ProjectAction.ChangeProgram,
        onClick: () => setOpenChangeProgramForm(true),
      })
      items.push({
        key: 'change-key',
        label: ProjectAction.ChangeKey,
        onClick: () => setOpenChangeKeyForm(true),
      })
      if (currentStatus !== 'Canceled') {
        if (!projectData?.projectLifecycle) {
          items.push({
            key: 'assign-lifecycle',
            label: ProjectAction.AssignLifecycle,
            onClick: () => setOpenAssignLifecycleForm(true),
          })
        } else {
          items.push({
            key: 'change-lifecycle',
            label: ProjectAction.ChangeLifecycle,
            onClick: () => setOpenChangeLifecycleForm(true),
          })
        }
      }
    }

    if (canDelete && availableActions.includes(ProjectAction.Delete)) {
      items.push({
        key: 'delete',
        label: ProjectAction.Delete,
        danger: true,
        onClick: () => setOpenDeleteProjectForm(true),
      })
    }

    // Server-decided, so this cannot offer a transition the aggregate would reject.
    const canRevertStatus =
      canManageProject && (projectData?.backwardStatusTargets?.length ?? 0) > 0

    if (
      canManageProject &&
      (availableActions.includes(ProjectAction.Approve) ||
        availableActions.includes(ProjectAction.Activate) ||
        availableActions.includes(ProjectAction.Complete) ||
        availableActions.includes(ProjectAction.Cancel) ||
        canRevertStatus)
    ) {
      items.push({ key: 'manage-divider', type: 'divider' })
    }

    if (canManageProject && availableActions.includes(ProjectAction.Approve)) {
      items.push({
        key: 'approve',
        label: ProjectAction.Approve,
        onClick: () => setOpenApproveProjectForm(true),
      })
    }

    if (canManageProject && availableActions.includes(ProjectAction.Activate)) {
      items.push({
        key: 'activate',
        label: ProjectAction.Activate,
        onClick: () => setOpenActivateProjectForm(true),
      })
    }

    if (canManageProject && availableActions.includes(ProjectAction.Complete)) {
      items.push({
        key: 'complete',
        label: ProjectAction.Complete,
        onClick: () => setOpenCompleteProjectForm(true),
      })
    }

    if (canManageProject && availableActions.includes(ProjectAction.Cancel)) {
      items.push({
        key: 'cancel',
        label: ProjectAction.Cancel,
        onClick: () => setOpenCancelProjectForm(true),
      })
    }

    if (canRevertStatus) {
      items.push({
        key: 'revert-status',
        label: ProjectAction.RevertStatus,
        onClick: () => setOpenRevertStatusForm(true),
      })
    }

    items.push({ key: 'other-divider', type: 'divider' })

    items.push({
      key: 'status-history',
      label: 'Status History',
      onClick: () => setOpenStatusHistory(true),
    })

    return items
  })()

  const onEditProjectFormClosed = (wasSaved: boolean) => {
    setOpenEditProjectForm(false)
    if (wasSaved) refetchProject()
  }

  const onAssignLifecycleFormClosed = (wasSaved: boolean) => {
    setOpenAssignLifecycleForm(false)
    if (wasSaved) refetchProject()
  }

  const onChangeLifecycleFormClosed = (wasSaved: boolean) => {
    setOpenChangeLifecycleForm(false)
    if (wasSaved) refetchProject()
  }

  const onChangeProgramFormClosed = (wasSaved: boolean) => {
    setOpenChangeProgramForm(false)
    if (wasSaved) refetchProject()
  }

  const onChangeKeyFormClosed = (wasSaved: boolean, newKey?: string) => {
    setOpenChangeKeyForm(false)
    if (wasSaved) {
      if (newKey && newKey !== projectData?.key) {
        router.push(`/ppm/projects/${newKey}`)
        return
      }
      refetchProject()
    }
  }

  const onApproveProjectFormClosed = (wasSaved: boolean) => {
    setOpenApproveProjectForm(false)
    if (wasSaved) refetchProject()
  }

  const onActivateProjectFormClosed = (wasSaved: boolean) => {
    setOpenActivateProjectForm(false)
    if (wasSaved) refetchProject()
  }

  const onCompleteProjectFormClosed = (wasSaved: boolean) => {
    setOpenCompleteProjectForm(false)
    if (wasSaved) refetchProject()
  }

  const onCancelProjectFormClosed = (wasSaved: boolean) => {
    setOpenCancelProjectForm(false)
    if (wasSaved) refetchProject()
  }

  const onRevertStatusFormClosed = (wasSaved: boolean) => {
    setOpenRevertStatusForm(false)
    if (wasSaved) refetchProject()
  }

  const onDeleteProjectFormClosed = (wasDeleted: boolean) => {
    setOpenDeleteProjectForm(false)
    if (wasDeleted) router.push('/ppm/projects')
  }

  const onCreateHealthCheckFormClosed = (wasSaved: boolean) => {
    setOpenCreateHealthCheckForm(false)
    if (wasSaved) refetchProject()
  }

  if (isLoading) {
    return <ProjectDetailsLoading />
  }

  if (!projectData) {
    return notFound()
  }

  // Plan needs a lifecycle to lay stages out against, so it appears only once
  // one is assigned.
  const sections: RecordSection[] = [
    { id: ProjectSections.Overview, label: 'Overview' },
    { id: ProjectSections.Definition, label: 'Definition' },
    { id: ProjectSections.Team, label: 'Team' },
    ...(projectData.projectLifecycle
      ? [{ id: ProjectSections.Plan, label: 'Plan' }]
      : []),
    { id: ProjectSections.WorkItems, label: 'Work Items' },
  ]

  // Closed statuses are included because revert targets depend on the lifecycle and dates too — a
  // project cancelled from Proposed has neither, and without this the Revert Status action is absent
  // with nothing explaining why.
  const showSetupWarnings =
    projectData.status.name === 'Proposed' ||
    projectData.status.name === 'Approved' ||
    projectData.status.name === 'Completed' ||
    projectData.status.name === 'Canceled'

  const setupWarnings = !showSetupWarnings ? null : (
    <>
      {missingDates && (
        <Alert
          title="Project Dates are required before activating."
          type="warning"
          showIcon
        />
      )}
      {missingLifecycle && (
        <Alert
          title="A Project Lifecycle is required before approving."
          type="warning"
          showIcon
        />
      )}
    </>
  )

  const renderSection = (section: ProjectSections) => {
    switch (section) {
      case ProjectSections.Definition:
        return <ProjectDefinition project={projectData} />
      case ProjectSections.Team:
        return <ProjectTeamGrid projectIdOrKey={projectKey} />
      case ProjectSections.Plan:
        return (
          <ProjectPlan project={projectData} canManageTasks={canUpdateProject} />
        )
      case ProjectSections.WorkItems:
        return (
          <ProjectWorkItemsViewManager
            workItems={workItemsData ?? []}
            isLoading={workItemsDataIsLoading}
            refetch={refetchWorkItemsData}
            hideProjectColumn={true}
          />
        )
      case ProjectSections.HealthReport:
        return <ProjectHealthReport projectId={projectData.id} />
      default:
        return (
          <Flex vertical gap="middle">
            {setupWarnings}
            <ProjectOverview project={projectData} />
          </Flex>
        )
    }
  }

  return (
    <>
      <RecordLayout
        sections={sections}
        reports={reports}
        defaultSection={ProjectSections.Overview}
        record={{
          name: projectData.name,
          recordKey: projectData.key,
          parent: { label: 'Projects', href: '/ppm/projects' },
          subtitle: 'Project Details',
          tags: (
            <Flex gap="small" align="center" wrap>
              <Tooltip title="View status history">
                <span
                  role="button"
                  tabIndex={0}
                  style={{ cursor: 'pointer' }}
                  onClick={() => setOpenStatusHistory(true)}
                  onKeyDown={(e) => {
                    if (e.key === 'Enter' || e.key === ' ') {
                      e.preventDefault()
                      setOpenStatusHistory(true)
                    }
                  }}
                >
                  <LifecycleStatusTag status={projectData.status} />
                </span>
              </Tooltip>
              <ProjectHealthCheckTag
                healthCheck={projectData.healthCheck}
                projectId={projectData.id}
              />
            </Flex>
          ),
          actions: (
            <>
              {/* Recording health is the recurring action on a live project,
                  so it sits out of the menu. Same gate as the menu item it
                  replaces: the permission alone cannot record one. */}
              {canManageProject && (
                <Button onClick={() => setOpenCreateHealthCheckForm(true)}>
                  Create Health Check
                </Button>
              )}
              <PageActions actionItems={actionsMenuItems} />
            </>
          ),
        }}
        facts={<ProjectFacts project={projectData} />}
        sectionActions={
          activeSection === ProjectSections.Plan ? (
            <ProjectTaskMetricsInline projectKey={projectKey} />
          ) : null
        }
      >
        {(section) => renderSection(section as ProjectSections)}
      </RecordLayout>

      {openEditProjectForm && (
        <EditProjectForm
          projectKey={projectData.key}
          onFormComplete={() => onEditProjectFormClosed(true)}
          onFormCancel={() => onEditProjectFormClosed(false)}
        />
      )}
      {openChangeProgramForm && (
        <ChangeProjectProgramForm
          project={projectData}
          onFormComplete={() => onChangeProgramFormClosed(true)}
          onFormCancel={() => onChangeProgramFormClosed(false)}
        />
      )}
      {openChangeKeyForm && (
        <ChangeProjectKeyForm
          projectKey={projectData.key}
          onFormComplete={(newKey) => onChangeKeyFormClosed(true, newKey)}
          onFormCancel={() => onChangeKeyFormClosed(false)}
        />
      )}
      {openApproveProjectForm && (
        <ChangeProjectStatusForm
          project={projectData}
          statusAction={ProjectStatusAction.Approve}
          onFormComplete={() => onApproveProjectFormClosed(true)}
          onFormCancel={() => onApproveProjectFormClosed(false)}
        />
      )}
      {openActivateProjectForm && (
        <ChangeProjectStatusForm
          project={projectData}
          statusAction={ProjectStatusAction.Activate}
          onFormComplete={() => onActivateProjectFormClosed(true)}
          onFormCancel={() => onActivateProjectFormClosed(false)}
        />
      )}
      {openCompleteProjectForm && (
        <ChangeProjectStatusForm
          project={projectData}
          statusAction={ProjectStatusAction.Complete}
          onFormComplete={() => onCompleteProjectFormClosed(true)}
          onFormCancel={() => onCompleteProjectFormClosed(false)}
        />
      )}
      {openCancelProjectForm && (
        <ChangeProjectStatusForm
          project={projectData}
          statusAction={ProjectStatusAction.Cancel}
          onFormComplete={() => onCancelProjectFormClosed(true)}
          onFormCancel={() => onCancelProjectFormClosed(false)}
        />
      )}
      {openRevertStatusForm && (
        <RevertProjectStatusForm
          project={projectData}
          onFormComplete={() => onRevertStatusFormClosed(true)}
          onFormCancel={() => onRevertStatusFormClosed(false)}
        />
      )}
      {openDeleteProjectForm && (
        <DeleteProjectForm
          project={projectData}
          onFormComplete={() => onDeleteProjectFormClosed(true)}
          onFormCancel={() => onDeleteProjectFormClosed(false)}
        />
      )}
      {openAssignLifecycleForm && (
        <AssignProjectLifecycleForm
          project={projectData}
          onFormComplete={() => onAssignLifecycleFormClosed(true)}
          onFormCancel={() => onAssignLifecycleFormClosed(false)}
        />
      )}
      {openChangeLifecycleForm && (
        <ChangeProjectLifecycleForm
          project={projectData}
          onFormComplete={() => onChangeLifecycleFormClosed(true)}
          onFormCancel={() => onChangeLifecycleFormClosed(false)}
        />
      )}
      {openCreateHealthCheckForm && (
        <CreateProjectHealthCheckForm
          projectId={projectData.id}
          onFormCreate={() => onCreateHealthCheckFormClosed(true)}
          onFormCancel={() => onCreateHealthCheckFormClosed(false)}
        />
      )}
      <ProjectStatusHistoryModal
        projectId={projectData.id}
        isOpen={openStatusHistory}
        onClose={() => setOpenStatusHistory(false)}
      />
    </>
  )
}

// useSearchParams suspends a prerendered route up to the nearest boundary. In
// development routes render on demand, so a missing one only fails the
// production build.
const ProjectDetailsPageWithSuspense = (props: {
  params: Promise<{ key: string }>
}) => (
  <Suspense fallback={<ProjectDetailsLoading />}>
    <ProjectDetailsPage {...props} />
  </Suspense>
)

const ProjectDetailsPageWithAuthorization = authorizePage(
  ProjectDetailsPageWithSuspense,
  'Permission',
  'Permissions.Projects.View',
)

export default ProjectDetailsPageWithAuthorization
