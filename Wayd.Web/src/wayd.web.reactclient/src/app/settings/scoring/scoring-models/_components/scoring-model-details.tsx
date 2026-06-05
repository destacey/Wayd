'use client'

import { ScoringModelDetailsDto } from '@/src/services/wayd-api'
import { Descriptions, Space } from 'antd'

const { Item } = Descriptions

interface ScoringModelDetailsProps {
  scoringModel: ScoringModelDetailsDto | undefined
}

const ScoringModelDetails: React.FC<ScoringModelDetailsProps> = ({
  scoringModel,
}: ScoringModelDetailsProps) => {
  if (!scoringModel) return null

  return (
    <Space orientation="vertical">
      <Descriptions size="small">
        <Item label="State">{scoringModel.state?.name}</Item>
      </Descriptions>
      <Descriptions size="small">
        <Item label="Description">{scoringModel.description}</Item>
      </Descriptions>
    </Space>
  )
}

export default ScoringModelDetails
