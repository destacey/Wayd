'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { StatusWorkflowDetailsDto } from '@/src/services/wayd-api'
import {
  useGetWorkflowOwnerTypesQuery,
  usePublishStatusWorkflowMutation,
} from '@/src/store/features/common/status-workflows-api'
import { statusCategoryLabel } from '@/src/components/common/status-workflows'
import { Alert, Flex, Modal, Typography } from 'antd'
import { useConfirmModal } from '@/src/hooks'
import { isApiError, type ApiError } from '@/src/utils'

const { Text } = Typography

export interface PublishStatusWorkflowFormProps {
  statusWorkflow: StatusWorkflowDetailsDto
  onFormComplete: () => void
  onFormCancel: () => void
}

/**
 * The server refuses a publish that is missing a required category or alias,
 * files an alias under the wrong category, or whose restricted transitions
 * would strand a status, so each problem is named here and OK stays disabled
 * rather than surfacing it as an error toast.
 */
const PublishStatusWorkflowForm = ({
  statusWorkflow,
  onFormComplete,
  onFormCancel,
}: PublishStatusWorkflowFormProps) => {
  const messageApi = useMessage()

  const [publishStatusWorkflow] = usePublishStatusWorkflowMutation()

  const missingAliases = statusWorkflow.missingRequiredAliases ?? []
  const hasMissingAliases = missingAliases.length > 0
  const transitionIssues = statusWorkflow.transitionIssues ?? []
  const hasTransitionIssues = transitionIssues.length > 0
  const missingCategories = statusWorkflow.missingRequiredCategories ?? []
  const { data: ownerTypes } = useGetWorkflowOwnerTypesQuery()
  const categoryLabels = ownerTypes?.find(
    (o) => o.key === statusWorkflow.owner?.key,
  )?.categoryLabels
  const hasMissingCategories = missingCategories.length > 0
  const misfiledAliases = statusWorkflow.misfiledAliases ?? []
  const hasMisfiledAliases = misfiledAliases.length > 0
  const isBlocked =
    hasMissingAliases ||
    hasTransitionIssues ||
    hasMissingCategories ||
    hasMisfiledAliases

  const { isOpen, isSaving, handleOk, handleCancel } = useConfirmModal({
    onSubmit: async () => {
      try {
        const response = await publishStatusWorkflow(statusWorkflow.id)
        if (response?.error) {
          throw response.error
        }
        messageApi.success('Successfully published status workflow.')
        return true
      } catch (error) {
        const apiError: ApiError = isApiError(error) ? error : {}
        messageApi.error(
          apiError.detail ??
            'An unexpected error occurred while publishing the status workflow.',
        )
        console.error(error)
        return false
      }
    },
    onComplete: onFormComplete,
    onCancel: onFormCancel,
    errorMessage:
      'An unexpected error occurred while publishing the status workflow.',
    permission: 'Permissions.StatusWorkflows.Update',
  })

  return (
    <Modal
      title="Publish this Status Workflow?"
      open={isOpen}
      onOk={handleOk}
      okText="Publish"
      okButtonProps={{ disabled: isBlocked }}
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Flex vertical gap={12}>
        <Text>
          {statusWorkflow.key} - {statusWorkflow.name}
        </Text>
        {hasMissingCategories && (
          <Alert
            type="error"
            showIcon
            title="Required categories are not covered"
            description={
              <>
                <div>Add a status in each of these categories:</div>
                <ul style={{ marginTop: 8, marginBottom: 0 }}>
                  {missingCategories.map((category) => (
                    <li key={category}>
                      {statusCategoryLabel(categoryLabels, category)}
                    </li>
                  ))}
                </ul>
              </>
            }
          />
        )}
        {hasMisfiledAliases && (
          <Alert
            type="error"
            showIcon
            title="Some meanings need fixing"
            description={
              <ul style={{ marginTop: 0, marginBottom: 0 }}>
                {misfiledAliases.map((issue) => (
                  <li key={issue}>{issue}</li>
                ))}
              </ul>
            }
          />
        )}
        {hasTransitionIssues && (
          <Alert
            type="error"
            showIcon
            title="Transitions would strand a status"
            description={
              <ul style={{ marginTop: 0, marginBottom: 0 }}>
                {transitionIssues.map((issue) => (
                  <li key={issue}>{issue}</li>
                ))}
              </ul>
            }
          />
        )}
        {hasMissingAliases ? (
          <Alert
            type="error"
            showIcon
            title="Required meanings are not covered"
            description={
              <>
                <div>Assign a status to each of these before publishing:</div>
                <ul style={{ marginTop: 8, marginBottom: 0 }}>
                  {missingAliases.map((alias) => (
                    <li key={alias}>{alias}</li>
                  ))}
                </ul>
              </>
            }
          />
        ) : isBlocked ? null : (
          <Text type="secondary">
            Publishing locks the workflow&apos;s statuses so records can be
            assigned to it. To change it afterwards, clone it into a new draft.
          </Text>
        )}
      </Flex>
    </Modal>
  )
}

export default PublishStatusWorkflowForm
