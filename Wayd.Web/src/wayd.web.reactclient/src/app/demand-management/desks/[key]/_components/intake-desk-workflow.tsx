'use client'

import useAuth from '@/src/components/contexts/auth'
import { useGetIntakeDeskWorkflowQuery } from '@/src/store/features/demand-management/demand-management-api'
import { Button, Flex, Skeleton, Tag, Typography } from 'antd'
import NextLink from 'next/link'
import { FC, useState } from 'react'
import ChangeIntakeDeskWorkflowForm from './change-intake-desk-workflow-form'

const { Text } = Typography

export interface IntakeDeskWorkflowProps {
  deskId: string
  deskName: string
}

/**
 * The review workflow the desk's requests follow, as a desk setting. Its name links to the workflow in
 * Settings for those who can open it there. A Desk Admin is told why it cannot change while requests are on
 * the desk, which the server answers only to them.
 */
const IntakeDeskWorkflow: FC<IntakeDeskWorkflowProps> = ({
  deskId,
  deskName,
}) => {
  const [changing, setChanging] = useState(false)
  const { data: workflow, isLoading } = useGetIntakeDeskWorkflowQuery(deskId)
  const { hasPermissionClaim } = useAuth()

  if (isLoading) return <Skeleton.Input active size="small" />
  if (!workflow) return <Text type="secondary">Not available</Text>

  const name = hasPermissionClaim('Permissions.StatusWorkflows.View') ? (
    <NextLink href={`/settings/status-workflows/${workflow.workflow.key}`}>
      {workflow.workflow.name}
    </NextLink>
  ) : (
    <Text>{workflow.workflow.name}</Text>
  )

  return (
    <Flex vertical gap={4}>
      <Flex gap={8} align="center" wrap>
        {name}
        {workflow.followsOrganizationDefault && <Tag>Organization default</Tag>}
        {workflow.canChange && (
          <Button size="small" onClick={() => setChanging(true)}>
            Change Workflow
          </Button>
        )}
      </Flex>
      {workflow.hasRequests && (
        <Text type="secondary">
          It cannot change while requests are on the desk.
        </Text>
      )}
      {changing && workflow.canChange && (
        <ChangeIntakeDeskWorkflowForm
          deskId={deskId}
          deskName={deskName}
          workflow={workflow}
          onFormComplete={() => setChanging(false)}
          onFormCancel={() => setChanging(false)}
        />
      )}
    </Flex>
  )
}

export default IntakeDeskWorkflow
