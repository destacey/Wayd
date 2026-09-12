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

// A batch is a seed's fifteen files or a split export's handful; the list cap is well past either.
const GROUP_PAGE_SIZE = 100

/**
 * The other runs of the batch this one was submitted in, so a reader can step through a seed or a
 * split export from any of its files without going back to the list.
 */
const SubmittedWith = ({ importProcess }: ImportFactsProps) => {
  const { data } = useGetImportProcessesQuery(
    {
      submissionGroupId: importProcess.submissionGroupId,
      pageSize: GROUP_PAGE_SIZE,
    },
    { skip: !importProcess.submissionGroupId },
  )

  if (!importProcess.submissionGroupId) return null

  const others = data?.processes?.filter((p) => p.id !== importProcess.id) ?? []

  return (
    <RecordFactsGroup label="Submitted With">
      <Flex vertical gap={6}>
        {others.length === 0 ? (
          <Typography.Text type="secondary">
            No other imports in this batch.
          </Typography.Text>
        ) : (
          others.map((other) => (
            <Flex key={other.id} gap={8} align="center" wrap>
              <Link href={`/settings/imports/${other.id}`}>{other.displayName}</Link>
              <ImportStatusTag status={other.status} />
            </Flex>
          ))
        )}
      </Flex>
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
