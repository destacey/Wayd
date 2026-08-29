'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import {
  ScoringModelDetailsDto,
  ProjectScoreDetailsDto,
  ScoringModelEvaluationDto,
} from '@/src/services/wayd-api'
import { useEvaluateScoringModelMutation } from '@/src/store/features/scoring/scoring-models-api'
import { useRecordProjectScoreMutation } from '@/src/store/features/ppm/project-scores-api'
import { isApiError, type ApiError } from '@/src/utils'
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
  theme,
} from 'antd'
import { useMemo, useState } from 'react'

const { Text } = Typography

export interface RecordProjectScoreFormProps {
  projectId: string
  scoringModel: ScoringModelDetailsDto
  modelArchived: boolean
  currentScore?: ProjectScoreDetailsDto
  onFormComplete: () => void
  onFormCancel: () => void
}

// A criterion is rated either by selecting a scale level (we track the level id and its numeric value)
// or by entering a free numeric value.
interface CriterionRating {
  value: number | null
  ratingLevelId: string | null
}

const RecordProjectScoreForm = ({
  projectId,
  scoringModel,
  modelArchived,
  currentScore,
  onFormComplete,
  onFormCancel,
}: RecordProjectScoreFormProps) => {
  const messageApi = useMessage()
  const { token } = theme.useToken()

  const [recordScore, { isLoading: isSaving }] = useRecordProjectScoreMutation()
  const [evaluate] = useEvaluateScoringModelMutation()

  const sortedCriteria = useMemo(
    () => [...(scoringModel.criteria ?? [])].sort((a, b) => a.order - b.order),
    [scoringModel.criteria],
  )

  const scalesById = useMemo(
    () => new Map((scoringModel.scales ?? []).map((s) => [s.id, s])),
    [scoringModel.scales],
  )

  // The evaluate endpoint returns output values but not their formulas; join by token to the model's
  // output definitions so the preview can show how each value is derived.
  const formulasByToken = useMemo(
    () =>
      new Map((scoringModel.outputs ?? []).map((o) => [o.token, o.formula])),
    [scoringModel.outputs],
  )

  // Seed from the current score so re-scoring starts from the last values.
  const [ratings, setRatings] = useState<Record<string, CriterionRating>>(() => {
    const seed: Record<string, CriterionRating> = {}
    for (const criterion of scoringModel.criteria ?? []) {
      const previous = currentScore?.ratings?.find(
        (r) => r.criterionId === criterion.id,
      )
      seed[criterion.id] = {
        value: previous?.ratingValue ?? null,
        ratingLevelId: previous?.ratingLevelId ?? null,
      }
    }
    return seed
  })

  const [preview, setPreview] = useState<ScoringModelEvaluationDto | null>(null)
  const [previewError, setPreviewError] = useState<string | null>(null)
  const [isPreviewing, setIsPreviewing] = useState(false)

  const resetPreview = () => {
    setPreview(null)
    setPreviewError(null)
  }

  const setScaleRating = (
    criterionId: string,
    levelId: string | null,
    value: number | null,
  ) => {
    setRatings((prev) => ({
      ...prev,
      [criterionId]: { value, ratingLevelId: levelId },
    }))
    resetPreview()
  }

  const setNumericRating = (criterionId: string, value: number | null) => {
    setRatings((prev) => ({
      ...prev,
      [criterionId]: { value, ratingLevelId: null },
    }))
    resetPreview()
  }

  const allRated = sortedCriteria.every(
    (c) => ratings[c.id]?.value !== undefined && ratings[c.id]?.value !== null,
  )

  const handlePreview = async () => {
    if (!allRated) return
    setIsPreviewing(true)
    try {
      const response = await evaluate({
        scoringModelId: scoringModel.id,
        criterionValues: sortedCriteria.map((c) => ({
          criterionId: c.id,
          value: ratings[c.id].value as number,
        })),
      })
      if (response.error) throw response.error
      setPreview(response.data ?? null)
      setPreviewError(null)
    } catch (error) {
      const apiError: ApiError = isApiError(error) ? error : {}
      setPreview(null)
      setPreviewError(
        apiError.detail ??
          'Unable to preview the score. Check the entered values and try again.',
      )
    } finally {
      setIsPreviewing(false)
    }
  }

  const handleSave = async () => {
    try {
      const response = await recordScore({
        projectId,
        request: {
          ratings: sortedCriteria.map((c) => ({
            criterionId: c.id,
            value: ratings[c.id].value ?? undefined,
            ratingLevelId: ratings[c.id].ratingLevelId ?? undefined,
          })),
        },
      })
      if (response.error) throw response.error
      messageApi.success('Score recorded successfully.')
      onFormComplete()
    } catch (error) {
      const apiError: ApiError = isApiError(error) ? error : {}
      messageApi.error(
        apiError.detail ?? 'An error occurred while recording the score.',
      )
    }
  }

  return (
    <Modal
      title={`Score ${scoringModel.name}`}
      open
      okText="Save score"
      okButtonProps={{ disabled: !allRated, loading: isSaving }}
      onOk={handleSave}
      onCancel={onFormCancel}
      width={560}
    >
      <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
        {modelArchived && (
          <Alert
            type="warning"
            showIcon
            title="This scoring model has been archived."
            description="You can still record a score with it, but consider asking an admin to assign a current model to the portfolio."
          />
        )}

        <Form layout="vertical" size="small">
          {sortedCriteria.map((criterion) => {
            const scale = criterion.scaleId
              ? scalesById.get(criterion.scaleId)
              : undefined
            const sortedLevels = scale
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
                    : 'Free numeric entry — no scale assigned.')
                }
                style={{ marginBottom: token.marginSM }}
              >
                {scale ? (
                  <Select
                    style={{ width: '100%' }}
                    value={rating?.ratingLevelId ?? undefined}
                    onChange={(levelId) => {
                      const level = sortedLevels.find((l) => l.id === levelId)
                      setScaleRating(
                        criterion.id,
                        levelId ?? null,
                        level?.value ?? null,
                      )
                    }}
                    placeholder="Select a rating"
                    options={sortedLevels.map((l) => ({
                      value: l.id,
                      label:
                        l.label === l.value.toString()
                          ? l.label
                          : `${l.label} (${l.value})`,
                    }))}
                  />
                ) : (
                  <InputNumber
                    style={{ width: '100%' }}
                    value={rating?.value ?? null}
                    onChange={(v) => setNumericRating(criterion.id, v)}
                    placeholder="Enter a numeric value"
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
                  .map((output) => {
                    const formula = formulasByToken.get(output.token)
                    return (
                      <Flex key={output.token} vertical gap={0}>
                        <Statistic
                          title={
                            <Space size="small">
                              {output.name}
                              {output.isPrimary && (
                                <Tag color={token.colorPrimary}>Score</Tag>
                              )}
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
                        {formula && (
                          <Text
                            type="secondary"
                            style={{ fontSize: 12, fontFamily: 'monospace' }}
                          >
                            {output.token} = {formula}
                          </Text>
                        )}
                      </Flex>
                    )
                  })}
              </Space>
            ) : (
              !previewError && (
                <Text type="secondary">
                  Preview the outputs before saving.
                </Text>
              )
            )}
          </Flex>
        </Card>
      </Space>
    </Modal>
  )
}

export default RecordProjectScoreForm
