'use client'

import { LifecycleStatusTag, PageActions } from '@/src/components/common'
import { RecordLayout, RecordSection } from '@/src/components/common/record'
import useAuth from '@/src/components/contexts/auth'
import { authorizePage } from '@/src/components/hoc'
import { useDocumentTitle } from '@/src/hooks'
import {
  useGetStrategicInitiativeKpisQuery,
  useGetStrategicInitiativeProjectsQuery,
  useGetStrategicInitiativeQuery,
} from '@/src/store/features/ppm/strategic-initiatives-api'
import { Button, MenuProps } from 'antd'
import { ItemType } from 'antd/es/menu/interface'
import { notFound, useRouter } from 'next/navigation'
import { Suspense, use, useState } from 'react'
import {
  ChangeStrategicInitiativeStatusForm,
  CreateStrategicInitiativeKpiForm,
  DeleteStrategicInitiativeForm,
  ManageStrategicInitiativeProjectsForm,
  StrategicInitiativeKpiViewManager,
} from '@/src/app/ppm/strategic-initiatives/_components'
import EditStrategicInitiativeForm from '@/src/app/ppm/strategic-initiatives/_components/edit-strategic-initiative-form'
import { StrategicInitiativeStatusAction } from '@/src/app/ppm/strategic-initiatives/_components/change-strategic-initiative-status-form'
import { ProjectViewManager } from '@/src/app/ppm/_components'
import StrategicInitiativeDetailsLoading from './loading'
import StrategicInitiativeFacts from './_components/strategic-initiative-facts'

enum StrategicInitiativeSections {
  Kpis = 'kpis',
  Projects = 'projects',
}

enum StrategicInitiativeAction {
  Edit = 'Edit',
  Delete = 'Delete',
  Approve = 'Approve',
  Activate = 'Activate',
  Complete = 'Complete',
  Cancel = 'Cancel',
}

