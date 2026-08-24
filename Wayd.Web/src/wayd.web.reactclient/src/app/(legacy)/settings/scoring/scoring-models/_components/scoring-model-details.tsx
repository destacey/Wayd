'use client'

import { ScoringModelDetailsDto } from '@/src/services/wayd-api'
import { Descriptions } from 'antd'

const { Item } = Descriptions

interface ScoringModelDetailsProps {
  scoringModel: ScoringModelDetailsDto | undefined
}

const ScoringModelDetails: React.FC<ScoringModelDetailsProps> = ({
  scoringModel,
}: ScoringModelDetailsProps) => {
  if (!scoringModel) return null

  return (
    <Descriptions column={1} size="small">
      <Item label="State">{scoringModel.state?.name}</Item>
      <Item label="Description">{scoringModel.description}</Item>
    </Descriptions>
  )
}

export default ScoringModelDetails
