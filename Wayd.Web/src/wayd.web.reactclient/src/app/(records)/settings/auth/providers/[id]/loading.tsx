'use client'

import { RecordLoading } from '@/src/components/common/record'
import SettingsRecordShell from '../../../_components/settings-record-shell'

export default function OidcProviderDetailsLoading() {
  return (
    <SettingsRecordShell>
      <RecordLoading title="Identity Provider" />
    </SettingsRecordShell>
  )
}
