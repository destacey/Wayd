'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { ScoringModelState } from '@/src/services/wayd-api'
import { useGetScoringModelsQuery } from '@/src/store/features/scoring/scoring-models-api'
import {
  useAssignPortfolioScoringModelMutation,
  useClearPortfolioScoringModelMutation,
} from '@/src/store/features/ppm/portfolios-api'
import { isApiError, type ApiError } from '@/src/utils'
import { useModalForm } from '@/src/hooks'
import { Form, Modal, Select, Typography } from 'antd'
import { useMemo } from 'react'

const { Item } = Form
const { Text } = Typography

export interface SetPortfolioScoringModelFormProps {
  portfolioId: string
  portfolioKey: number
  scoringModelId?: string
  onFormComplete: () => void
  onFormCancel: () => void
}

interface SetPortfolioScoringModelFormValues {
  scoringModelId?: string
}

const SetPortfolioScoringModelForm = ({
  portfolioId,
  portfolioKey,
  scoringModelId,
  onFormComplete,
  onFormCancel,
}: SetPortfolioScoringModelFormProps) => {
  const messageApi = useMessage()

  const { data: models = [], isLoading: isLoadingModels } =
    useGetScoringModelsQuery(ScoringModelState.Active)
  const [assignModel] = useAssignPortfolioScoringModelMutation()
  const [clearModel] = useClearPortfolioScoringModelMutation()

  const options = useMemo(
    () =>
      [...models]
        .sort((a, b) => a.name.localeCompare(b.name))
        .map((m) => ({
          value: m.id,
          label: m.name,
          description: m.description,
        })),
    [models],
  )

  const { form, isOpen, isSaving, handleOk, handleCancel } =
    useModalForm<SetPortfolioScoringModelFormValues>({
      onSubmit: async (values) => {
        try {
          const newModelId = values.scoringModelId ?? undefined
          const response = newModelId
            ? await assignModel({
                id: portfolioId,
                scoringModelId: newModelId,
                cacheKey: portfolioKey,
              })
            : await clearModel({ id: portfolioId, cacheKey: portfolioKey })
          if (response.error) throw response.error

          messageApi.success(
            newModelId
              ? 'Scoring model assigned. Projects in this portfolio can now be scored.'
              : 'Scoring disabled for this portfolio.',
          )
          return true
        } catch (error) {
          const apiError: ApiError = isApiError(error) ? error : {}
          messageApi.error(
            apiError.detail ??
              'An error occurred while updating the scoring model.',
          )
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      permission: 'Permissions.ProjectPortfolios.Update',
    })

  return (
    <Modal
      title="Set Scoring Model"
      open={isOpen}
      okText="Save"
      confirmLoading={isSaving}
      onOk={handleOk}
      onCancel={handleCancel}
      destroyOnHidden
    >
      <Text type="secondary">
        Assign a scoring model to enable priority scoring for this portfolio&apos;s
        projects. Clear the selection to disable scoring. Existing project scores
        are unaffected.
      </Text>
      <Form
        form={form}
        layout="vertical"
        initialValues={{ scoringModelId: scoringModelId ?? undefined }}
        style={{ marginTop: 16 }}
      >
        <Item name="scoringModelId" label="Scoring Model">
          <Select
            loading={isLoadingModels}
            allowClear
            placeholder="No scoring model (scoring disabled)"
            options={options}
            optionRender={(option) => (
              <div style={{ lineHeight: 1.3, padding: '2px 0' }}>
                <div>{option.data.label}</div>
                {option.data.description && (
                  <div
                    style={{
                      fontSize: 12,
                      color: 'var(--ant-color-text-tertiary)',
                      whiteSpace: 'normal',
                    }}
                  >
                    {option.data.description}
                  </div>
                )}
              </div>
            )}
          />
        </Item>
      </Form>
    </Modal>
  )
}

export default SetPortfolioScoringModelForm
