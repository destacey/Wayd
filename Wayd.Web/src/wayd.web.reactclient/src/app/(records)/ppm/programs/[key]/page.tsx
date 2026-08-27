'use client'

import { LifecycleStatusTag, PageActions } from '@/src/components/common'
import { RecordLayout, RecordSection } from '@/src/components/common/record'
import useAuth from '@/src/components/contexts/auth'
import { authorizePage } from '@/src/components/hoc'
import { useDocumentTitle } from '@/src/hooks'
import {
  useGetProgramProjectsQuery,
  useGetProgramQuery,
} from '@/src/store/features/ppm/programs-api'
import { Alert, Flex, MenuProps } from 'antd'
import { ItemType } from 'antd/es/menu/interface'
import { notFound, useRouter, useSearchParams } from 'next/navigation'
import { Suspense, use, useState } from 'react'
import {
  ChangeProgramStatusForm,
  DeleteProgramForm,
  EditProgramForm,
} from '@/src/app/(legacy)/ppm/programs/_components'
import { ProgramStatusAction } from '@/src/app/(legacy)/ppm/programs/_components/change-program-status-form'
import {
  ProjectsFilterBar,
  ProjectViewManager,
} from '@/src/app/(legacy)/ppm/_components'
import { useStatusFilter } from '../../_components/use-status-filter'
import ProgramDetailsLoading from './loading'
import ProgramFacts from './_components/program-facts'
import ProgramOverview from './_components/program-overview'

enum ProgramSections {
  Overview = 'overview',
  Projects = 'projects',
}

const sections: RecordSection[] = [
  { id: ProgramSections.Overview, label: 'Overview' },
  { id: ProgramSections.Projects, label: 'Projects' },
]

/** Approved(5), Active(2) — what a program's delivery is usually about. */
const DEFAULT_PROJECT_STATUSES = [5, 2]

enum ProgramAction {
  Edit = 'Edit',
  Delete = 'Delete',
  Activate = 'Activate',
  Complete = 'Complete',
  Cancel = 'Cancel',
}

