'use client'

import LinksCard from '@/src/components/common/links/links-card'
import { PlanningIntervalDetailsDto } from '@/src/services/wayd-api'
import { Col, Descriptions, DescriptionsProps, Divider, Row, Space } from 'antd'
import dayjs from 'dayjs'
import { PlanningIntervalIterationsList } from '.'
import { MarkdownRenderer } from '@/src/components/common/markdown'

const { Item } = Descriptions

interface PlanningIntervalDetailsProps {
  planningInterval: PlanningIntervalDetailsDto
}

const PlanningIntervalDetails = ({
  planningInterval,
}: PlanningIntervalDetailsProps) => {
  const detailsItems: DescriptionsProps['items'] = [
    {
      key: 'start',
      label: 'Start',
      children: dayjs(planningInterval.start).format('MMM D, YYYY'),
    },
    {
      key: 'end',
      label: 'End',
      children: dayjs(planningInterval.end).format('MMM D, YYYY'),
    },
    {
      key: 'objectivesLocked',
      label: 'Objectives Locked?',
      children: planningInterval.objectivesLocked ? 'Yes' : 'No',
    },
  ]

  if (!planningInterval) return null

  return (
    <>
      <Row>
        <Col xs={24} sm={24} md={11} lg={13} xl={16}>
          <Descriptions
            size="small"
            column={{ xs: 1, sm: 1, md: 1, lg: 3, xl: 4, xxl: 5 }}
            items={detailsItems}
          />
          {planningInterval.description && (
            <Descriptions layout="vertical" size="small">
              <Item label="Description">
                <MarkdownRenderer markdown={planningInterval.description} />
              </Item>
            </Descriptions>
          )}
        </Col>
        <Col xs={24} sm={24} md={13} lg={11} xl={8}>
          <PlanningIntervalIterationsList
            planningIntervalKey={planningInterval?.key}
          />
        </Col>
      </Row>
      <Divider />
      <Space align="start" wrap>
        <LinksCard objectId={planningInterval.id} />
      </Space>
    </>
  )
}

export default PlanningIntervalDetails
