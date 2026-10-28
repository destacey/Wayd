'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import {
  IntakeRequestNeedByDto,
  IntakeRequestScoreDto,
  ScoringModelDetailsDto,
  ScoringModelEvaluationDto,
} from '@/src/services/wayd-api'
import {
  usePreviewIntakeRequestScoreMutation,
  useScoreIntakeRequestMutation,
} from '@/src/store/features/demand-management/demand-management-api'
import { errorsByField, toApiError } from '@/src/utils'
import {
  Alert,
  Card,
  Flex,
  Form,
  InputNumber,
  Modal,
  Select,
  Space,
  Statistic,
  Tag,
  Typography,
} from 'antd'
import { FC, useState } from 'react'
import { NeedByCallout } from '../../../_components/need-by'

const { Text } = Typography

export interface ScoreIntakeRequestFormProps {
  requestId: string
  scoringModel: ScoringModelDetailsDto
  modelArchived: boolean
  /** The request's need-by, shown beside the ratings: a person rates urgency, and no criterion reads the date. */
  needBy: IntakeRequestNeedByDto | undefined
  /** The current score, whose ratings the form starts from. */
  currentScore: IntakeRequestScoreDto | undefined
  onFormComplete: () => void
  onFormCancel: () => void
}

/** A criterion's rating as entered: a level of its scale, or a number for one with no scale. */
interface CriterionRating {
  value: number | null
  ratingLevelId: string | null
}

/**
 * An agent rating a request against its desk's scoring model. It starts from the current score's ratings,
 * so re-scoring after the facts move changes only what moved; saving adds a score and keeps the earlier
 * ones. The preview runs the desk's model on the server, under the same rule as saving.
 */
