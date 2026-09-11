'use client'

import { LabeledContent } from '@/src/components/common/content'
import { RecordFactsGroup } from '@/src/components/common/record'
import { formatDateTime } from '@/src/components/common/wayd-grid'
import { ImportAtomicity, ImportProcessDto } from '@/src/services/wayd-api'
import { Flex, Tooltip } from 'antd'

export interface ImportFactsProps {
  importProcess: ImportProcessDto
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
  </>
)

export default ImportFacts
