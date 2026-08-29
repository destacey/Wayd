'use client'

import { useState } from 'react'
import dayjs from 'dayjs'
import { Flex, Modal, Select, Spin, Typography } from 'antd'
import {
  MapPlanningIntervalSprintsRequest,
  SprintListDto,
} from '@/src/services/wayd-api'
import {
  useGetIterationSprintsQuery,
  useMapTeamSprintsMutation,
} from '@/src/store/features/planning/planning-interval-api'
import { useGetTeamSprintsQuery } from '@/src/store/features/organizations/team-api'
import { useMessage } from '@/src/components/contexts/messaging'
import { useConfirmModal } from '@/src/hooks'

const { Text } = Typography

export interface ConfigureTeamSprintMappingsFormProps {
  planningIntervalId: string
  planningIntervalKey: number
  teamId: string
  teamName: string
  teamOfTeamsName: string | null
  onFormSave: () => void
  onFormCancel: () => void
}

interface IterationSprintMapping {
  iterationId: string
  iterationName: string
  iterationStart: Date
  iterationEnd: Date
  iterationCategory: string
  sprintId: string | null
}

const formatDateRange = (start: Date, end: Date): string => {
  return `${dayjs(start).format('MMM D, YYYY')} - ${dayjs(end).format('MMM D, YYYY')}`
}

const formatSprintOption = (sprint: SprintListDto): string => {
  return `${sprint.name} (${formatDateRange(sprint.start, sprint.end)})`
}

const ConfigureTeamSprintMappingsForm = ({
  planningIntervalId,
  planningIntervalKey,
  teamId,
  teamName,
  teamOfTeamsName,
  onFormSave,
  onFormCancel,
}: ConfigureTeamSprintMappingsFormProps) => {
  // Track user overrides separately so mappings can be derived from state
  const [sprintOverrides, setSprintOverrides] = useState<
    Record<string, string | null>
  >({})
  const messageApi = useMessage()

  const { data: iterationSprintsData, isLoading: iterationsLoading } =
    useGetIterationSprintsQuery({
      idOrKey: planningIntervalKey.toString(),
    })

  const { data: teamSprintsData, isLoading: sprintsLoading } =
    useGetTeamSprintsQuery(teamId)

  const [mapTeamSprints] = useMapTeamSprintsMutation()

  const isLoading = iterationsLoading || sprintsLoading

  const sprintOptions = !teamSprintsData
    ? []
    : [...teamSprintsData]
        .sort((a, b) => new Date(b.start).getTime() - new Date(a.start).getTime())
        .map((sprint) => ({
          value: sprint.id,
          label: formatSprintOption(sprint),
        }))

  // Derive mappings from query data + user overrides
  const mappings: IterationSprintMapping[] = !iterationSprintsData
    ? []
    : [...iterationSprintsData]
        .sort(
          (a, b) =>
            new Date(a.start).getTime() - new Date(b.start).getTime(),
        )
        .map((iteration) => {
        const existingSprint = iteration.sprints?.find(
          (s) => s.team.id === teamId,
        )
        const defaultSprintId = existingSprint?.id ?? null
        const sprintId =
          iteration.id in sprintOverrides
            ? sprintOverrides[iteration.id]
            : defaultSprintId

        return {
          iterationId: iteration.id,
          iterationName: iteration.name,
          iterationStart: iteration.start,
          iterationEnd: iteration.end,
          iterationCategory: iteration.category?.name ?? '',
          sprintId,
        }
      })

  const { isOpen, isSaving, handleOk, handleCancel } = useConfirmModal({
    onSubmit: async () => {
      try {
        const iterationSprintMappings: { [key: string]: string } = {}
        mappings.forEach((m) => {
          if (m.sprintId) {
            iterationSprintMappings[m.iterationId] = m.sprintId
          }
        })

        const request: MapPlanningIntervalSprintsRequest = {
          id: planningIntervalId,
          teamId: teamId,
          iterationSprintMappings,
        }

        await mapTeamSprints({
          planningIntervalId,
          teamId,
          request,
          cacheKey: planningIntervalKey,
        }).unwrap()

        messageApi.success(
          `Successfully updated sprint mappings for ${teamName}.`,
        )
        return true
      } catch (error) {
        console.error('Error saving sprint mappings:', error)
        messageApi.error(
          'An error occurred while saving sprint mappings. Please try again.',
        )
        return false
      }
    },
    onComplete: onFormSave,
    onCancel: onFormCancel,
    errorMessage:
      'An error occurred while saving sprint mappings. Please try again.',
  })

  const handleSprintChange = (iterationId: string, sprintId: string | null) => {
    setSprintOverrides((prev) => ({ ...prev, [iterationId]: sprintId }))
  }

  return (
    <Modal
      title={`Configure Sprint Mapping - ${teamName}`}
      open={isOpen}
      width={700}
      onOk={handleOk}
      okText="Save Changes"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Spin spinning={isLoading} size="large">
        {teamOfTeamsName && (
          <Text type="secondary" style={{ display: 'block', marginBottom: 16 }}>
            Team of Teams: {teamOfTeamsName}
          </Text>
        )}
        <Flex vertical gap="middle">
          <Flex justify="space-between" align="center" gap="middle">
            <Text strong style={{ minWidth: 180 }}>
              PI Iteration
            </Text>
            <Text strong style={{ flex: 1 }}>
              Team Sprint
            </Text>
          </Flex>
          {mappings.map((mapping) => (
            <Flex
              key={mapping.iterationId}
              justify="space-between"
              align="center"
              gap="middle"
            >
              <div style={{ minWidth: 180 }}>
                <div>
                  <Text strong>
                    {mapping.iterationName} - {mapping.iterationCategory}
                  </Text>
                </div>
                <Text type="secondary" style={{ fontSize: '12px' }}>
                  {formatDateRange(
                    mapping.iterationStart,
                    mapping.iterationEnd,
                  )}
                </Text>
              </div>
              <Select
                style={{ flex: 1 }}
                placeholder="Select sprint..."
                allowClear
                value={mapping.sprintId}
                onChange={(value) =>
                  handleSprintChange(mapping.iterationId, value ?? null)
                }
                options={sprintOptions}
                showSearch
                filterOption={(input, option) =>
                  (option?.label ?? '')
                    .toLowerCase()
                    .includes(input.toLowerCase())
                }
              />
            </Flex>
          ))}
        </Flex>
      </Spin>
    </Modal>
  )
}

export default ConfigureTeamSprintMappingsForm
