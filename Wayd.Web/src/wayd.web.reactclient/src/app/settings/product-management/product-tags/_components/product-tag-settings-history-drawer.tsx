'use client'

import {
  ACTIVITY_LOG_PAGE_SIZE,
  ActivityLogExportButton,
  ActivityLogTimeline,
  useActivityLog,
} from '@/src/components/common/activities'
import {
  PRODUCT_TAG_SETTINGS_ACTIVITY_ID,
  useGetProductTagSettingsActivitiesQuery,
  useLazyGetProductTagSettingsActivitiesQuery,
} from '@/src/store/features/admin/system-settings-api'
import { Drawer } from 'antd'

export interface ProductTagSettingsHistoryDrawerProps {
  open: boolean
  onClose: () => void
}

/** Who changed the free tags setting for products, and when. */
const ProductTagSettingsHistoryDrawer = ({
  open,
  onClose,
}: ProductTagSettingsHistoryDrawerProps) => {
  const activitiesQuery = useGetProductTagSettingsActivitiesQuery(
    {
      idOrKey: PRODUCT_TAG_SETTINGS_ACTIVITY_ID,
      page: 1,
      pageSize: ACTIVITY_LOG_PAGE_SIZE,
    },
    { skip: !open },
  )
  const [fetchActivityLogPage] = useLazyGetProductTagSettingsActivitiesQuery()
  const activityLog = useActivityLog({
    idOrKey: PRODUCT_TAG_SETTINGS_ACTIVITY_ID,
    query: activitiesQuery,
    fetchPage: fetchActivityLogPage,
    exportFilename: 'product-tag-settings-activity',
  })

  return (
    <Drawer
      title="Free Tags Setting History"
      placement="right"
      open={open}
      onClose={onClose}
      destroyOnHidden
      extra={<ActivityLogExportButton activityLog={activityLog} />}
    >
      <ActivityLogTimeline
        {...activityLog.timelineProps}
        emptyDescription="No changes yet. Until the first save, free tags are off."
      />
    </Drawer>
  )
}

export default ProductTagSettingsHistoryDrawer
