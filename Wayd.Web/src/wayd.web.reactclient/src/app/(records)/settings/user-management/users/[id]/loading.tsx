'use client'

import { RecordLoading } from '@/src/components/common/record'
import SettingsRecordShell from '../../../_components/settings-record-shell'

export default function UserDetailsLoading() {
  return (
    <SettingsRecordShell>
      <RecordLoading title="User Details" />
    </SettingsRecordShell>
  )
}
