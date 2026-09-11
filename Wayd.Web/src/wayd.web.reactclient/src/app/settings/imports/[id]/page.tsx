'use client'

import { PageActions } from '@/src/components/common'
import { RecordLayout } from '@/src/components/common/record'
import { formatDateTime } from '@/src/components/common/wayd-grid'
import { useDocumentTitle } from '@/src/hooks'
import { useGetImportProcessByIdQuery } from '@/src/store/features/admin/imports-api'
import { ItemType } from 'antd/es/menu/interface'
import { notFound } from 'next/navigation'
import { use, useEffect, useState } from 'react'
import ImportDetails from '../_components/import-details'
import ImportFacts from '../_components/import-facts'
import { ImportStatusTag, isRunning } from '../_components/import-status-tag'
import useImportActions from '../_components/use-import-actions'
import ImportDetailsLoading from './loading'

const POLLING_INTERVAL_MS = 5000

const OVERVIEW = 'overview'

/**
 * One import run — where a submission that did not finish in its response sends the person who made it,
 * and where the Imports list links each run.
 */
const ImportDetailsPage = (props: { params: Promise<{ id: string }> }) => {
  const { id } = use(props.params)

  // Whether to keep polling is decided by the query's own result, which cannot be expressed in the same
  // call. Held as state and adjusted during render, as the Imports list does.
  const [isPolling, setIsPolling] = useState(false)

  const {
    data: importProcess,
    isLoading,
    error,
  } = useGetImportProcessByIdQuery(id, {
    pollingInterval: isPolling ? POLLING_INTERVAL_MS : 0,
    skipPollingIfUnfocused: true,
  })

  const running = importProcess ? isRunning(importProcess.status) : false

  if (isPolling !== running) {
    setIsPolling(running)
  }

  const { handleCancel, handleResume, handleRetryFailed } = useImportActions()

  useDocumentTitle(
    importProcess ? `${importProcess.displayName} - Import` : 'Import',
  )

  useEffect(() => {
    error && console.error(error)
  }, [error])

  if (isLoading) {
    return <ImportDetailsLoading />
  }

  if (!importProcess) {
    return notFound()
  }

  // Seeing a run and acting on it are separate grants: someone overseeing every import type reads this
  // page but may not change the records the run created. The server refuses either way; this keeps the
  // menu from offering what it would refuse.
  const actionItems: ItemType[] = []
  if (importProcess.canManage) {
    if (running) {
      actionItems.push({
        key: 'cancel',
        label: 'Stop',
        danger: true,
        onClick: () => handleCancel(importProcess),
      })
    } else {
      if (importProcess.unappliedRowCount > 0) {
        actionItems.push({
          key: 'resume',
          label: 'Resume',
          onClick: () => handleResume(importProcess),
        })
      }
      if (importProcess.failedRowCount > 0) {
        actionItems.push({
          key: 'retry-failed',
          label: 'Retry Rejected Rows',
          onClick: () => handleRetryFailed(importProcess),
        })
      }
    }
  }

  return (
    <RecordLayout
      sections={[{ id: OVERVIEW, label: 'Overview' }]}
      defaultSection={OVERVIEW}
      record={{
        name: importProcess.displayName,
        parent: { label: 'Imports', href: '/settings/imports' },
        subtitle: 'Import',
        tags: <ImportStatusTag status={importProcess.status} />,
        descriptor: `Submitted ${formatDateTime(importProcess.submittedOn)}`,
        actions:
          actionItems.length > 0 ? (
            <PageActions actionItems={actionItems} />
          ) : undefined,
      }}
      facts={<ImportFacts importProcess={importProcess} />}
    >
      {() => <ImportDetails importProcess={importProcess} />}
    </RecordLayout>
  )
}

// No authorizePage claim: there is no single import permission to name. The server answers only for a
// run whose import type this viewer may see.
export default ImportDetailsPage
