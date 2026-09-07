'use client'

import { PageActions } from '@/src/components/common'
import { MarkdownRenderer } from '@/src/components/common/markdown'
import { RecordLayout, RecordSection } from '@/src/components/common/record'
import useAuth from '@/src/components/contexts/auth'
import { authorizePage } from '@/src/components/hoc'
import { useDocumentTitle } from '@/src/hooks'
import {
  useGetStrategicThemeActivitiesQuery,
  useGetStrategicThemeQuery,
  useLazyGetStrategicThemeActivitiesQuery,
} from '@/src/store/features/strategic-management/strategic-themes-api'
import {
  ACTIVITY_LOG_PAGE_SIZE,
  ActivityLogExportButton,
  ActivityLogTimeline,
  useActivityLog,
} from '@/src/components/common/activities'
import { MenuProps, Tag } from 'antd'
import { notFound, useRouter, useSearchParams } from 'next/navigation'
import StrategicThemeDetailsLoading from './loading'
import { use, useEffect, useState } from 'react'
import { ItemType } from 'antd/es/menu/interface'
import {
  ChangeStrategicThemeStateForm,
  DeleteStrategicThemeForm,
  EditStrategicThemeForm,
} from '../_components'
import { StrategicThemeStateAction } from '../_components/change-strategic-theme-state-form'

enum MenuActions {
  Edit = 'Edit',
  Delete = 'Delete',
  Activate = 'Activate',
  Archive = 'Archive',
}

enum StrategicThemeSections {
  Overview = 'overview',
  Activities = 'activities',
}

const sections: RecordSection[] = [
  { id: StrategicThemeSections.Overview, label: 'Overview' },
  { id: StrategicThemeSections.Activities, label: 'Activity' },
]

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
    error && console.error(error)
  }, [error])

  // The active section lives in the URL, owned by RecordLayout. Read here only
  // to hold the activity query back until its section is open.
  const searchParams = useSearchParams()
  const activeSection = (searchParams.get('section') ??
    StrategicThemeSections.Overview) as StrategicThemeSections

  const activitiesQuery = useGetStrategicThemeActivitiesQuery(
    {
      idOrKey: strategicThemeData?.id ?? '',
      page: 1,
      pageSize: ACTIVITY_LOG_PAGE_SIZE,
    },
    {
      skip:
        !strategicThemeData?.id ||
        activeSection !== StrategicThemeSections.Activities,
    },
  )
  const [fetchActivityLogPage] = useLazyGetStrategicThemeActivitiesQuery()

  const activityLog = useActivityLog({
    idOrKey: strategicThemeData?.id,
    query: activitiesQuery,
    fetchPage: fetchActivityLogPage,
    exportFilename: `strategic-theme-${strategicThemeData?.key ?? stKey}-activity`,
  })

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
      <RecordLayout
        sections={sections}
        defaultSection={StrategicThemeSections.Overview}
        record={{
          name: strategicThemeData.name,
          recordKey: String(strategicThemeData.key),
          parent: {
            label: 'Strategic Themes',
            href: '/strategic-management/strategic-themes',
          },
          subtitle: 'Strategic Theme Details',
          tags: <Tag>{strategicThemeData.state.name}</Tag>,
          actions: <PageActions actionItems={actionsMenuItems} />,
        }}
        sectionActions={
          activeSection === StrategicThemeSections.Activities ? (
            <ActivityLogExportButton activityLog={activityLog} />
          ) : undefined
        }
      >
        {(section) =>
          section === StrategicThemeSections.Activities ? (
            <ActivityLogTimeline {...activityLog.timelineProps} />
          ) : (
            /* The description is the whole of a theme's content, so it leads
               rather than sitting in a facts panel that opens closed. */
            <MarkdownRenderer markdown={strategicThemeData.description} />
          )
        }
      </RecordLayout>

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
