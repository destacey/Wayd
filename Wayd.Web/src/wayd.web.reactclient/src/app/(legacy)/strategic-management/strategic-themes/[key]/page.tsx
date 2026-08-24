'use client'

import { PageActions, PageTitle } from '@/src/components/common'
import { MarkdownRenderer } from '@/src/components/common/markdown'
import useAuth from '@/src/components/contexts/auth'
import { authorizePage } from '@/src/components/hoc'
import { useAppDispatch, useDocumentTitle } from '@/src/hooks'
import { useGetStrategicThemeQuery } from '@/src/store/features/strategic-management/strategic-themes-api'
import { Descriptions, Flex, MenuProps } from 'antd'
import { notFound, usePathname, useRouter } from 'next/navigation'
import StrategicThemeDetailsLoading from './loading'
import { use, useEffect, useState } from 'react'
import { BreadcrumbItem, setBreadcrumbRoute } from '@/src/store/breadcrumbs'
import { ItemType } from 'antd/es/menu/interface'
import {
  ChangeStrategicThemeStateForm,
  DeleteStrategicThemeForm,
  EditStrategicThemeForm,
} from '../_components'
import { StrategicThemeStateAction } from '../_components/change-strategic-theme-state-form'

const { Item } = Descriptions

enum MenuActions {
  Edit = 'Edit',
  Delete = 'Delete',
  Activate = 'Activate',
  Archive = 'Archive',
}

