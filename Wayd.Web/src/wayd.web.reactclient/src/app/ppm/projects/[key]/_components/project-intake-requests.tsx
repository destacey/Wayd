'use client'

import { requestHref } from '@/src/app/demand-management/_components/intake-requests-grid'
import { outcomeKindLabel } from '@/src/app/demand-management/_components/outcome-kind'
import WaydList from '@/src/components/common/wayd-list'
import { WorkflowStatusTag } from '@/src/components/common/status-workflows'
import { useGetProjectIntakeRequestsQuery } from '@/src/store/features/demand-management/demand-management-api'
import { Alert, Button, Flex, Spin, Tag, Typography } from 'antd'
import NextLink from 'next/link'
import { FC } from 'react'

const { Paragraph, Text } = Typography

export interface ProjectIntakeRequestsProps {
  projectId: string
}

/**
 * The intake requests the project came from or absorbed. Demand Management decides which the reader may
 * see, request by request, so a request on a private desk they are not part of is simply not listed.
 */
const ProjectIntakeRequests: FC<ProjectIntakeRequestsProps> = ({
  projectId,
}) => {
  const { data, isLoading, error, refetch } =
    useGetProjectIntakeRequestsQuery(projectId)

  if (isLoading) return <Spin />

  if (error) {
    return (
      <Alert
        type="error"
        showIcon
        title="The requests could not be loaded."
        action={<Button onClick={() => refetch()}>Try again</Button>}
      />
    )
  }

  if (!data || data.length === 0) {
    return (
      <Paragraph type="secondary" style={{ marginBottom: 0 }}>
        No request you can see is linked to this project.
      </Paragraph>
    )
  }

  return (
    <WaydList
      dataSource={data}
      rowKey={(item) => item.request.id}
      renderItem={(item) => (
        <WaydList.Item>
          <WaydList.Item.Meta
            title={
              <Flex gap={8} align="center" wrap>
                <NextLink href={requestHref(item.request.key)}>
                  {item.request.key} · {item.request.title}
                </NextLink>
                <WorkflowStatusTag
                  name={item.status.name}
                  category={item.status.category}
                />
                <Tag>{outcomeKindLabel[item.kind]}</Tag>
              </Flex>
            }
            description={item.note && <Text>{item.note}</Text>}
          />
        </WaydList.Item>
      )}
    />
  )
}

export default ProjectIntakeRequests
