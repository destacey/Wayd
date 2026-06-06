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
  Flex,
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

  const scoreButton = canManageProject && (
    <Button
      type="primary"
      size="small"
      loading={openForm && isContextLoading}
      onClick={() => setOpenForm(true)}
    >
      {currentScore ? 'Re-score' : 'Score'}
    </Button>
  )

  const body = (
    <Flex justify="space-between" align="center" gap="small" wrap>
      {currentScore ? (
        <Flex align="center" gap="small" wrap>
          <Title
            level={2}
            style={{
              margin: 0,
              color: token.colorPrimary,
              lineHeight: 1,
              whiteSpace: 'nowrap',
            }}
          >
            {currentScore.value.toFixed(2)}
          </Title>
          <Flex vertical>
            <Text style={{ lineHeight: 1.2 }}>
              {currentScore.scoringModelName} ·{' '}
              {dayjs(currentScore.scoredOn).format('MMM D, YYYY')}
            </Text>
            {currentScore.scoredBy && (
              <Text type="secondary" style={{ fontSize: 12, lineHeight: 1.2 }}>
                by {currentScore.scoredBy.name}
              </Text>
            )}
          </Flex>
        </Flex>
      ) : (
        <Text type="secondary">Not yet scored · {scoringModel.name}</Text>
      )}

      {scoreButton}
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
