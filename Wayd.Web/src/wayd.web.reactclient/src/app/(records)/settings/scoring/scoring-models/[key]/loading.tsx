'use client'

import { RecordLoading } from '@/src/components/common/record'
import SettingsRecordShell from '../../../_components/settings-record-shell'

export default function ScoringModelDetailsLoading() {
  return (
    <SettingsRecordShell>
      <RecordLoading title="Scoring Model Details" />
    </SettingsRecordShell>
  )
}