const StrategicThemeDetailsPage = (props: {
  params: Promise<{ key: string }>
}) => {
  const { key } = use(props.params)
  const stKey = Number(key)

  useDocumentTitle('Strategic Theme Details')

  const [openEditStrategicThemeForm, setOpenEditStrategicThemeForm] =
    useState<boolean>(false)
  const [openActivateStrategicThemeForm, setOpenActivateStrategicThemeForm] =
    useState<boolean>(false)
  const [openArchiveStrategicThemeForm, setOpenArchiveStrategicThemeForm] =
    useState<boolean>(false)
  const [openDeleteStrategicThemeForm, setOpenDeleteStrategicThemeForm] =
    useState<boolean>(false)

  const pathname = usePathname()
  const dispatch = useAppDispatch()

  const router = useRouter()

  const { hasPermissionClaim } = useAuth()
  const canUpdateStrategicTheme = hasPermissionClaim(
    'Permissions.StrategicThemes.Update',
  )
  const canDeleteStrategicTheme = hasPermissionClaim(
    'Permissions.StrategicThemes.Delete',
  )

  const {
    data: strategicThemeData,
    isLoading,
    error,
    refetch: refetchStrategicTheme,
  } = useGetStrategicThemeQuery(stKey)

  useEffect(() => {
    if (!strategicThemeData) return

    const breadcrumbRoute: BreadcrumbItem[] = [
      {
        title: 'Strategic Management',
      },
      {
        href: `/strategic-management/strategic-themes`,
        title: 'Strategic Themes',
      },
    ]

    breadcrumbRoute.push({
      title: 'Details',
    })

    dispatch(setBreadcrumbRoute({ route: breadcrumbRoute, pathname }))
  }, [dispatch, pathname, strategicThemeData])

  useEffect(() => {
    error && console.error(error)
  }, [error])

  const actionsMenuItems: MenuProps['items'] = (() => {
    const currentState = strategicThemeData?.state.name
    const availableActions =
      currentState === 'Proposed'
        ? [MenuActions.Delete, MenuActions.Activate]
        : currentState === 'Active'
          ? [MenuActions.Archive]
          : []

    const items: ItemType[] = []
    if (canUpdateStrategicTheme) {
      items.push({
        key: 'edit',
        label: MenuActions.Edit,
        onClick: () => setOpenEditStrategicThemeForm(true),
      })
    }
    if (
      canDeleteStrategicTheme &&
      availableActions.includes(MenuActions.Delete)
    ) {
      items.push({
        key: 'delete',
        label: MenuActions.Delete,
        onClick: () => setOpenDeleteStrategicThemeForm(true),
      })
    }

    if (
      (canUpdateStrategicTheme &&
        availableActions.includes(MenuActions.Activate)) ||
      availableActions.includes(MenuActions.Archive)
    ) {
      items.push({
        key: 'manage-divider',
        type: 'divider',
      })
    }

    if (
      canUpdateStrategicTheme &&
      availableActions.includes(MenuActions.Activate)
    ) {
      items.push({
        key: 'activate',
        label: MenuActions.Activate,
        onClick: () => setOpenActivateStrategicThemeForm(true),
      })
    }

    if (
      canUpdateStrategicTheme &&
      availableActions.includes(MenuActions.Archive)
    ) {
      items.push({
        key: 'archive',
        label: MenuActions.Archive,
        onClick: () => setOpenArchiveStrategicThemeForm(true),
      })
    }

    return items
  })()

  const onEditStrategicThemeFormClosed = (wasSaved: boolean) => {
    setOpenEditStrategicThemeForm(false)
    if (wasSaved) {
      refetchStrategicTheme()
    }
  }

  const onActivateStrategicThemeFormClosed = (wasSaved: boolean) => {
    setOpenActivateStrategicThemeForm(false)
    if (wasSaved) {
      refetchStrategicTheme()
    }
  }

  const onArchiveStrategicThemeFormClosed = (wasSaved: boolean) => {
    setOpenArchiveStrategicThemeForm(false)
    if (wasSaved) {
      refetchStrategicTheme()
    }
  }

  const onDeleteStrategicThemeFormClosed = (wasDeleted: boolean) => {
    setOpenDeleteStrategicThemeForm(false)
    if (wasDeleted) {
      router.push('/strategic-management/strategic-themes')
    }
  }

  if (isLoading) {
    return <StrategicThemeDetailsLoading />
  }

  if (!strategicThemeData) {
    return notFound()
  }

  return (
    <>
      <PageTitle
        title={`${strategicThemeData?.key} - ${strategicThemeData?.name}`}
        subtitle="Strategic Theme Details"
        actions={<PageActions actionItems={actionsMenuItems} />}
      />
      <Flex vertical gap="small">
        <Descriptions>
          <Item label="State">{strategicThemeData.state.name}</Item>
        </Descriptions>
        <Descriptions layout="vertical" size="small">
          <Item label="Description">
            <MarkdownRenderer markdown={strategicThemeData.description} />
          </Item>
        </Descriptions>
      </Flex>

      {openEditStrategicThemeForm && (
        <EditStrategicThemeForm
          strategicThemeKey={strategicThemeData?.key}
          onFormComplete={() => onEditStrategicThemeFormClosed(true)}
          onFormCancel={() => onEditStrategicThemeFormClosed(false)}
        />
      )}
      {openActivateStrategicThemeForm && (
        <ChangeStrategicThemeStateForm
          strategicTheme={strategicThemeData}
          stateAction={StrategicThemeStateAction.Activate}
          onFormComplete={() => onActivateStrategicThemeFormClosed(true)}
          onFormCancel={() => onActivateStrategicThemeFormClosed(false)}
        />
      )}
      {openArchiveStrategicThemeForm && (
        <ChangeStrategicThemeStateForm
          strategicTheme={strategicThemeData}
          stateAction={StrategicThemeStateAction.Archive}
          onFormComplete={() => onArchiveStrategicThemeFormClosed(true)}
          onFormCancel={() => onArchiveStrategicThemeFormClosed(false)}
        />
      )}
      {openDeleteStrategicThemeForm && (
        <DeleteStrategicThemeForm
          strategicTheme={strategicThemeData}
          onFormComplete={() => onDeleteStrategicThemeFormClosed(true)}
          onFormCancel={() => onDeleteStrategicThemeFormClosed(false)}
        />
      )}
    </>
  )
}

const StrategicThemeDetailsPageWithAuthorization = authorizePage(
  StrategicThemeDetailsPage,
  'Permission',
  'Permissions.StrategicThemes.View',
)

export default StrategicThemeDetailsPageWithAuthorization
