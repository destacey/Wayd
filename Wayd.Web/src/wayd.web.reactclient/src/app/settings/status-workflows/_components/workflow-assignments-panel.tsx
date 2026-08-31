'use client'

import useAuth from '@/src/components/contexts/auth'
import { WorkflowAssignmentDto } from '@/src/services/wayd-api'
import { useGetWorkflowAssignmentsQuery } from '@/src/store/features/common/status-workflows-api'
import { Button, Card, Flex, Table, Typography } from 'antd'
import type { ColumnsType } from 'antd/es/table'
import Link from 'next/link'
import { useState } from 'react'
import ReassignWorkflowModal from './reassign-workflow-modal'

const { Text } = Typography

/**
 * Which workflow governs each kind of record, and the way to change it.
 *
 * One row per owner type: every scope is organization-wide today, so a scope
 * column would be a column of blanks.
 */
const WorkflowAssignmentsPanel = () => {
  const [reassigning, setReassigning] = useState<WorkflowAssignmentDto | null>(
    null,
  )

  const { hasPermissionClaim } = useAuth()
  const canReassign = hasPermissionClaim('Permissions.StatusWorkflows.Update')

  const { data: assignments, isLoading, refetch } = useGetWorkflowAssignmentsQuery(undefined)

  const columns: ColumnsType<WorkflowAssignmentDto> = [
    {
      title: 'Record Type',
      key: 'owner',
      render: (_, assignment) => <Text strong>{assignment.owner.name}</Text>,
    },
    {
      title: 'Workflow',
      key: 'workflow',
      render: (_, assignment) => (
        <Link href={`/settings/status-workflows/${assignment.workflow.key}`}>
          {assignment.workflow.name}
        </Link>
      ),
    },
    {
      key: 'actions',
      width: 140,
      align: 'right',
      render: (_, assignment) =>
        canReassign ? (
          <Button size="small" onClick={() => setReassigning(assignment)}>
            Change workflow
          </Button>
        ) : null,
    },
  ]

  return (
    <>
      <Card
        size="small"
        title="In use"
        extra={
          <Text type="secondary" style={{ fontSize: 12 }}>
            The workflow each kind of record currently uses
          </Text>
        }
      >
        <Flex vertical gap={8}>
          <Table
            size="small"
            rowKey={(assignment) => assignment.id}
            dataSource={assignments ?? []}
            columns={columns}
            loading={isLoading}
            pagination={false}
          />
        </Flex>
      </Card>

      {reassigning && (
        <ReassignWorkflowModal
          assignment={reassigning}
          onFormComplete={() => {
            setReassigning(null)
            refetch()
          }}
          onFormCancel={() => setReassigning(null)}
        />
      )}
    </>
  )
}

export default WorkflowAssignmentsPanel
