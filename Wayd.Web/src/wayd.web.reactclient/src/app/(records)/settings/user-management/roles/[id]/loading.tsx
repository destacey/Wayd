'use client'

import { RecordLoading } from '@/src/components/common/record'
import SettingsRecordShell from '../../../_components/settings-record-shell'

export default function RoleDetailsLoading() {
  return (
    <SettingsRecordShell>
      <RecordLoading title="Role Details" />
    </SettingsRecordShell>
  )
}
