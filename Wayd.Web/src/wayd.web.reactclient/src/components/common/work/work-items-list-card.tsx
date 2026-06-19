import { WorkItemListDto } from '@/src/services/wayd-api'
import WaydEmpty from '../wayd-empty'
import WaydList from '../wayd-list'
import WorkItemListItem from './work-item-list-item'

export interface WorkItemsListCardProps {
  workItems: WorkItemListDto[]
  isLoading: boolean
}

const WorkItemsListCard = ({
  workItems,
  isLoading,
}: WorkItemsListCardProps) => {
  return (
    <WaydList
      size="small"
      loading={isLoading}
      dataSource={workItems}
      locale={{ emptyText: <WaydEmpty message="No work items" /> }}
      renderItem={(workItem) => <WorkItemListItem workItem={workItem} />}
    />
  )
}

export default WorkItemsListCard
