'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useConfirmModal } from '@/src/hooks'
import { RoadmapDetailsDto } from '@/src/services/wayd-api'
import { useUpdateRoadmapColorsMutation } from '@/src/store/features/planning/roadmaps-api'
import { isApiError, type ApiError } from '@/src/utils'
import { Modal } from 'antd'
import { useState } from 'react'
import RoadmapColorConfig, {
  RoadmapColorConfigEntry,
  RoadmapColorConfigErrors,
  validateColorEntries,
} from './roadmap-color-config'

export interface ConfigureRoadmapColorsFormProps {
  roadmap: RoadmapDetailsDto
  onFormComplete: () => void
  onFormCancel: () => void
}

const toConfigEntries = (
  roadmap: RoadmapDetailsDto,
): RoadmapColorConfigEntry[] =>
  [...roadmap.colors]
    .sort((a, b) => a.order - b.order)
    .map((c) => ({
      key: crypto.randomUUID(),
      color: c.color,
      name: c.name,
      isDefault: c.isDefault,
    }))

const ConfigureRoadmapColorsForm = ({
  roadmap,
  onFormComplete,
  onFormCancel,
}: ConfigureRoadmapColorsFormProps) => {
  const messageApi = useMessage()
  const [entries, setEntries] = useState<RoadmapColorConfigEntry[]>(() =>
    toConfigEntries(roadmap),
  )

  const errors: RoadmapColorConfigErrors = validateColorEntries(entries)
  const isValid = !errors.hasErrors

  const [updateRoadmapColors] = useUpdateRoadmapColorsMutation()

  const { isOpen, isSaving, handleOk, handleCancel } = useConfirmModal({
    onSubmit: async () => {
      if (!isValid) return false

      try {
        const response = await updateRoadmapColors({
          roadmapId: roadmap.id,
          cacheKey: roadmap.key,
          request: {
            roadmapId: roadmap.id,
            colors: entries.map((e, index) => ({
              color: e.color!,
              name: e.name.trim(),
              order: index + 1,
              isDefault: e.isDefault,
            })),
          },
        })

        if (response?.error) throw response.error

        messageApi.success('Roadmap colors updated successfully.')
        return true
      } catch (error) {
        const apiError: ApiError = isApiError(error) ? error : {}
        messageApi.error(
          apiError.detail ??
            'An error occurred while updating the roadmap colors. Please try again.',
        )
        console.error(error)
        return false
      }
    },
    onComplete: onFormComplete,
    onCancel: onFormCancel,
    errorMessage:
      'An error occurred while updating the roadmap colors. Please try again.',
    permission: 'Permissions.Roadmaps.Update',
  })

  return (
    <Modal
      title="Configure Colors"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Save"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <RoadmapColorConfig
        value={entries}
        onChange={setEntries}
        errors={errors}
      />
    </Modal>
  )
}

export default ConfigureRoadmapColorsForm

