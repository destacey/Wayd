'use client'

import { Tag } from 'antd'
import { SyncOutlined } from '@ant-design/icons'
import {
  ImportProcessStatus,
  ImportRowStatus,
} from '@/src/services/wayd-api'

const PROCESS_COLOR: Record<ImportProcessStatus, string> = {
  [ImportProcessStatus.Queued]: 'default',
  [ImportProcessStatus.Processing]: 'processing',
  [ImportProcessStatus.Cancelling]: 'warning',
  [ImportProcessStatus.Succeeded]: 'success',
  // Some rows landed and some did not. Warning rather than error: there is something to keep as
  // well as something to fix, and the two need telling apart at a glance.
  [ImportProcessStatus.PartiallySucceeded]: 'warning',
  [ImportProcessStatus.Failed]: 'error',
  [ImportProcessStatus.Cancelled]: 'default',
}

const PROCESS_LABEL: Record<ImportProcessStatus, string> = {
  [ImportProcessStatus.Queued]: 'Queued',
  [ImportProcessStatus.Processing]: 'Processing',
  [ImportProcessStatus.Cancelling]: 'Stopping',
  [ImportProcessStatus.Succeeded]: 'Succeeded',
  [ImportProcessStatus.PartiallySucceeded]: 'Partially Succeeded',
  [ImportProcessStatus.Failed]: 'Failed',
  [ImportProcessStatus.Cancelled]: 'Cancelled',
}

const ROW_COLOR: Record<ImportRowStatus, string> = {
  [ImportRowStatus.Pending]: 'default',
  [ImportRowStatus.Succeeded]: 'success',
  [ImportRowStatus.Failed]: 'error',
  [ImportRowStatus.Cancelled]: 'default',
}

const ROW_LABEL: Record<ImportRowStatus, string> = {
  [ImportRowStatus.Pending]: 'Not Applied',
  [ImportRowStatus.Succeeded]: 'Applied',
  [ImportRowStatus.Failed]: 'Rejected',
  [ImportRowStatus.Cancelled]: 'Cancelled',
}

/** True while the run is still the worker's to finish. */
export const isRunning = (status: ImportProcessStatus) =>
  status === ImportProcessStatus.Queued ||
  status === ImportProcessStatus.Processing ||
  status === ImportProcessStatus.Cancelling

export const ImportStatusTag = ({ status }: { status: ImportProcessStatus }) => (
  <Tag
    color={PROCESS_COLOR[status]}
    icon={
      status === ImportProcessStatus.Processing ? <SyncOutlined spin /> : undefined
    }
  >
    {PROCESS_LABEL[status]}
  </Tag>
)

export const ImportRowStatusTag = ({ status }: { status: ImportRowStatus }) => (
  <Tag color={ROW_COLOR[status]}>{ROW_LABEL[status]}</Tag>
)

export const importRowStatusLabel = (status: ImportRowStatus) => ROW_LABEL[status]
export const importStatusLabel = (status: ImportProcessStatus) =>
  PROCESS_LABEL[status]
