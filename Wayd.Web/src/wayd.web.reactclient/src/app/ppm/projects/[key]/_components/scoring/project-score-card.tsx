'use client'

import { useGetProjectScoringContextQuery } from '@/src/store/features/ppm/project-scores-api'
import {
  NavigationDto,
  ScoreSummaryDto,
} from '@/src/services/wayd-api'
import {
  Button,
  Card,
  Divider,
  Empty,
  Flex,
  Statistic,
  Typography,
  theme,
} from 'antd'
import { FC, useState } from 'react'
import dayjs from 'dayjs'
import RecordProjectScoreForm from './record-project-score-form'
import ProjectScoreHistoryDrawer from './project-score-history-drawer'

const { Text, Title } = Typography

export interface ProjectScoreCardProps {
  projectId: string
  /** The portfolio's assigned scoring model, or undefined when scoring is not enabled. */
  scoringModel?: NavigationDto
  /** The project's current score summary, or undefined if not yet scored. */
  currentScore?: ScoreSummaryDto
  /** Whether the current user may record a score (project/portfolio/program Owner or Manager). */
  canManageProject: boolean
  /**
   * "card" renders as a standalone bordered Card (default). "section" renders
   * borderless with an inline header, for embedding inside another card such as
   * the project details sidebar.
   */
  variant?: 'card' | 'section'
}

const ProjectScoreCard: FC<ProjectScoreCardProps> = ({
  projectId,
  scoringModel,
  currentScore,
  canManageProject,
  variant = 'card',
}) => {
  const { token } = theme.useToken()

  const [openForm, setOpenForm] = useState(false)
  const [openHistory, setOpenHistory] = useState(false)

  // The model definition (criteria/scales) and re-score seed are only needed when the form opens, so
  // the context is fetched lazily rather than on every card render.
  const { data: context, isFetching: isContextLoading } =
    useGetProjectScoringContextQuery({ projectId }, { skip: !openForm })

  // Scoring isn't enabled for this project's portfolio — don't render anything.
  if (!scoringModel) {
    return null
  }

  const historyLink = currentScore && (
    <Typography.Link onClick={() => setOpenHistory(true)}>
      View history
    </Typography.Link>
  )

  const body = (
    <Flex vertical gap="middle">
      {currentScore ? (
        <Statistic
          title="Score"
          value={currentScore.value}
          precision={2}
          styles={{ content: { color: token.colorPrimary } }}
        />
      ) : (
        <Empty
          image={Empty.PRESENTED_IMAGE_SIMPLE}
          description="This project has not been scored yet."
        />
      )}

      {currentScore && (
        <Text type="secondary">
          Scored {dayjs(currentScore.scoredOn).format('MMM D, YYYY')}
          {currentScore.scoredBy ? ` by ${currentScore.scoredBy.name}` : ''}
        </Text>
      )}

      <Flex justify="space-between" align="center">
        <Text type="secondary">Model: {scoringModel.name}</Text>
        {canManageProject && (
          <Button
            type="primary"
            loading={openForm && isContextLoading}
            onClick={() => setOpenForm(true)}
          >
            {currentScore ? 'Re-score' : 'Score'}
          </Button>
        )}
      </Flex>
    </Flex>
  )

  return (
    <>
      {variant === 'card' ? (
        <Card size="small" title="Priority Score" extra={historyLink}>
          {body}
        </Card>
      ) : (
        <>
          <Divider size="small" />
          <Flex vertical gap="middle">
            <Flex justify="space-between" align="center">
              <Title level={5} style={{ margin: 0 }}>
                Priority Score
              </Title>
              {historyLink}
            </Flex>
            {body}
          </Flex>
        </>
      )}

      {openForm && context?.scoringModel && (
        <RecordProjectScoreForm
          projectId={projectId}
          scoringModel={context.scoringModel}
          modelArchived={context.scoringModelArchived}
          currentScore={context.currentScore}
          onFormComplete={() => setOpenForm(false)}
          onFormCancel={() => setOpenForm(false)}
        />
      )}

      <ProjectScoreHistoryDrawer
        projectId={projectId}
        open={openHistory}
        onClose={() => setOpenHistory(false)}
      />
    </>
  )
}

export default ProjectScoreCard
