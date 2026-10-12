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
  SPRINT_METRICS_SETTINGS_ACTIVITY_ID,
  useGetSprintMetricsSettingsActivitiesQuery,
  useGetSprintMetricsSettingsQuery,
  useLazyGetSprintMetricsSettingsActivitiesQuery,
} from '@/src/store/features/admin/system-settings-api'
import SprintMetricsSettingsForm from './_components/sprint-metrics-settings-form'

const SprintMetricsSettingsPage = () => {
  useDocumentTitle('Sprint Metrics')

  const { hasPermissionClaim } = useAuth()
  const canUpdate = hasPermissionClaim('Permissions.SystemSettings.Update')

  const { data: settings, isLoading } = useGetSprintMetricsSettingsQuery()

  const activitiesQuery = useGetSprintMetricsSettingsActivitiesQuery({
    idOrKey: SPRINT_METRICS_SETTINGS_ACTIVITY_ID,
    page: 1,
    pageSize: ACTIVITY_LOG_PAGE_SIZE,
  })
  const [fetchActivityLogPage] =
    useLazyGetSprintMetricsSettingsActivitiesQuery()
  const activityLog = useActivityLog({
    idOrKey: SPRINT_METRICS_SETTINGS_ACTIVITY_ID,
    query: activitiesQuery,
    fetchPage: fetchActivityLogPage,
    exportFilename: 'sprint-metrics-settings-activity',
  })

  return (
    <div className="page-gutters">
      <PageTitle
        title="Sprint Metrics"
        subtitle="How many sprints every team's velocity, predictability and say/do are averaged over."
      />
      <Flex vertical gap="middle">
        <Card title="Window" size="small">
          <SprintMetricsSettingsForm
            settings={settings}
            isLoading={isLoading}
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
            emptyDescription="No changes yet. Until the first save, the default above is in effect."
          />
        </Card>
      </Flex>
    </div>
  )
}

export default authorizePage(
  SprintMetricsSettingsPage,
  'Permission',
  'Permissions.SystemSettings.View',
)
