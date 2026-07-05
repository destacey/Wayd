'use client'

import {
  LifecycleStatusTag,
  PageActions,
  PageTitle,
} from '@/src/components/common'
import useAuth from '@/src/components/contexts/auth'
import { authorizePage } from '@/src/components/hoc'
import { useAppDispatch, useDocumentTitle } from '@/src/hooks'
import { Badge, Col, Flex, MenuProps, Row, Typography } from 'antd'
import { notFound, usePathname, useRouter } from 'next/navigation'

const { Text } = Typography
import { use, useEffect, useState } from 'react'
import StrategicInitiativeDetailsLoading from './loading'
import { BreadcrumbItem, setBreadcrumbRoute } from '@/src/store/breadcrumbs'
import { ItemType } from 'antd/es/menu/interface'
import {
  useGetStrategicInitiativeKpisQuery,
  useGetStrategicInitiativeProjectsQuery,
  useGetStrategicInitiativeQuery,
} from '@/src/store/features/ppm/strategic-initiatives-api'
import {
  ChangeStrategicInitiativeStatusForm,
  CreateStrategicInitiativeKpiForm,
  DeleteStrategicInitiativeForm,
  ManageStrategicInitiativeProjectsForm,
  StrategicInitiativeDetails,
  StrategicInitiativeKpiViewManager,
} from '../_components'
import EditStrategicInitiativeForm from '../_components/edit-strategic-initiative-form'
import { StrategicInitiativeStatusAction } from '../_components/change-strategic-initiative-status-form'
import { ProjectViewManager } from '../../_components'

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

  useDocumentTitle('Strategic Initiative Details')

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

  const pathname = usePathname()
  const dispatch = useAppDispatch()

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
    error,
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

  // Derive isReadOnly from strategic initiative status
  const isReadOnly = !strategicInitiativeData ? false : (() => {
    const status = strategicInitiativeData.status.name
    return status === 'Completed' || status === 'Cancelled'
  })()

  // Update breadcrumb route - side effect only
  useEffect(() => {
    if (!strategicInitiativeData) return

    const breadcrumbRoute: BreadcrumbItem[] = [
      {
        title: 'PPM',
      },
      {
        href: `/ppm/strategic-initiatives`,
        title: 'Strategic Initiatives',
      },
    ]

    breadcrumbRoute.push({
      title: 'Details',
    })

    dispatch(setBreadcrumbRoute({ route: breadcrumbRoute, pathname }))
  }, [dispatch, pathname, strategicInitiativeData])

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

    //KPI and Project actions
    if (!isReadOnly && canUpdateStrategicInitiative) {
      items.push(
        {
          key: 'manage-divider-kps',
          type: 'divider',
        },
        {
          key: 'createKpi',
          label: 'Create KPI',
          onClick: () => setOpenCreateKpiForm(true),
        },
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

  return (
    <>
      <PageTitle
        title={`${strategicInitiativeData?.key} - ${strategicInitiativeData?.name}`}
        subtitle="Strategic Initiative Details"
        tags={<LifecycleStatusTag status={strategicInitiativeData?.status} />}
        actions={<PageActions actionItems={actionsMenuItems} />}
      />

      <Row gutter={16}>
        <Col xs={24} md={9} xxl={6}>
          <StrategicInitiativeDetails
            strategicInitiative={strategicInitiativeData!}
          />
        </Col>
        <Col xs={24} md={15} xxl={18}>
          <Flex vertical gap="large">
            <Flex vertical>
              <Flex align="center" gap={8}>
                <Text strong>KPIs</Text>
                <Badge count={kpiData?.length ?? 0} showZero color="blue" />
              </Flex>
              <StrategicInitiativeKpiViewManager
                strategicInitiativeId={strategicInitiativeData.id}
                kpis={kpiData}
                canManageKpis={canUpdateStrategicInitiative}
                isLoading={isLoadingKpis}
                refetch={refetchKpis}
                gridHeight={400}
                isReadOnly={isReadOnly}
                onCreateKpi={
                  !isReadOnly && canUpdateStrategicInitiative
                    ? () => setOpenCreateKpiForm(true)
                    : undefined
                }
              />
            </Flex>
            <Flex vertical>
              <Flex align="center" gap={8}>
                <Text strong>Projects</Text>
                <Badge count={projectData?.length ?? 0} showZero color="blue" />
              </Flex>
              <ProjectViewManager
                projects={projectData ?? []}
                isLoading={isLoadingProjects}
                refetch={refetchProjects}
                hidePortfolio={false}
                groupByProgram={true}
                defaultView="Card"
                persistStateKey="strategic-initiative-projects"
              />
            </Flex>
          </Flex>
        </Col>
      </Row>

      {openEditStrategicInitiativeForm && (
        <EditStrategicInitiativeForm
          strategicInitiativeKey={strategicInitiativeData?.key}
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
          strategicInitiativeId={strategicInitiativeData?.id}
          onFormComplete={() => onCreateKpiFormClosed(true)}
          onFormCancel={() => onCreateKpiFormClosed(false)}
        />
      )}
      {openManageProjectsForm && (
        <ManageStrategicInitiativeProjectsForm
          strategicInitiativeId={strategicInitiativeData?.id}
          portfolioId={strategicInitiativeData?.portfolio.id}
          onFormComplete={() => setOpenManageProjectsForm(false)}
          onFormCancel={() => setOpenManageProjectsForm(false)}
        />
      )}
    </>
  )
}

const StrategicInitiativeDetailsPageWithAuthorization = authorizePage(
  StrategicInitiativeDetailsPage,
  'Permission',
  'Permissions.StrategicInitiatives.View',
)

export default StrategicInitiativeDetailsPageWithAuthorization
