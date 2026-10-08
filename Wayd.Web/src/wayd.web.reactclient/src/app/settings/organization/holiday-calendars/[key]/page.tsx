'use client'

import { PageActions } from '@/src/components/common'
import {
  ACTIVITY_LOG_PAGE_SIZE,
  ActivityLogExportButton,
  ActivityLogTimeline,
  useActivityLog,
} from '@/src/components/common/activities'
import { LabeledContent } from '@/src/components/common/content'
import { RecordLayout, RecordSection } from '@/src/components/common/record'
import useAuth from '@/src/components/contexts/auth'
import { useMessage } from '@/src/components/contexts/messaging'
import { authorizePage } from '@/src/components/hoc'
import { useDocumentTitle } from '@/src/hooks/use-document-title'
import {
  useGetHolidayCalendarActivitiesQuery,
  useGetHolidayCalendarQuery,
  useLazyGetHolidayCalendarActivitiesQuery,
} from '@/src/store/features/organization/holiday-calendars-api'
import { isApiError } from '@/src/utils'
import { Button, Flex, Tag } from 'antd'
import { ItemType } from 'antd/es/menu/interface'
import { notFound, useRouter, useSearchParams } from 'next/navigation'
import { use, useEffect, useState } from 'react'
import DeleteHolidayCalendarForm from '../_components/delete-holiday-calendar-form'
import HolidayCalendarForm from '../_components/holiday-calendar-form'
import HolidayForm from '../_components/holiday-form'
import HolidaysGrid from '../_components/holidays-grid'
import HolidayCalendarDetailsLoading from './loading'

enum HolidayCalendarSections {
  Holidays = 'holidays',
  Activities = 'activities',
}

/** The dialogs this record can open. One value, not one boolean each. */
type DialogId = 'edit' | 'delete' | 'add-holiday'

const HolidayCalendarDetailsPage = (props: {
  params: Promise<{ key: string }>
}) => {
  const { key } = use(props.params)

  const [dialog, setDialog] = useState<DialogId | null>(null)

  const messageApi = useMessage()
  const router = useRouter()

  const {
    data: calendar,
    isLoading,
    error,
    refetch,
  } = useGetHolidayCalendarQuery(key)

  const searchParams = useSearchParams()
  const activeSection =
    searchParams.get('section') ?? HolidayCalendarSections.Holidays

  const activitiesQuery = useGetHolidayCalendarActivitiesQuery(
    { idOrKey: calendar?.id ?? '', page: 1, pageSize: ACTIVITY_LOG_PAGE_SIZE },
    {
      skip:
        !calendar?.id || activeSection !== HolidayCalendarSections.Activities,
    },
  )
  const [fetchActivityLogPage] = useLazyGetHolidayCalendarActivitiesQuery()

  const activityLog = useActivityLog({
    idOrKey: calendar?.id,
    query: activitiesQuery,
    fetchPage: fetchActivityLogPage,
    exportFilename: `holiday-calendar-${calendar?.key ?? key}-activity`,
  })

  const { hasPermissionClaim } = useAuth()
  const canUpdate = hasPermissionClaim('Permissions.HolidayCalendars.Update')
  const canDelete = hasPermissionClaim('Permissions.HolidayCalendars.Delete')

  useDocumentTitle(
    calendar
      ? `${calendar.name} - Holiday Calendar Details`
      : 'Holiday Calendar Details',
  )

  useEffect(() => {
    if (error) {
      messageApi.error(
        (isApiError(error) ? error.detail : undefined) ??
          'An error occurred while loading the holiday calendar.',
      )
      console.error(error)
    }
  }, [error, messageApi])

  const actionsMenuItems: ItemType[] = [
    ...(canUpdate
      ? [{ key: 'edit', label: 'Edit', onClick: () => setDialog('edit') }]
      : []),
    ...(canDelete
      ? [
          {
            key: 'delete',
            label: 'Delete',
            onClick: () => setDialog('delete'),
          },
        ]
      : []),
  ]

  if (isLoading) {
    return <HolidayCalendarDetailsLoading />
  }

  if (!calendar) {
    return notFound()
  }

  const sections: RecordSection[] = [
    {
      id: HolidayCalendarSections.Holidays,
      label: 'Holidays',
      count: calendar.holidays.length,
    },
    { id: HolidayCalendarSections.Activities, label: 'Activity' },
  ]

  const renderSection = (section: string) => {
    switch (section as HolidayCalendarSections) {
      case HolidayCalendarSections.Holidays:
        return (
          <HolidaysGrid
            calendarId={calendar.id}
            holidays={calendar.holidays}
            canUpdate={canUpdate}
            onRefresh={refetch}
          />
        )
      case HolidayCalendarSections.Activities:
        return <ActivityLogTimeline {...activityLog.timelineProps} />
      default:
        return null
    }
  }

  const sectionActions =
    activeSection === HolidayCalendarSections.Activities ? (
      <ActivityLogExportButton activityLog={activityLog} />
    ) : canUpdate ? (
      <Button size="small" onClick={() => setDialog('add-holiday')}>
        Add Holiday
      </Button>
    ) : undefined

  return (
    <>
      <RecordLayout
        sections={sections}
        defaultSection={HolidayCalendarSections.Holidays}
        record={{
          name: calendar.name,
          recordKey: String(calendar.key),
          parent: {
            label: 'Holiday Calendars',
            href: '/settings/organization/holiday-calendars',
          },
          subtitle: 'Holiday Calendar Details',
          actions:
            actionsMenuItems.length > 0 ? (
              <PageActions actionItems={actionsMenuItems} />
            ) : undefined,
        }}
        facts={
          <Flex vertical gap={10}>
            <LabeledContent label="Key">{calendar.key}</LabeledContent>
            {calendar.isDefault && (
              <LabeledContent label="Default">
                <Tag color="blue">System default</Tag>
              </LabeledContent>
            )}
            <LabeledContent label="Operating Models">
              {calendar.operatingModelCount}
            </LabeledContent>
            {calendar.description && (
              <LabeledContent label="Description">
                {calendar.description}
              </LabeledContent>
            )}
          </Flex>
        }
        sectionActions={sectionActions}
      >
        {(section) => renderSection(section)}
      </RecordLayout>

      {dialog === 'edit' && (
        <HolidayCalendarForm
          calendar={calendar}
          onFormComplete={() => setDialog(null)}
          onFormCancel={() => setDialog(null)}
        />
      )}
      {dialog === 'delete' && (
        <DeleteHolidayCalendarForm
          calendar={calendar}
          onFormComplete={() => {
            setDialog(null)
            router.push('/settings/organization/holiday-calendars')
          }}
          onFormCancel={() => setDialog(null)}
        />
      )}
      {dialog === 'add-holiday' && (
        <HolidayForm
          calendarId={calendar.id}
          onFormComplete={() => setDialog(null)}
          onFormCancel={() => setDialog(null)}
        />
      )}
    </>
  )
}

const HolidayCalendarDetailsPageWithAuthorization = authorizePage(
  HolidayCalendarDetailsPage,
  'Permission',
  'Permissions.HolidayCalendars.View',
)

export default HolidayCalendarDetailsPageWithAuthorization
