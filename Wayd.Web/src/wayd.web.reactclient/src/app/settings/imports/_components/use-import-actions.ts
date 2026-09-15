'use client'

import { App } from 'antd'
import { useRouter } from 'next/navigation'
import { ResumedImport } from '@/src/services/wayd-api'
import {
  useApplyImportPreflightMutation,
  useCancelImportProcessMutation,
  useResumeImportProcessMutation,
  useRetryFailedImportRowsMutation,
} from '@/src/store/features/admin/imports-api'
import { useMessage } from '@/src/components/contexts/messaging'
import { isApiError } from '@/src/utils'

interface ImportInfo {
  id: string
  displayName: string
  totalRowCount: number
  failedRowCount: number
  unappliedRowCount: number
  isPreflight?: boolean
  appliedImportProcessId?: string
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
  const [applyPreflight] = useApplyImportPreflightMutation()
  const router = useRouter()

  const handleCancel = (importProcess: ImportInfo, onSuccess?: () => void) => {
    modal.confirm({
      title: importProcess.isPreflight ? 'Stop Check' : 'Stop Import',
      // Deliberately explicit about what stopping does not do. "Cancel" reads like an undo, and the rows
      // already applied are staying applied. A preflight applied nothing and cannot be resumed.
      content: importProcess.isPreflight
        ? `Stop checking the ${importProcess.displayName} file? Nothing has been imported, and a stopped check cannot be resumed — check the file again instead.`
        : `Stop the ${importProcess.displayName} import? Rows already applied will stay applied — the rest can be resumed afterwards.`,
      okText: importProcess.isPreflight ? 'Stop Check' : 'Stop Import',
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

  /** Imports the rows a preflight checked, and opens the run that does it. */
  const handleApply = (importProcess: ImportInfo) => {
    const rejected =
      importProcess.failedRowCount > 0
        ? ` The check rejected ${importProcess.failedRowCount} of them, and they are included — they will be rejected again unless the data has changed.`
        : ''

    // Not refused: the earlier run may have failed for a reason since fixed. Saying so is what stops an
    // additive import, such as deployments, from being applied twice by someone who came back later.
    const alreadyApplied = !!importProcess.appliedImportProcessId
    const rows =
      importProcess.totalRowCount === 1
        ? 'the 1 row'
        : `the ${importProcess.totalRowCount} rows`

    modal.confirm({
      title: alreadyApplied ? 'Import File Again' : 'Import File',
      content: alreadyApplied
        ? `This check has already been imported. Importing it again resubmits ${rows}: records the first import created may be rejected as duplicates, or created a second time where the import has no natural key. Open the earlier import first if you are not sure how it went.`
        : `Import ${rows} this ${importProcess.displayName} check covered?${rejected} Every row is checked again as it is imported.`,
      okText: alreadyApplied ? 'Import Again' : 'Import',
      okButtonProps: alreadyApplied ? { danger: true } : undefined,
      onOk: async () => {
        try {
          const run = await applyPreflight(importProcess.id).unwrap()
          messageApi.success(
            run.isTerminal ? 'Import finished.' : 'Import started.',
          )
          router.push(`/settings/imports/${run.id}`)
        } catch (error) {
          messageApi.error(
            (isApiError(error) && error.detail) ||
              'Failed to start the import.',
          )
        }
      },
    })
  }

  return { handleCancel, handleResume, handleRetryFailed, handleApply }
}

export default useImportActions
