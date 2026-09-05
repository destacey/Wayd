'use client'

import { App } from 'antd'
import { ResumedImport } from '@/src/services/wayd-api'
import {
  useCancelImportProcessMutation,
  useResumeImportProcessMutation,
  useRetryFailedImportRowsMutation,
} from '@/src/store/features/admin/imports-api'
import { useMessage } from '@/src/components/contexts/messaging'

interface ImportInfo {
  id: string
  displayName: string
  failedRowCount: number
  unappliedRowCount: number
}

/**
 * Reports what was actually queued rather than a bare "done". A resume can queue fewer rows than the
 * person expects — anything past its retention window no longer has the data to reapply — and saying so
 * here is the only place they would find out.
 */
const queuedSummary = (result: ResumedImport) => {
  const queued = `${result.queuedRowCount} row${result.queuedRowCount === 1 ? '' : 's'} queued`

  return result.skippedRowCount > 0
    ? `${queued}. ${result.skippedRowCount} skipped — past the retention window, so the data to reapply is gone.`
    : `${queued}.`
}

const useImportActions = () => {
  const messageApi = useMessage()
  const { modal } = App.useApp()
  const [cancelImport] = useCancelImportProcessMutation()
  const [resumeImport] = useResumeImportProcessMutation()
  const [retryFailedRows] = useRetryFailedImportRowsMutation()

  const handleCancel = (importProcess: ImportInfo, onSuccess?: () => void) => {
    modal.confirm({
      title: 'Stop Import',
      // Deliberately explicit about what stopping does not do. "Cancel" reads like an undo, and the rows
      // already applied are staying applied.
      content: `Stop the ${importProcess.displayName} import? Rows already applied will stay applied — the rest can be resumed afterwards.`,
      okText: 'Stop Import',
      okButtonProps: { danger: true },
      onOk: async () => {
        try {
          await cancelImport(importProcess.id).unwrap()
          messageApi.success('Import asked to stop.')
          onSuccess?.()
        } catch {
          messageApi.error('Failed to stop the import.')
        }
      },
    })
  }

  const handleResume = async (
    importProcess: ImportInfo,
    onSuccess?: () => void,
  ) => {
    try {
      const result = await resumeImport(importProcess.id).unwrap()
      messageApi.success(`Import resumed. ${queuedSummary(result)}`)
      onSuccess?.()
    } catch {
      messageApi.error('Failed to resume the import.')
    }
  }

  const handleRetryFailed = (
    importProcess: ImportInfo,
    onSuccess?: () => void,
  ) => {
    modal.confirm({
      title: 'Retry Rejected Rows',
      content: `Reapply the ${importProcess.failedRowCount} rejected row${importProcess.failedRowCount === 1 ? '' : 's'} from the ${importProcess.displayName} import? Rows that already succeeded are never reapplied.`,
      okText: 'Retry',
      onOk: async () => {
        try {
          const result = await retryFailedRows(importProcess.id).unwrap()
          messageApi.success(`Import queued again. ${queuedSummary(result)}`)
          onSuccess?.()
        } catch {
          messageApi.error('Failed to queue the rejected rows.')
        }
      },
    })
  }

  return { handleCancel, handleResume, handleRetryFailed }
}

export default useImportActions
