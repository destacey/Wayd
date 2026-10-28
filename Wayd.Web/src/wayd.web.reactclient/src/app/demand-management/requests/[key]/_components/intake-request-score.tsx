'use client'

import {
  IntakeRequestScoreDto,
  IntakeRequestScoringDto,
} from '@/src/services/wayd-api'
import { useGetIntakeRequestScoringQuery } from '@/src/store/features/demand-management/demand-management-api'
import {
  Alert,
  Descriptions,
  Empty,
  Flex,
  Skeleton,
  Space,
  Statistic,
  Table,
  Tag,
  Typography,
} from 'antd'
import dayjs from 'dayjs'
import { FC } from 'react'

const { Text } = Typography

export interface IntakeRequestScoreProps {
  requestId: string
  /** Whether the request is open; a closed one is not scored or ranked until it is moved back. */
  isOpen: boolean
}

/** "2nd of 7 open requests on this desk". */
export const deskRankText = (rank: IntakeRequestScoringDto['deskRank']) => {
  if (!rank) return undefined
  const n = rank.position
  const suffix =
    n % 100 >= 11 && n % 100 <= 13
      ? 'th'
      : (({ 1: 'st', 2: 'nd', 3: 'rd' } as Record<number, string>)[n % 10] ??
        'th')
  return `${n}${suffix} of ${rank.of} open ${rank.of === 1 ? 'request' : 'requests'} on this desk`
}

const ScoreBreakdown: FC<{ score: IntakeRequestScoreDto }> = ({ score }) => (
  <Flex vertical gap="middle">
    <Space size="large" wrap>
      {score.outputs.map((output) => (
        <Statistic
          key={output.token}
          title={
            <Space size="small">
              {output.name}
              {output.isPrimary && <Tag color="processing">Score</Tag>}
            </Space>
          }
          value={output.value}
          precision={2}
        />
      ))}
    </Space>
    <Table
      size="small"
      rowKey={(r) => r.criterionId}
      pagination={false}
      dataSource={score.ratings}
      columns={[
        {
          title: 'Criterion',
          key: 'criterion',
          render: (_: unknown, r) => (
            <Space size="small">
              <Text>{r.criterionName}</Text>
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
  </Flex>
)

/**
 * A request's Score section: its current score, where that puts it among the open requests on its desk, the
 * ratings behind it, and every earlier score. A request scored months apart shows how its score moved when
 * the facts moved. Shown only to a reader who can read the request's substance, the tier its score is in.
 */
const IntakeRequestScore: FC<IntakeRequestScoreProps> = ({
  requestId,
  isOpen,
}) => {
  const {
    data: scoring,
    isLoading,
    isError,
  } = useGetIntakeRequestScoringQuery(requestId)

  if (isLoading) return <Skeleton active paragraph={{ rows: 4 }} />
  if (isError || !scoring)
    return (
      <Alert type="error" showIcon title="The score could not be loaded." />
    )

  const [current, ...earlier] = scoring.scores

  return (
    <Flex vertical gap="large">
      {scoring.scoringModelArchived && (
        <Alert
          type="warning"
          showIcon
          title="This desk's scoring model has been archived."
          description="Requests can still be scored with it, but a Desk Admin should give the desk a current model."
        />
      )}
      {!scoring.scoringModel && (
        <Alert
          type="info"
          showIcon
          title="This request's desk does not score its requests."
          description={
            current
              ? 'Its scores are kept, but no longer rank with the desk’s other requests.'
              : 'A Desk Admin can choose a scoring model in the desk settings.'
          }
        />
      )}
      {!isOpen && scoring.scoringModel && (
        <Alert
          type="info"
          showIcon
          title="Scoring is frozen while the request is closed."
          description="It is not ranked with the desk's open requests, and can be scored again once it is moved back to an open status."
        />
      )}
      {current ? (
        <>
          <Flex gap="large" wrap align="end">
            <Statistic
              title="Score"
              value={current.primaryValue}
              precision={2}
            />
            {deskRankText(scoring.deskRank) && (
              <Text strong>{deskRankText(scoring.deskRank)}</Text>
            )}
          </Flex>
          {isOpen &&
            scoring.scoringModel &&
            current.scoringModel.id !== scoring.scoringModel.id && (
              <Alert
                type="info"
                showIcon
                title={`Scored with ${current.scoringModel.name}, which this desk no longer uses, so it does not rank with the desk's other requests.`}
              />
            )}
          <Descriptions size="small" column={1}>
            <Descriptions.Item label="Model">
              {current.scoringModel.name}
            </Descriptions.Item>
            <Descriptions.Item label="Scored">
              {dayjs(current.scoredOn).format('MMM D, YYYY h:mm A')} by{' '}
              {current.scoredBy.name}
            </Descriptions.Item>
          </Descriptions>
          <ScoreBreakdown score={current} />
        </>
      ) : (
        scoring.scoringModel && (
          <Empty
            description={
              scoring.canScore
                ? 'Not scored yet. Score it to rank it with the desk’s other requests.'
                : 'Not scored yet.'
            }
          />
        )
      )}
      {earlier.length > 0 && (
        <Flex vertical gap="small">
          <Text strong>Earlier Scores</Text>
          <Table
            size="small"
            rowKey={(s) => s.id}
            pagination={false}
            dataSource={earlier}
            expandable={{
              expandedRowRender: (s) => <ScoreBreakdown score={s} />,
            }}
            columns={[
              {
                title: 'Scored',
                key: 'scoredOn',
                render: (_: unknown, s) =>
                  dayjs(s.scoredOn).format('MMM D, YYYY'),
              },
              {
                title: 'Score',
                key: 'value',
                align: 'right',
                render: (_: unknown, s) => s.primaryValue.toFixed(2),
              },
              {
                title: 'By',
                key: 'scoredBy',
                render: (_: unknown, s) => s.scoredBy.name,
              },
              {
                title: 'Model',
                key: 'model',
                render: (_: unknown, s) => s.scoringModel.name,
              },
            ]}
          />
        </Flex>
      )}
    </Flex>
  )
}

export default IntakeRequestScore
