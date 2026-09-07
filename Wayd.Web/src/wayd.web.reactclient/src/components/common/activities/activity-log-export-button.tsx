'use client'

import { DownloadOutlined } from '@ant-design/icons'
import { Button } from 'antd'
import { FC } from 'react'
import { ActivityLog } from './use-activity-log'

export interface ActivityLogExportButtonProps {
  activityLog: ActivityLog
}

/**
 * The activity section's own header action, for a record page's `sectionActions`.
 */
const ActivityLogExportButton: FC<ActivityLogExportButtonProps> = ({
  activityLog,
}) => (
  <Button
    icon={<DownloadOutlined />}
    onClick={activityLog.openExport}
    disabled={activityLog.isExportDisabled}
  >
    Export
  </Button>
)

export default ActivityLogExportButton
