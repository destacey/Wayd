'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import {
  SprintDetailsDto,
  SprintType,
  SprintTypeSource,
} from '@/src/services/wayd-api'
import {
  useGetSprintTypesQuery,
  useSetSprintTypeMutation,
} from '@/src/store/features/work-management/sprints-api'
import { isApiError, type ApiError } from '@/src/utils'
import { Flex, Modal, Radio, Typography } from 'antd'
import { useState } from 'react'

const { Text } = Typography

// The option that clears the team's type, so the sprint follows its mapping.
const FOLLOW_MAPPING = 'follow-mapping'

type SprintTypeChoice = SprintType | typeof FOLLOW_MAPPING

export interface SprintTypeFormProps {
  sprint: SprintDetailsDto
  onFormComplete: () => void
  onFormCancel: () => void
}

/**
 * Sets the sprint's type for the team, or clears it so the sprint follows the
 * category of the planning interval iteration it is mapped to.
 */
const SprintTypeForm = ({
  sprint,
  onFormComplete,
  onFormCancel,
}: SprintTypeFormProps) => {
  const messageApi = useMessage()
  const { data: sprintTypes = [] } = useGetSprintTypesQuery()
  const [setSprintType, { isLoading }] = useSetSprintTypeMutation()
  const [choice, setChoice] = useState<SprintTypeChoice>(
    sprint.sprintTypeSource === SprintTypeSource.Team
      ? sprint.sprintType
      : FOLLOW_MAPPING,
  )

  const handleOk = async () => {
    try {
      const response = await setSprintType({
        id: sprint.id,
        key: sprint.key,
        sprintType: choice === FOLLOW_MAPPING ? undefined : choice,
      })
      if (response.error) throw response.error
      messageApi.success('Sprint type saved.')
      onFormComplete()
    } catch (error) {
      const apiError: ApiError = isApiError(error) ? error : {}
      messageApi.error(
        apiError.detail ?? 'An error occurred while saving the sprint type.',
      )
    }
  }

  return (
    <Modal
      title="Sprint Type"
      open={true}
      onOk={handleOk}
      okText="Save"
      confirmLoading={isLoading}
      onCancel={onFormCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Flex vertical gap={12}>
        <Text type="secondary">
          A non-standard sprint, such as an innovation and planning sprint, a
          hackathon or a holiday period, keeps its own metrics but is left out
          of velocity and other rollups across the team&apos;s sprints.
        </Text>
        <Radio.Group
          value={choice}
          onChange={(e) => setChoice(e.target.value)}
          aria-label="Sprint type"
        >
          <Flex vertical gap={8}>
            <Radio value={FOLLOW_MAPPING}>
              <Flex vertical>
                <Text>Follow the planning interval</Text>
                <Text type="secondary">
                  From the category of the planning interval iteration the
                  sprint is mapped to: IP iterations are non-standard. Standard
                  when it isn&apos;t mapped.
                </Text>
              </Flex>
            </Radio>
            {sprintTypes.map((type) => (
              <Radio key={type.code} value={type.code}>
                <Flex vertical>
                  <Text>{type.name}</Text>
                  {type.description && (
                    <Text type="secondary">{type.description}</Text>
                  )}
                </Flex>
              </Radio>
            ))}
          </Flex>
        </Radio.Group>
      </Flex>
    </Modal>
  )
}

export default SprintTypeForm
