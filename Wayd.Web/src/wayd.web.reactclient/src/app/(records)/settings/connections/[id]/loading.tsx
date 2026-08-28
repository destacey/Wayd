'use client'

import { RecordLoading } from '@/src/components/common/record'
import SettingsRecordShell from '../../_components/settings-record-shell'

export default function ConnectionDetailsLoading() {
  return (
    <SettingsRecordShell>
      <RecordLoading title="Connection Details" />
    </SettingsRecordShell>
  )
}