const ScoreIntakeRequestForm: FC<ScoreIntakeRequestFormProps> = ({
  requestId,
  scoringModel,
  modelArchived,
  needBy,
  currentScore,
  onFormComplete,
  onFormCancel,
}) => {
  const messageApi = useMessage()
  const [score, { isLoading: isSaving }] = useScoreIntakeRequestMutation()
  const [previewScore, { isLoading: isPreviewing }] =
    usePreviewIntakeRequestScoreMutation()

  const criteria = [...scoringModel.criteria].sort((a, b) => a.order - b.order)
  const scales = new Map(scoringModel.scales.map((s) => [s.id, s]))
  const formulas = new Map(
    scoringModel.outputs.map((o) => [o.token, o.formula]),
  )

  // A rating from an earlier model has a criterion id this one does not, so it seeds nothing.
  const [ratings, setRatings] = useState<Record<string, CriterionRating>>(() =>
    Object.fromEntries(
      scoringModel.criteria.map((c) => {
        const previous = currentScore?.ratings.find(
          (r) => r.criterionId === c.id,
        )
        return [
          c.id,
          {
            value: previous?.ratingValue ?? null,
            ratingLevelId: previous?.ratingLevelId ?? null,
          },
        ]
      }),
    ),
  )
  const [ratingErrors, setRatingErrors] = useState<Record<string, string>>({})
  const [preview, setPreview] = useState<ScoringModelEvaluationDto | null>(null)
  const [previewError, setPreviewError] = useState<string | null>(null)

  const rate = (criterionId: string, rating: CriterionRating) => {
    setRatings((prev) => ({ ...prev, [criterionId]: rating }))
    setRatingErrors(({ [criterionId]: _, ...rest }) => rest)
    setPreview(null)
    setPreviewError(null)
  }

  const allRated = criteria.every((c) => ratings[c.id]?.value != null)

  const toRequest = () => ({
    requestId,
    ratings: criteria.map((c) => ({
      criterionId: c.id,
      value: ratings[c.id].ratingLevelId
        ? undefined
        : (ratings[c.id].value ?? undefined),
      ratingLevelId: ratings[c.id].ratingLevelId ?? undefined,
    })),
  })

  const handlePreview = async () => {
    try {
      setPreview(await previewScore(toRequest()).unwrap())
      setPreviewError(null)
    } catch (error) {
      setPreview(null)
      setPreviewError(
        toApiError(error).detail ??
          'The score could not be previewed. Check the ratings and try again.',
      )
    }
  }

  const handleSave = async () => {
    try {
      await score(toRequest()).unwrap()
      messageApi.success('Score recorded.')
      onFormComplete()
    } catch (error) {
      const apiError = toApiError(error)
      // `ratings[i]` is the i-th criterion sent, in the order shown.
      const errors = apiError.errors
        ? errorsByField(apiError.errors, (key) => {
            const match = /^ratings\[(\d+)\]/.exec(key)
            return match ? criteria[Number(match[1])]?.id : undefined
          })
        : {}
      if (apiError.status === 422 && Object.keys(errors).length > 0) {
        setRatingErrors(
          Object.fromEntries(
            Object.entries(errors).map(([id, e]) => [id, e.join(' ')]),
          ),
        )
        messageApi.error('Correct the validation error(s) to continue.')
      } else {
        messageApi.error(apiError.detail ?? 'The score could not be recorded.')
      }
    }
  }

  return (
    <Modal
      title={`Score with ${scoringModel.name}`}
      open
      okText="Save Score"
      okButtonProps={{ disabled: !allRated, loading: isSaving }}
      onOk={handleSave}
      onCancel={onFormCancel}
      width={640}
    >
      <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
        {modelArchived && (
          <Alert
            type="warning"
            showIcon
            title="This desk's scoring model has been archived."
            description="You can still score with it, but a Desk Admin should give the desk a current model."
          />
        )}
        {needBy ? (
          <NeedByCallout needBy={needBy} />
        ) : (
          <Text type="secondary">The requester gave no need-by date.</Text>
        )}
        <Form layout="vertical" size="small">
          {criteria.map((criterion) => {
            const scale = criterion.scaleId
              ? scales.get(criterion.scaleId)
              : undefined
            const levels = scale
              ? [...scale.levels].sort((a, b) => a.order - b.order)
              : []
            const rating = ratings[criterion.id]
            return (
              <Form.Item
                key={criterion.id}
                label={
                  <Space size="small">
                    <Text strong>{criterion.name}</Text>
                    <Tag>{criterion.token}</Tag>
                  </Space>
                }
                tooltip={
                  criterion.description ||
                  (scale
                    ? `Rated on the "${scale.name}" scale.`
                    : 'Rated with a number.')
                }
                validateStatus={
                  ratingErrors[criterion.id] ? 'error' : undefined
                }
                help={ratingErrors[criterion.id]}
              >
                {scale ? (
                  <Select
                    aria-label={criterion.name}
                    value={rating?.ratingLevelId ?? undefined}
                    onChange={(levelId: string) =>
                      rate(criterion.id, {
                        ratingLevelId: levelId,
                        value:
                          levels.find((l) => l.id === levelId)?.value ?? null,
                      })
                    }
                    placeholder="Choose a rating"
                    options={levels.map((l) => ({
                      value: l.id,
                      label:
                        l.label === l.value.toString()
                          ? l.label
                          : `${l.label} (${l.value})`,
                    }))}
                  />
                ) : (
                  <InputNumber
                    aria-label={criterion.name}
                    style={{ width: '100%' }}
                    value={rating?.value ?? null}
                    onChange={(value) =>
                      rate(criterion.id, { value, ratingLevelId: null })
                    }
                    placeholder="Enter a number"
                  />
                )}
              </Form.Item>
            )
          })}
        </Form>
        <Card size="small">
          <Flex vertical gap="middle">
            <Flex justify="space-between" align="center">
              <Text strong>Preview</Text>
              <Typography.Link
                disabled={!allRated || isPreviewing}
                onClick={handlePreview}
              >
                {isPreviewing ? 'Calculating…' : 'Preview'}
              </Typography.Link>
            </Flex>
            {previewError && (
              <Alert type="error" showIcon title={previewError} />
            )}
            {preview ? (
              <Space size="large" align="start" wrap>
                {[...preview.outputs]
                  .sort((a, b) => a.order - b.order)
                  .map((output) => (
                    <Flex key={output.token} vertical gap={0}>
                      <Statistic
                        title={
                          <Space size="small">
                            {output.name}
                            {output.isPrimary && (
                              <Tag color="processing">Score</Tag>
                            )}
                          </Space>
                        }
                        value={output.value}
                        precision={2}
                      />
                      {formulas.get(output.token) && (
                        <Text type="secondary" code>
                          {output.token} = {formulas.get(output.token)}
                        </Text>
                      )}
                    </Flex>
                  ))}
              </Space>
            ) : (
              !previewError && (
                <Text type="secondary">See the outputs before saving.</Text>
              )
            )}
          </Flex>
        </Card>
      </Space>
    </Modal>
  )
}

export default ScoreIntakeRequestForm
