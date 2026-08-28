'use client'

import { RecordLoading } from '@/src/components/common/record'
import SettingsRecordShell from '../../../_components/settings-record-shell'

export default function ProjectLifecycleDetailsLoading() {
  return (
    <SettingsRecordShell>
      <RecordLoading title="Project Lifecycle Details" />
    </SettingsRecordShell>
  )
}
