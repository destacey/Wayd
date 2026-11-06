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
  GENERAL_SETTINGS_ACTIVITY_ID,
  useGetGeneralSettingsActivitiesQuery,
  useGetGeneralSettingsQuery,
  useLazyGetGeneralSettingsActivitiesQuery,
} from '@/src/store/features/admin/system-settings-api'
import GeneralSettingsForm from './_components/general-settings-form'

const GeneralSettingsPage = () => {
  useDocumentTitle('General')

  const { hasPermissionClaim } = useAuth()
  const canUpdate = hasPermissionClaim('Permissions.SystemSettings.Update')

  const { data: settings, isLoading } = useGetGeneralSettingsQuery()

  const activitiesQuery = useGetGeneralSettingsActivitiesQuery({
    idOrKey: GENERAL_SETTINGS_ACTIVITY_ID,
    page: 1,
    pageSize: ACTIVITY_LOG_PAGE_SIZE,
  })
  const [fetchActivityLogPage] = useLazyGetGeneralSettingsActivitiesQuery()
  const activityLog = useActivityLog({
    idOrKey: GENERAL_SETTINGS_ACTIVITY_ID,
    query: activitiesQuery,
    fetchPage: fetchActivityLogPage,
    exportFilename: 'general-settings-activity',
  })

  return (
    <div className="page-gutters">
      <PageTitle
        title="General"
        subtitle="Organization-wide settings that apply across Wayd, including the system time zone."
      />
      <Flex vertical gap="middle">
        <Card title="System" size="small">
          <GeneralSettingsForm
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
            emptyDescription="No changes yet. Until the first save, the defaults above are in effect."
          />
        </Card>
      </Flex>
    </div>
  )
}

export default authorizePage(
  GeneralSettingsPage,
  'Permission',
  'Permissions.SystemSettings.View',
)
