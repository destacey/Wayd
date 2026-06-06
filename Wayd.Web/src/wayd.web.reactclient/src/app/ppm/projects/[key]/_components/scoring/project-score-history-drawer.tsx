'use client'

import {
  useGetProjectScoreQuery,
  useGetProjectScoresQuery,
} from '@/src/store/features/ppm/project-scores-api'
import { Button, Drawer, Empty, List, Spin, Tag, Typography } from 'antd'
import { ArrowLeftOutlined } from '@ant-design/icons'
import { FC, useState } from 'react'
import dayjs from 'dayjs'
import ProjectScoreDetail from './project-score-detail'

const { Text } = Typography

export interface ProjectScoreHistoryDrawerProps {
  projectId: string
  open: boolean
  onClose: () => void
}

const ProjectScoreHistoryDrawer: FC<ProjectScoreHistoryDrawerProps> = ({
  projectId,
  open,
  onClose,
}) => {
  const [selectedScoreId, setSelectedScoreId] = useState<string | null>(null)

  const { data: scores = [], isLoading } = useGetProjectScoresQuery(
    { projectId },
    { skip: !open },
  )

  const { data: selectedScore, isFetching: isFetchingScore } =
    useGetProjectScoreQuery(
      { projectId, scoreId: selectedScoreId ?? '' },
      { skip: !selectedScoreId },
    )

  const handleClose = () => {
    setSelectedScoreId(null)
    onClose()
  }

  return (
    <Drawer
      title={selectedScoreId ? 'Score details' : 'Scoring history'}
      open={open}
      onClose={handleClose}
      size={480}
      destroyOnHidden
    >
      {selectedScoreId ? (
        <>
          <Button
            type="link"
            icon={<ArrowLeftOutlined />}
            style={{ paddingLeft: 0, marginBottom: 8 }}
            onClick={() => setSelectedScoreId(null)}
          >
            Back to history
          </Button>
          {isFetchingScore || !selectedScore ? (
            <Spin />
          ) : (
            <ProjectScoreDetail score={selectedScore} />
          )}
        </>
      ) : isLoading ? (
        <Spin />
      ) : scores.length === 0 ? (
        <Empty description="This project has not been scored yet." />
      ) : (
        <List
          dataSource={scores}
          rowKey={(s) => s.id}
          renderItem={(score) => (
            <List.Item
              onClick={() => setSelectedScoreId(score.id)}
              style={{ cursor: 'pointer' }}
              actions={[
                <Typography.Link key="view">View</Typography.Link>,
              ]}
            >
              <List.Item.Meta
                title={
                  <>
                    <Text strong>{score.primaryValue}</Text>{' '}
                    <Tag>{score.scoringModelName}</Tag>
                  </>
                }
                description={
                  <>
                    {dayjs(score.scoredOn).format('MMM D, YYYY h:mm A')}
                    {score.scoredBy ? ` · ${score.scoredBy.name}` : ''}
                  </>
                }
              />
            </List.Item>
          )}
        />
      )}
    </Drawer>
  )
}

export default ProjectScoreHistoryDrawer
