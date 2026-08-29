'use client'

import { ProjectScoreDetailsDto } from '@/src/services/wayd-api'
import {
  Descriptions,
  Space,
  Statistic,
  Table,
  Tag,
  Typography,
  theme,
} from 'antd'
import { FC } from 'react'
import dayjs from 'dayjs'

const { Text } = Typography

export interface ProjectScoreDetailProps {
  score: ProjectScoreDetailsDto
}

const ProjectScoreDetail: FC<ProjectScoreDetailProps> = ({ score }) => {
  const { token } = theme.useToken()

  const sortedRatings = [...(score.ratings ?? [])].sort((a, b) => a.order - b.order)
  const sortedOutputs = [...(score.outputs ?? [])].sort((a, b) => a.order - b.order)

  return (
    <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
      <Descriptions size="small" column={1}>
        <Descriptions.Item label="Model">
          {score.scoringModelName} (#{score.scoringModelKey})
        </Descriptions.Item>
        <Descriptions.Item label="Scored on">
          {dayjs(score.scoredOn).format('MMM D, YYYY h:mm A')}
        </Descriptions.Item>
        {score.scoredBy && (
          <Descriptions.Item label="Scored by">
            {score.scoredBy.name}
          </Descriptions.Item>
        )}
      </Descriptions>

      <Space size="large" wrap>
        {sortedOutputs.map((output) => (
          <Statistic
            key={output.token}
            title={
              <Space size="small">
                {output.name}
                {output.isPrimary && <Tag color={token.colorPrimary}>Score</Tag>}
              </Space>
            }
            value={output.value}
            precision={2}
            styles={
              output.isPrimary
                ? { content: { color: token.colorPrimary } }
                : undefined
            }
          />
        ))}
      </Space>

      <div>
        <Text strong>Ratings</Text>
        <Table
          size="small"
          rowKey={(r) => r.criterionId}
          pagination={false}
          dataSource={sortedRatings}
          columns={[
            {
              title: 'Criterion',
              dataIndex: 'criterionName',
              key: 'criterionName',
              render: (name: string, r) => (
                <Space size="small">
                  <Text>{name}</Text>
                  <Tag>{r.criterionToken}</Tag>
                </Space>
              ),
            },
            {
              title: 'Rating',
              key: 'rating',
              align: 'right',
              render: (_: unknown, r) =>
                r.ratingLevelLabel
                  ? `${r.ratingLevelLabel} (${r.ratingValue})`
                  : r.ratingValue,
            },
          ]}
        />
      </div>
    </Space>
  )
}

export default ProjectScoreDetail
