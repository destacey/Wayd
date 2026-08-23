'use client'

import { EstimationScaleDto } from '@/src/services/wayd-api'
import { Descriptions, Space, Tag } from 'antd'

const { Item } = Descriptions

interface EstimationScaleDetailsProps {
  estimationScale: EstimationScaleDto
}

const EstimationScaleDetails = ({
  estimationScale,
}: EstimationScaleDetailsProps) => {
  if (!estimationScale) return null

  return (
    <Space vertical style={{ width: '100%' }}>
      <Descriptions size="small" column={1}>
        <Item label="Name">{estimationScale.name}</Item>
        <Item label="Description">{estimationScale.description}</Item>
        <Item label="Active">{estimationScale.isActive ? 'Yes' : 'No'}</Item>
        <Item label="Values">
          <Space size={[4, 4]} wrap>
            {estimationScale.values.map((v, i) => (
              <Tag key={i}>{v}</Tag>
            ))}
          </Space>
        </Item>
      </Descriptions>
    </Space>
  )
}

export default EstimationScaleDetails