const StrategicInitiativeDetailsPage = (props: {
  params: Promise<{ key: string }>
}) => {
  const { key } = use(props.params)
  const siKey = Number(key)

  const [openEditStrategicInitiativeForm, setOpenEditStrategicInitiativeForm] =
    useState<boolean>(false)
  const [
    openApproveStrategicInitiativeForm,
    setOpenApproveStrategicInitiativeForm,
  ] = useState<boolean>(false)
  const [
    openActivateStrategicInitiativeForm,
    setOpenActivateStrategicInitiativeForm,
  ] = useState<boolean>(false)
  const [
    openCompleteStrategicInitiativeForm,
    setOpenCompleteStrategicInitiativeForm,
  ] = useState<boolean>(false)
  const [
    openCancelStrategicInitiativeForm,
    setOpenCancelStrategicInitiativeForm,
  ] = useState<boolean>(false)
  const [
    openDeleteStrategicInitiativeForm,
    setOpenDeleteStrategicInitiativeForm,
  ] = useState<boolean>(false)
  const [openCreateKpiForm, setOpenCreateKpiForm] = useState(false)
  const [openManageProjectsForm, setOpenManageProjectsForm] = useState(false)

  const router = useRouter()

  const { hasPermissionClaim } = useAuth()
  const canUpdateStrategicInitiative = hasPermissionClaim(
    'Permissions.StrategicInitiatives.Update',
  )
  const canDeleteStrategicInitiative = hasPermissionClaim(
    'Permissions.StrategicInitiatives.Delete',
  )

  const {
    data: strategicInitiativeData,
    isLoading,
    refetch: refetchStrategicInitiative,
  } = useGetStrategicInitiativeQuery(siKey)

  const {
    data: kpiData,
    isLoading: isLoadingKpis,
    refetch: refetchKpis,
  } = useGetStrategicInitiativeKpisQuery(strategicInitiativeData?.id ?? '', {
    skip: !strategicInitiativeData?.id,
  })

  const {
    data: projectData,
    isLoading: isLoadingProjects,
    refetch: refetchProjects,
  } = useGetStrategicInitiativeProjectsQuery(strategicInitiativeData?.id ?? '', {
    skip: !strategicInitiativeData?.id,
  })

  useDocumentTitle(
    `${strategicInitiativeData?.name ?? siKey} - Strategic Initiative Details`,
  )

  // A closed initiative takes no new KPIs or project assignments.
  const isReadOnly = !strategicInitiativeData
    ? false
    : (() => {
        const status = strategicInitiativeData.status.name
        return status === 'Completed' || status === 'Canceled'
      })()

  const actionsMenuItems: MenuProps['items'] = (() => {
    const currentStatus = strategicInitiativeData?.status.name
    const availableActions =
      currentStatus === 'Proposed'
        ? [
            StrategicInitiativeAction.Edit,
            StrategicInitiativeAction.Delete,
            StrategicInitiativeAction.Approve,
            StrategicInitiativeAction.Cancel,
          ]
        : currentStatus === 'Approved'
          ? [
              StrategicInitiativeAction.Edit,
              StrategicInitiativeAction.Delete,
              StrategicInitiativeAction.Activate,
              StrategicInitiativeAction.Cancel,
            ]
          : currentStatus === 'Active'
            ? [
                StrategicInitiativeAction.Edit,
                StrategicInitiativeAction.Complete,
                StrategicInitiativeAction.Cancel,
              ]
            : []

    // TODO: Implement On Hold status

    const items: ItemType[] = []
    if (
      canUpdateStrategicInitiative &&
      availableActions.includes(StrategicInitiativeAction.Edit)
    ) {
      items.push({
        key: 'edit',
        label: StrategicInitiativeAction.Edit,
        onClick: () => setOpenEditStrategicInitiativeForm(true),
      })
    }
    if (
      canDeleteStrategicInitiative &&
      availableActions.includes(StrategicInitiativeAction.Delete)
    ) {
      items.push({
        key: 'delete',
        label: StrategicInitiativeAction.Delete,
        onClick: () => setOpenDeleteStrategicInitiativeForm(true),
      })
    }

    if (
      canUpdateStrategicInitiative &&
      (availableActions.includes(StrategicInitiativeAction.Approve) ||
        availableActions.includes(StrategicInitiativeAction.Activate) ||
        availableActions.includes(StrategicInitiativeAction.Complete) ||
        availableActions.includes(StrategicInitiativeAction.Cancel))
    ) {
      items.push({
        key: 'manage-divider',
        type: 'divider',
      })
    }

    if (
      canUpdateStrategicInitiative &&
      availableActions.includes(StrategicInitiativeAction.Approve)
    ) {
      items.push({
        key: 'approve',
        label: StrategicInitiativeAction.Approve,
        onClick: () => setOpenApproveStrategicInitiativeForm(true),
      })
    }

    if (
      canUpdateStrategicInitiative &&
      availableActions.includes(StrategicInitiativeAction.Activate)
    ) {
      items.push({
        key: 'activate',
        label: StrategicInitiativeAction.Activate,
        onClick: () => setOpenActivateStrategicInitiativeForm(true),
      })
    }

    if (
      canUpdateStrategicInitiative &&
      availableActions.includes(StrategicInitiativeAction.Complete)
    ) {
      items.push({
        key: 'complete',
        label: StrategicInitiativeAction.Complete,
        onClick: () => setOpenCompleteStrategicInitiativeForm(true),
      })
    }

    if (
      canUpdateStrategicInitiative &&
      availableActions.includes(StrategicInitiativeAction.Cancel)
    ) {
      items.push({
        key: 'cancel',
        label: StrategicInitiativeAction.Cancel,
        onClick: () => setOpenCancelStrategicInitiativeForm(true),
      })
    }

    if (!isReadOnly && canUpdateStrategicInitiative) {
      items.push(
        {
          key: 'manage-divider-projects',
          type: 'divider',
        },
        {
          key: 'manageProjects',
          label: 'Manage Projects',
          onClick: () => setOpenManageProjectsForm(true),
        },
      )
    }

    return items
  })()

  const onEditStrategicInitiativeFormClosed = (wasSaved: boolean) => {
    setOpenEditStrategicInitiativeForm(false)
    if (wasSaved) {
      refetchStrategicInitiative()
    }
  }

  const onApproveStrategicInitiativeFormClosed = (wasSaved: boolean) => {
    setOpenApproveStrategicInitiativeForm(false)
    if (wasSaved) {
      refetchStrategicInitiative()
    }
  }

  const onActivateStrategicInitiativeFormClosed = (wasSaved: boolean) => {
    setOpenActivateStrategicInitiativeForm(false)
    if (wasSaved) {
      refetchStrategicInitiative()
    }
  }

  const onCompleteStrategicInitiativeFormClosed = (wasSaved: boolean) => {
    setOpenCompleteStrategicInitiativeForm(false)
    if (wasSaved) {
      refetchStrategicInitiative()
    }
  }

  const onCancelStrategicInitiativeFormClosed = (wasSaved: boolean) => {
    setOpenCancelStrategicInitiativeForm(false)
    if (wasSaved) {
      refetchStrategicInitiative()
    }
  }

  const onDeleteStrategicInitiativeFormClosed = (wasDeleted: boolean) => {
    setOpenDeleteStrategicInitiativeForm(false)
    if (wasDeleted) {
      router.push('/ppm/strategic-initiatives')
    }
  }

  const onCreateKpiFormClosed = (wasSaved: boolean) => {
    setOpenCreateKpiForm(false)
    if (wasSaved) refetchKpis()
  }

  if (isLoading) {
    return <StrategicInitiativeDetailsLoading />
  }

  if (!strategicInitiativeData) {
    return notFound()
  }

  const canManageKpis = !isReadOnly && canUpdateStrategicInitiative

  const sections: RecordSection[] = [
    {
      id: StrategicInitiativeSections.Kpis,
      label: 'KPIs',
      count: kpiData?.length,
    },
    {
      id: StrategicInitiativeSections.Projects,
      label: 'Projects',
      count: projectData?.length,
    },
  ]

  const renderSection = (section: StrategicInitiativeSections) => {
    switch (section) {
      case StrategicInitiativeSections.Projects:
        return (
          <ProjectViewManager
            projects={projectData ?? []}
            isLoading={isLoadingProjects}
            refetch={refetchProjects}
            hidePortfolio={false}
            groupByProgram={true}
            defaultView="Card"
            persistStateKey="strategic-initiative-projects"
          />
        )
      default:
        return (
          <StrategicInitiativeKpiViewManager
            strategicInitiativeId={strategicInitiativeData.id}
            kpis={kpiData}
            canManageKpis={canUpdateStrategicInitiative}
            isLoading={isLoadingKpis}
            refetch={refetchKpis}
            gridHeight={400}
            isReadOnly={isReadOnly}
            onCreateKpi={
              canManageKpis ? () => setOpenCreateKpiForm(true) : undefined
            }
          />
        )
    }
  }

  return (
    <>
      <RecordLayout
        sections={sections}
        defaultSection={StrategicInitiativeSections.Kpis}
        record={{
          name: strategicInitiativeData.name,
          recordKey: String(strategicInitiativeData.key),
          parent: {
            label: 'Strategic Initiatives',
            href: '/ppm/strategic-initiatives',
          },
          subtitle: 'Strategic Initiative Details',
          tags: <LifecycleStatusTag status={strategicInitiativeData.status} />,
          actions: <PageActions actionItems={actionsMenuItems} />,
        }}
        facts={
          <StrategicInitiativeFacts
            strategicInitiative={strategicInitiativeData}
          />
        }
        sectionActions={
          canManageKpis ? (
            <Button onClick={() => setOpenCreateKpiForm(true)}>
              Create KPI
            </Button>
          ) : null
        }
      >
        {(section) => renderSection(section as StrategicInitiativeSections)}
      </RecordLayout>

      {openEditStrategicInitiativeForm && (
        <EditStrategicInitiativeForm
          strategicInitiativeKey={strategicInitiativeData.key}
          onFormComplete={() => onEditStrategicInitiativeFormClosed(true)}
          onFormCancel={() => onEditStrategicInitiativeFormClosed(false)}
        />
      )}
      {openApproveStrategicInitiativeForm && (
        <ChangeStrategicInitiativeStatusForm
          strategicInitiative={strategicInitiativeData}
          statusAction={StrategicInitiativeStatusAction.Approve}
          onFormComplete={() => onApproveStrategicInitiativeFormClosed(true)}
          onFormCancel={() => onApproveStrategicInitiativeFormClosed(false)}
        />
      )}
      {openActivateStrategicInitiativeForm && (
        <ChangeStrategicInitiativeStatusForm
          strategicInitiative={strategicInitiativeData}
          statusAction={StrategicInitiativeStatusAction.Activate}
          onFormComplete={() => onActivateStrategicInitiativeFormClosed(true)}
          onFormCancel={() => onActivateStrategicInitiativeFormClosed(false)}
        />
      )}
      {openCompleteStrategicInitiativeForm && (
        <ChangeStrategicInitiativeStatusForm
          strategicInitiative={strategicInitiativeData}
          statusAction={StrategicInitiativeStatusAction.Complete}
          onFormComplete={() => onCompleteStrategicInitiativeFormClosed(true)}
          onFormCancel={() => onCompleteStrategicInitiativeFormClosed(false)}
        />
      )}
      {openCancelStrategicInitiativeForm && (
        <ChangeStrategicInitiativeStatusForm
          strategicInitiative={strategicInitiativeData}
          statusAction={StrategicInitiativeStatusAction.Cancel}
          onFormComplete={() => onCancelStrategicInitiativeFormClosed(true)}
          onFormCancel={() => onCancelStrategicInitiativeFormClosed(false)}
        />
      )}
      {openDeleteStrategicInitiativeForm && (
        <DeleteStrategicInitiativeForm
          strategicInitiative={strategicInitiativeData}
          onFormComplete={() => onDeleteStrategicInitiativeFormClosed(true)}
          onFormCancel={() => onDeleteStrategicInitiativeFormClosed(false)}
        />
      )}
      {openCreateKpiForm && (
        <CreateStrategicInitiativeKpiForm
          strategicInitiativeId={strategicInitiativeData.id}
          onFormComplete={() => onCreateKpiFormClosed(true)}
          onFormCancel={() => onCreateKpiFormClosed(false)}
        />
      )}
      {openManageProjectsForm && (
        <ManageStrategicInitiativeProjectsForm
          strategicInitiativeId={strategicInitiativeData.id}
          portfolioId={strategicInitiativeData.portfolio.id}
          onFormComplete={() => setOpenManageProjectsForm(false)}
          onFormCancel={() => setOpenManageProjectsForm(false)}
        />
      )}
    </>
  )
}

// useSearchParams suspends a prerendered route up to the nearest boundary. In
// development routes render on demand, so a missing one only fails the
// production build.
const StrategicInitiativeDetailsPageWithSuspense = (props: {
  params: Promise<{ key: string }>
}) => (
  <Suspense fallback={<StrategicInitiativeDetailsLoading />}>
    <StrategicInitiativeDetailsPage {...props} />
  </Suspense>
)

const StrategicInitiativeDetailsPageWithAuthorization = authorizePage(
  StrategicInitiativeDetailsPageWithSuspense,
  'Permission',
  'Permissions.StrategicInitiatives.View',
)

export default StrategicInitiativeDetailsPageWithAuthorization
