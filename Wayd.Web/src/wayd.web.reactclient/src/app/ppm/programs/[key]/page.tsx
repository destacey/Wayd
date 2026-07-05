'use client'

import {
  LifecycleStatusTag,
  PageActions,
  PageTitle,
} from '@/src/components/common'
import useAuth from '@/src/components/contexts/auth'
import { authorizePage } from '@/src/components/hoc'
import { useAppDispatch, useDocumentTitle } from '@/src/hooks'
import {
  useGetProgramQuery,
  useGetProgramProjectsQuery,
} from '@/src/store/features/ppm/programs-api'
import { Alert, Badge, Col, Flex, MenuProps, Row, Typography } from 'antd'

const { Text } = Typography
import { notFound, usePathname, useRouter } from 'next/navigation'
import { use, useEffect, useState } from 'react'
import ProgramDetailsLoading from './loading'
import {
  ChangeProgramStatusForm,
  DeleteProgramForm,
  EditProgramForm,
  ProgramDetails,
} from '../_components'
import { BreadcrumbItem, setBreadcrumbRoute } from '@/src/store/breadcrumbs'
import { ItemType } from 'antd/es/menu/interface'
import { ProgramStatusAction } from '../_components/change-program-status-form'
import { ProjectViewManager } from '@/src/app/ppm/_components'

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

  const pathname = usePathname()
  const dispatch = useAppDispatch()

  const router = useRouter()

  const { hasPermissionClaim } = useAuth()
  const canUpdateProgram = hasPermissionClaim('Permissions.Programs.Update')
  const canDeleteProgram = hasPermissionClaim('Permissions.Programs.Delete')

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
    },
    { skip: !programData },
  )

  useDocumentTitle(`${programData?.name ?? programKey} - Program Details`)

  useEffect(() => {
    if (!programData) return

    const breadcrumbRoute: BreadcrumbItem[] = [
      {
        title: 'PPM',
      },
      {
        href: `/ppm/programs`,
        title: 'Programs',
      },
    ]

    breadcrumbRoute.push({
      title: 'Details',
    })

    dispatch(setBreadcrumbRoute({ route: breadcrumbRoute, pathname }))
  }, [dispatch, pathname, programData])

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
    if (canUpdateProgram && availableActions.includes(ProgramAction.Edit)) {
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
      canUpdateProgram &&
      (availableActions.includes(ProgramAction.Activate) ||
        availableActions.includes(ProgramAction.Complete) ||
        availableActions.includes(ProgramAction.Cancel))
    ) {
      items.push({
        key: 'manage-divider',
        type: 'divider',
      })
    }

    if (canUpdateProgram && availableActions.includes(ProgramAction.Activate)) {
      items.push({
        key: 'activate',
        label: ProgramAction.Activate,
        onClick: () => setOpenActivateProgramForm(true),
      })
    }

    if (canUpdateProgram && availableActions.includes(ProgramAction.Complete)) {
      items.push({
        key: 'complete',
        label: ProgramAction.Complete,
        onClick: () => setOpenCompleteProgramForm(true),
      })
    }

    if (canUpdateProgram && availableActions.includes(ProgramAction.Cancel)) {
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

  return (
    <>
      <PageTitle
        title={`${programData?.key} - ${programData?.name}`}
        subtitle="Program Details"
        tags={<LifecycleStatusTag status={programData?.status} />}
        actions={<PageActions actionItems={actionsMenuItems} />}
      />

      {missingDates === true && (
        <>
          <Alert
            title="Program Dates are required before activating."
            type="warning"
            showIcon
          />
          <br />
        </>
      )}

      <Row gutter={16}>
        <Col xs={24} md={9} xxl={6}>
          <ProgramDetails program={programData!} />
        </Col>
        <Col xs={24} md={15} xxl={18}>
          <Flex vertical gap="large">
            <Flex vertical>
              <Flex align="center" gap={8}>
                <Text strong>Projects</Text>
                <Badge
                  count={projectsData?.length ?? 0}
                  showZero
                  color="blue"
                />
              </Flex>
              <ProjectViewManager
                projects={projectsData ?? []}
                isLoading={projectsDataIsLoading}
                refetch={refetchProjectsData}
                hidePortfolio={true}
                hideProgram={true}
                defaultView="Card"
                persistStateKey="program-projects"
              />
            </Flex>
          </Flex>
        </Col>
      </Row>

      {openEditProgramForm && (
        <EditProgramForm
          programKey={programData?.key}
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

const ProgramDetailsPageWithAuthorization = authorizePage(
  ProgramDetailsPage,
  'Permission',
  'Permissions.Programs.View',
)

export default ProgramDetailsPageWithAuthorization
