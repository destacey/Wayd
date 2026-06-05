'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import {
  ScoringModelDetailsDto,
  ScoringModelEvaluationDto,
} from '@/src/services/wayd-api'
import { useEvaluateScoringModelMutation } from '@/src/store/features/scoring/scoring-models-api'
import { isApiError, type ApiError } from '@/src/utils'
import {
  Alert,
  Button,
  Card,
  Empty,
  Form,
  InputNumber,
  Select,
  Space,
  Statistic,
  Tag,
  Typography,
  theme,
} from 'antd'
import { useMemo, useState } from 'react'

const { Text } = Typography

export interface ScoringModelTestPanelProps {
  scoringModel: ScoringModelDetailsDto
}

type CriterionValues = Record<string, number | null>

const ScoringModelTestPanel = ({ scoringModel }: ScoringModelTestPanelProps) => {
  const messageApi = useMessage()
  const { token } = theme.useToken()

  const [evaluate, { isLoading }] = useEvaluateScoringModelMutation()

  const sortedCriteria = useMemo(
    () => [...(scoringModel.criteria ?? [])].sort((a, b) => a.order - b.order),
    [scoringModel.criteria],
  )

  const scalesById = useMemo(
    () => new Map((scoringModel.scales ?? []).map((s) => [s.id, s])),
    [scoringModel.scales],
  )

  const [values, setValues] = useState<CriterionValues>({})
  const [result, setResult] = useState<ScoringModelEvaluationDto | null>(null)

  const setValue = (criterionId: string, value: number | null) => {
    setValues((prev) => ({ ...prev, [criterionId]: value }))
    // A changed input invalidates the previously shown result.
    setResult(null)
  }

  const allRated = sortedCriteria.every(
    (c) => values[c.id] !== undefined && values[c.id] !== null,
  )

  const handleCalculate = async () => {
    try {
      const response = await evaluate({
        scoringModelId: scoringModel.id,
        criterionValues: sortedCriteria.map((c) => ({
          criterionId: c.id,
          value: values[c.id] as number,
        })),
      })
      if (response.error) {
        throw response.error
      }
      setResult(response.data ?? null)
    } catch (error) {
      const apiError: ApiError = isApiError(error) ? error : {}
      setResult(null)
      messageApi.error(
        apiError.detail ??
          'An error occurred while evaluating the scoring model. Please try again.',
      )
    }
  }

  if (sortedCriteria.length === 0) {
    return (
      <Empty description="Add criteria and outputs to test this scoring model." />
    )
  }

  return (
    <Space orientation="vertical" size="large" style={{ width: '100%' }}>
      <Alert
        type="info"
        title="Plug in a value for each criterion to preview the output formulas. Nothing is saved."
      />

      <Card size="small" title="Criterion values">
        <Form layout="vertical" size="small">
          {sortedCriteria.map((criterion) => {
            const scale = criterion.scaleId
              ? scalesById.get(criterion.scaleId)
              : undefined
            const sortedLevels = scale
              ? [...scale.levels].sort((a, b) => a.order - b.order)
              : []
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
                  scale
                    ? `Rated on the "${scale.name}" scale.`
                    : 'Free numeric entry — no scale assigned.'
                }
                style={{ marginBottom: token.marginSM }}
              >
                {scale ? (
                  <Select
                    style={{ width: '100%' }}
                    value={values[criterion.id] ?? undefined}
                    onChange={(v) => setValue(criterion.id, v ?? null)}
                    placeholder="Select a rating"
                    options={sortedLevels.map((l) => ({
                      value: l.value,
                      label:
                        l.label === l.value.toString()
                          ? l.label
                          : `${l.label} (${l.value})`,
                    }))}
                  />
                ) : (
                  <InputNumber
                    style={{ width: '100%' }}
                    value={values[criterion.id] ?? null}
                    onChange={(v) => setValue(criterion.id, v)}
                    placeholder="Enter a numeric value"
                  />
                )}
              </Form.Item>
            )
          })}
          <Button
            type="primary"
            size="small"
            loading={isLoading}
            disabled={!allRated}
            onClick={handleCalculate}
          >
            Calculate
          </Button>
        </Form>
      </Card>

      {result && (
        <Card size="small" title="Results">
          <Space size="large" wrap>
            {[...result.outputs]
              .sort((a, b) => a.order - b.order)
              .map((output) => (
                <Statistic
                  key={output.token}
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
              ))}
          </Space>
        </Card>
      )}
    </Space>
  )
}

export default ScoringModelTestPanel
