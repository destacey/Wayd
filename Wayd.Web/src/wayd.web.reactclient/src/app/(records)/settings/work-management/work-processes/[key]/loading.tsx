'use client'

import { RecordLoading } from '@/src/components/common/record'
import SettingsRecordShell from '../../../_components/settings-record-shell'

export default function WorkProcessDetailsLoading() {
  return (
    <SettingsRecordShell>
      <RecordLoading title="Work Process Details" />
    </SettingsRecordShell>
  )
}
