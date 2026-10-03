'use client'

import { Descriptions, Modal, Typography } from 'antd'
import { useMessage } from '@/src/components/contexts/messaging'
import { useConfirmModal } from '@/src/hooks'
import {
  CalendarDate,
  compareCalendarDates,
  formatCalendarDate,
  isApiError,
} from '@/src/utils'

const { Item } = Descriptions
const { Paragraph } = Typography

export interface OperatingModelSummary {
  start: CalendarDate
  timeZone: string
}

export interface DeleteOperatingModelFormProps {
  operatingModel: OperatingModelSummary
  /** The model that becomes current again once this one is deleted. */
  reinstatedModel: OperatingModelSummary | undefined
  deleteOperatingModel: () => Promise<unknown>
  onFormComplete: () => void
  onFormCancel: () => void
}

/** The latest model before the current one: the one deleting it reinstates. */
export const findReinstatedModel = <
  T extends OperatingModelSummary & { isCurrent: boolean },
>(
  models: T[] | undefined,
): T | undefined =>
  models
    ?.filter((m) => !m.isCurrent)
    .reduce<T | undefined>(
      (latest, m) =>
        !latest || compareCalendarDates(m.start, latest.start) > 0 ? m : latest,
      undefined,
    )

const formatDate = (date: CalendarDate) => formatCalendarDate(date, 'M/D/YYYY')

const DeleteOperatingModelForm = ({
  operatingModel,
  reinstatedModel,
  deleteOperatingModel,
  onFormComplete,
  onFormCancel,
}: DeleteOperatingModelFormProps) => {
  const messageApi = useMessage()

  const { isOpen, isSaving, handleOk, handleCancel } = useConfirmModal({
    onSubmit: async () => {
      try {
        await deleteOperatingModel()
        messageApi.success('Successfully deleted operating model.')
        return true
      } catch (error) {
        messageApi.error(
          (isApiError(error) ? error.detail : undefined) ??
            'An unexpected error occurred while deleting the operating model.',
        )
        console.error(error)
        return false
      }
    },
    onComplete: onFormComplete,
    onCancel: onFormCancel,
    errorMessage:
      'An unexpected error occurred while deleting the operating model.',
    permission: 'Permissions.Teams.Update',
  })

  return (
    <Modal
      title="Are you sure you want to delete the current operating model?"
      open={isOpen}
      onOk={handleOk}
      okText="Delete"
      okType="danger"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false} // disable esc key to close modal
      destroyOnHidden
    >
      <Descriptions size="small" column={1}>
        <Item label="Start">{formatDate(operatingModel.start)}</Item>
        <Item label="Time Zone">{operatingModel.timeZone}</Item>
      </Descriptions>
      {reinstatedModel && (
        <Paragraph type="secondary" style={{ marginTop: 12, marginBottom: 0 }}>
          The model from {formatDate(reinstatedModel.start)} (
          {reinstatedModel.timeZone}) becomes current again and applies from
          that date onward.
        </Paragraph>
      )}
    </Modal>
  )
}

export default DeleteOperatingModelForm
