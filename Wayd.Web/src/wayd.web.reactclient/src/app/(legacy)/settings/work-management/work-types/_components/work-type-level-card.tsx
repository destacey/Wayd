import { WorkTypeLevelDto } from '@/src/services/wayd-api'
import { Button, Card } from 'antd'
import WaydList from '@/src/components/common/wayd-list'
import { EditWorkTypeLevelForm } from '.'
import { useState } from 'react'
import { EditOutlined, HolderOutlined } from '@ant-design/icons'
import { useSortable } from '@dnd-kit/sortable'
import { CSS } from '@dnd-kit/utilities'
import { MarkdownRenderer } from '@/src/components/common/markdown'

const { Item } = WaydList
const { Meta } = Item

interface WorkTypeLevelCardProps {
  level: WorkTypeLevelDto
  canUpdateWorkTypeLevels: boolean
  canOrder: boolean
  refreshLevels: () => void
}

const WorkTypeLevelCard = (props: WorkTypeLevelCardProps) => {
  const [openEditWorkTypeLevelForm, setOpenEditWorkTypeLevelForm] =
    useState<boolean>(false)

  const { attributes, listeners, setNodeRef, transform, transition } =
    useSortable({ id: props.level.id })

  const onEditObjectiveFormClosed = (wasSaved: boolean) => {
    setOpenEditWorkTypeLevelForm(false)
    if (wasSaved) {
      props.refreshLevels()
    }
  }

  const sortableStyle = {
    transition: transition,
    transform: CSS.Transform.toString(transform),
    touchAction: 'none',
    marginBottom: 4,
  }

  return (
    <>
      <Card
        size="small"
        ref={setNodeRef}
        {...attributes}
        style={sortableStyle}
        styles={{ body: { padding: 0 } }}
        hoverable
      >
        <Item
          key={props.level.id}
          actions={[
            props.canUpdateWorkTypeLevels && (
              <Button
                type="text"
                title="Edit work type level"
                icon={<EditOutlined />}
                onClick={() => setOpenEditWorkTypeLevelForm(true)}
              />
            ),
          ]}
        >
          {props.canOrder && props.canUpdateWorkTypeLevels && (
            // TODO: add a visual indicator that the item is draggable for the whole row
            <HolderOutlined
              {...listeners}
              rotate={90}
              style={{ marginRight: 12 }}
            />
          )}
          <Meta
            title={props.level.name}
            description={
              <MarkdownRenderer markdown={props.level.description} />
            }
          />
        </Item>
      </Card>
      {props.canUpdateWorkTypeLevels && (
        <EditWorkTypeLevelForm
          levelId={props.level.id}
          onFormSave={() => onEditObjectiveFormClosed(true)}
          onFormCancel={() => onEditObjectiveFormClosed(false)}
        />
      )}
    </>
  )
}

export default WorkTypeLevelCard
