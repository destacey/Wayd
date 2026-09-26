'use client'

import { Card, Flex } from 'antd'
import PageTitle from '@/src/components/common/page-title'
import { authorizePage } from '@/src/components/hoc'
import useAuth from '@/src/components/contexts/auth'
import { useDocumentTitle } from '@/src/hooks'
import {
  ACTIVITY_LOG_PAGE_SIZE,
  ActivityLogExportButton,
  ActivityLogTimeline,
  useActivityLog,
} from '@/src/components/common/activities'
import {
  SCHEDULING_SETTINGS_ACTIVITY_ID,
  useGetSchedulingSettingsActivitiesQuery,
  useGetSchedulingSettingsQuery,
  useLazyGetSchedulingSettingsActivitiesQuery,
} from '@/src/store/features/admin/system-settings-api'
import { useGetTimeZonesQuery } from '@/src/store/features/common/time-zones-api'
import SchedulingSettingsForm from './_components/scheduling-settings-form'

const SchedulingSettingsPage = () => {
  useDocumentTitle('Scheduling')

  const { hasPermissionClaim } = useAuth()
  const canUpdate = hasPermissionClaim('Permissions.SystemSettings.Update')

  const { data: settings, isLoading: isLoadingSettings } =
    useGetSchedulingSettingsQuery()
  const { data: timeZones, isLoading: isLoadingTimeZones } =
    useGetTimeZonesQuery()

  const activitiesQuery = useGetSchedulingSettingsActivitiesQuery({
    idOrKey: SCHEDULING_SETTINGS_ACTIVITY_ID,
    page: 1,
    pageSize: ACTIVITY_LOG_PAGE_SIZE,
  })
  const [fetchActivityLogPage] = useLazyGetSchedulingSettingsActivitiesQuery()
  const activityLog = useActivityLog({
    idOrKey: SCHEDULING_SETTINGS_ACTIVITY_ID,
    query: activitiesQuery,
    fetchPage: fetchActivityLogPage,
    exportFilename: 'scheduling-settings-activity',
  })

  return (
    <div className="page-gutters">
      <PageTitle
        title="Scheduling"
        subtitle="Organization-wide defaults for team time zones and sprint commitment."
      />
      <Flex vertical gap="middle">
        <Card title="Defaults" size="small">
          <SchedulingSettingsForm
            settings={settings}
            timeZones={timeZones}
            isLoading={isLoadingSettings || isLoadingTimeZones}
            canUpdate={canUpdate}
          />
        </Card>
        <Card
          title="Activity"
          size="small"
          extra={<ActivityLogExportButton activityLog={activityLog} />}
        >
          <ActivityLogTimeline
            {...activityLog.timelineProps}
            emptyDescription="No changes yet. Until the first save, the defaults above are in effect."
          />
        </Card>
      </Flex>
    </div>
  )
}

export default authorizePage(
  SchedulingSettingsPage,
  'Permission',
  'Permissions.SystemSettings.View',
)
