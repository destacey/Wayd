'use client'

import { Alert, Card, Flex } from 'antd'
import PageTitle from '@/src/components/common/page-title'
import { authorizePage, requireFeatureFlag } from '@/src/components/hoc'
import useAuth from '@/src/components/contexts/auth'
import { useDocumentTitle } from '@/src/hooks'
import {
  ACTIVITY_LOG_PAGE_SIZE,
  ActivityLogExportButton,
  ActivityLogTimeline,
  useActivityLog,
} from '@/src/components/common/activities'
import {
  PRODUCT_TAG_SETTINGS_ACTIVITY_ID,
  useGetProductTagSettingsActivitiesQuery,
  useGetProductTagSettingsQuery,
  useLazyGetProductTagSettingsActivitiesQuery,
} from '@/src/store/features/admin/system-settings-api'
import ProductTagSettingsForm from './_components/product-tag-settings-form'

const ProductTagSettingsPage = () => {
  useDocumentTitle('Product Tags')

  const { hasPermissionClaim } = useAuth()
  const canUpdate = hasPermissionClaim('Permissions.SystemSettings.Update')

  const { data: settings, isLoading, isError } = useGetProductTagSettingsQuery()

  const activitiesQuery = useGetProductTagSettingsActivitiesQuery({
    idOrKey: PRODUCT_TAG_SETTINGS_ACTIVITY_ID,
    page: 1,
    pageSize: ACTIVITY_LOG_PAGE_SIZE,
  })
  const [fetchActivityLogPage] = useLazyGetProductTagSettingsActivitiesQuery()
  const activityLog = useActivityLog({
    idOrKey: PRODUCT_TAG_SETTINGS_ACTIVITY_ID,
    query: activitiesQuery,
    fetchPage: fetchActivityLogPage,
    exportFilename: 'product-tag-settings-activity',
  })

  return (
    <div className="page-gutters">
      <PageTitle
        title="Product Tags"
        subtitle="How products are tagged across the organization."
      />
      <Flex vertical gap="middle">
        <Card title="Free tags" size="small">
          {/* An unloaded setting would show as an unchecked switch, which reads as off. */}
          {isError ? (
            <Alert
              type="error"
              showIcon
              title="Could not load the product tag settings. Reload the page to try again."
            />
          ) : (
            <ProductTagSettingsForm
              settings={settings}
              isLoading={isLoading}
              canUpdate={canUpdate}
            />
          )}
        </Card>
        <Card
          title="Activity"
          size="small"
          extra={<ActivityLogExportButton activityLog={activityLog} />}
        >
          <ActivityLogTimeline
            {...activityLog.timelineProps}
            emptyDescription="No changes yet. Until the first save, free tags are off."
          />
        </Card>
      </Flex>
    </div>
  )
}

export default requireFeatureFlag(
  authorizePage(
    ProductTagSettingsPage,
    'Permission',
    'Permissions.SystemSettings.View',
  ),
  'product-management',
)
