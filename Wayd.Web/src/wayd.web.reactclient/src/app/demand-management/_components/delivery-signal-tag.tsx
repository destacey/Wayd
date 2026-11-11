import { DeliverySignal, StatusCategory } from '@/src/services/wayd-api'
import { Tag } from 'antd'
import { FC } from 'react'

/** How each delivery signal reads wherever a request's delivery is shown. */
export const deliverySignalLabel: Record<DeliverySignal, string> = {
  [DeliverySignal.NotStarted]: 'Not Started',
  [DeliverySignal.InProgress]: 'In Progress',
  [DeliverySignal.Blocked]: 'Blocked',
  [DeliverySignal.Done]: 'Done',
  [DeliverySignal.Cancelled]: 'Cancelled',
}

const deliverySignalColor: Record<DeliverySignal, string> = {
  [DeliverySignal.NotStarted]: 'default',
  [DeliverySignal.InProgress]: 'processing',
  [DeliverySignal.Blocked]: 'error',
  [DeliverySignal.Done]: 'success',
  [DeliverySignal.Cancelled]: 'default',
}

/**
 * The delivery state a request shows, or none for a request that was declined, withdrawn or marked a
 * duplicate: its outcomes' work may go on, but it no longer reports on the request.
 */
export const shownDeliveryState = (
  statusCategory: StatusCategory,
  deliveryState: DeliverySignal | undefined,
): DeliverySignal | undefined =>
  statusCategory === StatusCategory.Removed ? undefined : deliveryState

export interface DeliverySignalTagProps {
  signal: DeliverySignal
}

/** An outcome's delivery signal, or a request's delivery state rolled up from them. */
const DeliverySignalTag: FC<DeliverySignalTagProps> = ({ signal }) => (
  <Tag color={deliverySignalColor[signal]}>{deliverySignalLabel[signal]}</Tag>
)

export default DeliverySignalTag
