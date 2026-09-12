'use client'

import { LabeledContent } from '@/src/components/common/content'
import { RecordFactsGroup } from '@/src/components/common/record'
import { formatDateTime } from '@/src/components/common/wayd-grid'
import { ImportAtomicity, ImportProcessDto } from '@/src/services/wayd-api'
import { useGetImportProcessesQuery } from '@/src/store/features/admin/imports-api'
import { Flex, Tooltip, Typography } from 'antd'
import Link from 'next/link'
import { ImportStatusTag } from './import-status-tag'

export interface ImportFactsProps {
  importProcess: ImportProcessDto
}

// The server's cap. A batch is a seed's fifteen files or a split export's handful, so one page holds
// it; a caller who posts more under one id is told how many are not listed rather than shown a
// list that looks complete.
const GROUP_PAGE_SIZE = 500

/**
 * The other runs of the batch this one was submitted in, so a reader can step through a seed or a
 * split export from any of its files without going back to the list.
 */
const SubmittedWith = ({ importProcess }: ImportFactsProps) => {
  const { data, isLoading, isError } = useGetImportProcessesQuery(
    {
      submissionGroupId: importProcess.submissionGroupId,
      pageSize: GROUP_PAGE_SIZE,
    },
    { skip: !importProcess.submissionGroupId },
  )

  if (!importProcess.submissionGroupId) return null

  const listed = data?.processes ?? []
  const others = listed.filter((p) => p.id !== importProcess.id)
  // Beyond the page: whatever the server counted that the page did not carry, less this run if it
  // was one of the listed ones.
  const unlisted = data ? data.totalCount - listed.length : 0

  // Empty means "the server said so", never "not answered yet" or "could not ask".
  const body = isLoading ? (
    <Typography.Text type="secondary">Loading…</Typography.Text>
  ) : isError || !data ? (
    <Typography.Text type="danger">
      Could not load the rest of this batch.
    </Typography.Text>
  ) : others.length === 0 && unlisted === 0 ? (
    <Typography.Text type="secondary">
      No other imports in this batch.
    </Typography.Text>
  ) : (
    <>
      {others.map((other) => (
        <Flex key={other.id} gap={8} align="center" wrap>
          <Link href={`/settings/imports/${other.id}`}>{other.displayName}</Link>
          <ImportStatusTag status={other.status} />
        </Flex>
      ))}
      {unlisted > 0 && (
        <Typography.Text type="secondary">
          {`and ${unlisted} more not listed`}
        </Typography.Text>
      )}
    </>
  )

  return (
    <RecordFactsGroup label="Submitted With">
      <Flex vertical gap={6}>{body}</Flex>
    </RecordFactsGroup>
  )
}

/**
 * A run's stable facts, for the details panel.
 *
 * How the import applies is here because the counts cannot explain themselves: "0 of 40 applied" reads
 * as a bug until you know one rejected row keeps the whole file out.
 */
const ImportFacts = ({ importProcess }: ImportFactsProps) => (
  <>
    <Flex vertical gap={10}>
      <LabeledContent label="Submitted By">
        {importProcess.submittedByName ?? importProcess.submittedByUserId}
      </LabeledContent>
      <LabeledContent label="Rows">{importProcess.totalRowCount}</LabeledContent>
      <LabeledContent label="Applies">
        {importProcess.atomicity === ImportAtomicity.Atomic ? (
          <Tooltip title="One rejected row keeps the whole file out.">
            <span>All or nothing</span>
          </Tooltip>
        ) : (
          <Tooltip title="Each row applies on its own; a rejected row does not stop the rest.">
            <span>Row by row</span>
          </Tooltip>
        )}
      </LabeledContent>
    </Flex>

    <RecordFactsGroup label="Timing">
      <Flex vertical gap={10}>
        <LabeledContent label="Submitted">
          {formatDateTime(importProcess.submittedOn)}
        </LabeledContent>
        <LabeledContent label="Started">
          {formatDateTime(importProcess.startedOn) || 'Not yet'}
        </LabeledContent>
        <LabeledContent label="Finished">
          {formatDateTime(importProcess.completedOn) || 'In progress'}
        </LabeledContent>
      </Flex>
    </RecordFactsGroup>

    <SubmittedWith importProcess={importProcess} />
  </>
)

export default ImportFacts
