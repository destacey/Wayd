'use client'

import { LabeledContent } from '@/src/components/common/content'
import { RecordFactsGroup } from '@/src/components/common/record'
import { WorkspaceDto } from '@/src/services/wayd-api'
import { Divider, Flex, Typography } from 'antd'
import Link from 'next/link'

const { Text } = Typography

export interface WorkspaceFactsProps {
  workspace: WorkspaceDto
}

/**
 * A workspace's stable facts, for the details panel.
 *
 * Its own attributes, then the work process it runs on — reference material
 * you consult while working in the work items beside it, which is why these
 * are a panel rather than the section that opens first.
 */
const WorkspaceFacts = ({ workspace }: WorkspaceFactsProps) => (
  <>
    <Flex vertical gap={10}>
      <LabeledContent label="Ownership">
        {workspace.ownership.name}
      </LabeledContent>

      {workspace.description && (
        <LabeledContent label="Description">
          <Text>{workspace.description}</Text>
        </LabeledContent>
      )}
    </Flex>

    <Divider size="small" style={{ margin: 0 }} />

    <RecordFactsGroup label="Relationships">
      <LabeledContent label="Work Process">
        <Link
          href={`/settings/work-management/work-processes/${workspace.workProcess.id}`}
        >
          {workspace.workProcess.name}
        </Link>
      </LabeledContent>
    </RecordFactsGroup>
  </>
)

export default WorkspaceFacts