const ProgramDetailsPage = (props: { params: Promise<{ key: string }> }) => {
  const { key } = use(props.params)
  const programKey = Number(key)

  const [openEditProgramForm, setOpenEditProgramForm] = useState<boolean>(false)
  const [openActivateProgramForm, setOpenActivateProgramForm] =
    useState<boolean>(false)
  const [openCompleteProgramForm, setOpenCompleteProgramForm] =
    useState<boolean>(false)
  const [openCancelProgramForm, setOpenCancelProgramForm] =
    useState<boolean>(false)
  const [openDeleteProgramForm, setOpenDeleteProgramForm] =
    useState<boolean>(false)

  const router = useRouter()
  const searchParams = useSearchParams()

  const { hasPermissionClaim } = useAuth()
  const canUpdateProgram = hasPermissionClaim('Permissions.Programs.Update')
  const canDeleteProgram = hasPermissionClaim('Permissions.Programs.Delete')

  // Shared by the overview's tiles and charts and by the Projects section, so
  // a count and the list it links to can never disagree. Keyed by program, so
  // filtering one does not change what another opens on.
  const { selected: projectStatuses, setSelected: setProjectStatuses } =
    useStatusFilter(
      `program:${programKey}:projectStatus`,
      DEFAULT_PROJECT_STATUSES,
    )

  const {
    data: programData,
    isLoading,
    refetch: refetchProgram,
  } = useGetProgramQuery(programKey)

  const {
    data: projectsData,
    isLoading: projectsDataIsLoading,
    refetch: refetchProjectsData,
  } = useGetProgramProjectsQuery(
    {
      programIdOrKey: programKey.toString(),
      status: projectStatuses.length > 0 ? projectStatuses : undefined,
    },
    { skip: !programData },
  )

  useDocumentTitle(`${programData?.name ?? programKey} - Program Details`)

  // Managing a program needs the Update permission AND delivery leadership on it — program or parent
  // portfolio Owner/Manager, or the PPM administrator grant. The server computes the membership half
  // (canManageProgram) so the UI cannot drift from the rule the aggregate enforces.
  const canManageProgram = canUpdateProgram && !!programData?.canManageProgram

  const missingDates = programData?.start === null || programData?.end === null

  const actionsMenuItems: MenuProps['items'] = (() => {
    const currentStatus = programData?.status.name
    const availableActions =
      currentStatus === 'Proposed'
        ? !missingDates
          ? [
              ProgramAction.Edit,
              ProgramAction.Delete,
              ProgramAction.Activate,
              ProgramAction.Cancel,
            ]
          : [ProgramAction.Edit, ProgramAction.Delete, ProgramAction.Cancel]
        : currentStatus === 'Active'
          ? [ProgramAction.Edit, ProgramAction.Complete, ProgramAction.Cancel]
          : []

    const items: ItemType[] = []
    if (canManageProgram && availableActions.includes(ProgramAction.Edit)) {
      items.push({
        key: 'edit',
        label: ProgramAction.Edit,
        onClick: () => setOpenEditProgramForm(true),
      })
    }
    if (canDeleteProgram && availableActions.includes(ProgramAction.Delete)) {
      items.push({
        key: 'delete',
        label: ProgramAction.Delete,
        onClick: () => setOpenDeleteProgramForm(true),
      })
    }

    if (
      canManageProgram &&
      (availableActions.includes(ProgramAction.Activate) ||
        availableActions.includes(ProgramAction.Complete) ||
        availableActions.includes(ProgramAction.Cancel))
    ) {
      items.push({
        key: 'manage-divider',
        type: 'divider',
      })
    }

    if (canManageProgram && availableActions.includes(ProgramAction.Activate)) {
      items.push({
        key: 'activate',
        label: ProgramAction.Activate,
        onClick: () => setOpenActivateProgramForm(true),
      })
    }

    if (canManageProgram && availableActions.includes(ProgramAction.Complete)) {
      items.push({
        key: 'complete',
        label: ProgramAction.Complete,
        onClick: () => setOpenCompleteProgramForm(true),
      })
    }

    if (canManageProgram && availableActions.includes(ProgramAction.Cancel)) {
      items.push({
        key: 'cancel',
        label: ProgramAction.Cancel,
        onClick: () => setOpenCancelProgramForm(true),
      })
    }

    return items
  })()

  const onEditProgramFormClosed = (wasSaved: boolean) => {
    setOpenEditProgramForm(false)
    if (wasSaved) {
      refetchProgram()
    }
  }

  const onActivateProgramFormClosed = (wasSaved: boolean) => {
    setOpenActivateProgramForm(false)
    if (wasSaved) {
      refetchProgram()
    }
  }

  const onCompleteProgramFormClosed = (wasSaved: boolean) => {
    setOpenCompleteProgramForm(false)
    if (wasSaved) {
      refetchProgram()
    }
  }

  const onCancelProgramFormClosed = (wasSaved: boolean) => {
    setOpenCancelProgramForm(false)
    if (wasSaved) {
      refetchProgram()
    }
  }

  const onDeleteProgramFormClosed = (wasDeleted: boolean) => {
    setOpenDeleteProgramForm(false)
    if (wasDeleted) {
      router.push('/ppm/programs')
    }
  }

  if (isLoading) {
    return <ProgramDetailsLoading />
  }

  if (!programData) {
    return notFound()
  }

  // Carries the rest of the query across, so following a tile to its section
  // does not reset the filter the tile was counting under.
  const goToSection = (sectionId: string) => {
    const params = new URLSearchParams(searchParams.toString())
    if (sectionId === ProgramSections.Overview) {
      params.delete('section')
    } else {
      params.set('section', sectionId)
    }
    const query = params.toString()
    router.replace(
      query
        ? `/ppm/programs/${programKey}?${query}`
        : `/ppm/programs/${programKey}`,
      { scroll: false },
    )
  }

  const statusFilter = (
    <ProjectsFilterBar
      selectedStatuses={projectStatuses}
      onStatusChange={setProjectStatuses}
      showPortfolioFilter={false}
      showRoleFilter={false}
      onReset={() => setProjectStatuses(DEFAULT_PROJECT_STATUSES)}
    />
  )

  const renderSection = (section: ProgramSections) => {
    switch (section) {
      case ProgramSections.Projects:
        return (
          <>
            {statusFilter}
            <ProjectViewManager
              projects={projectsData ?? []}
              isLoading={projectsDataIsLoading}
              refetch={refetchProjectsData}
              hidePortfolio={true}
              hideProgram={true}
              defaultView="Card"
              persistStateKey="program-projects"
            />
          </>
        )
      default:
        return (
          <Flex vertical gap="middle">
            {missingDates && (
              <Alert
                title="Program Dates are required before activating."
                type="warning"
                showIcon
              />
            )}
            {statusFilter}
            <ProgramOverview
              program={programData}
              projects={projectsData ?? []}
              projectsLoading={projectsDataIsLoading}
              onNavigateToSection={goToSection}
            />
          </Flex>
        )
    }
  }

  return (
    <>
      <RecordLayout
        sections={sections}
        defaultSection={ProgramSections.Overview}
        record={{
          name: programData.name,
          recordKey: String(programData.key),
          parent: { label: 'Programs', href: '/ppm/programs' },
          subtitle: 'Program Details',
          tags: <LifecycleStatusTag status={programData.status} />,
          actions: <PageActions actionItems={actionsMenuItems} />,
        }}
        facts={<ProgramFacts program={programData} />}
      >
        {(section) => renderSection(section as ProgramSections)}
      </RecordLayout>

      {openEditProgramForm && (
        <EditProgramForm
          programKey={programData.key}
          onFormComplete={() => onEditProgramFormClosed(true)}
          onFormCancel={() => onEditProgramFormClosed(false)}
        />
      )}
      {openActivateProgramForm && (
        <ChangeProgramStatusForm
          program={programData}
          statusAction={ProgramStatusAction.Activate}
          onFormComplete={() => onActivateProgramFormClosed(true)}
          onFormCancel={() => onActivateProgramFormClosed(false)}
        />
      )}
      {openCompleteProgramForm && (
        <ChangeProgramStatusForm
          program={programData}
          statusAction={ProgramStatusAction.Complete}
          onFormComplete={() => onCompleteProgramFormClosed(true)}
          onFormCancel={() => onCompleteProgramFormClosed(false)}
        />
      )}
      {openCancelProgramForm && (
        <ChangeProgramStatusForm
          program={programData}
          statusAction={ProgramStatusAction.Cancel}
          onFormComplete={() => onCancelProgramFormClosed(true)}
          onFormCancel={() => onCancelProgramFormClosed(false)}
        />
      )}
      {openDeleteProgramForm && (
        <DeleteProgramForm
          program={programData}
          onFormComplete={() => onDeleteProgramFormClosed(true)}
          onFormCancel={() => onDeleteProgramFormClosed(false)}
        />
      )}
    </>
  )
}

// useSearchParams suspends a prerendered route up to the nearest boundary. In
// development routes render on demand, so a missing one only fails the
// production build.
const ProgramDetailsPageWithSuspense = (props: {
  params: Promise<{ key: string }>
}) => (
  <Suspense fallback={<ProgramDetailsLoading />}>
    <ProgramDetailsPage {...props} />
  </Suspense>
)

const ProgramDetailsPageWithAuthorization = authorizePage(
  ProgramDetailsPageWithSuspense,
  'Permission',
  'Permissions.Programs.View',
)

export default ProgramDetailsPageWithAuthorization
