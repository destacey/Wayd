'use client'

import { ExpenditureCategoryDetailsDto } from '@/src/services/wayd-api'
import { Descriptions } from 'antd'

const { Item } = Descriptions

interface ExpenditureCategoryDetailsProps {
  expenditureCategory: ExpenditureCategoryDetailsDto
}

const ExpenditureCategoryDetails: React.FC<ExpenditureCategoryDetailsProps> = ({
  expenditureCategory,
}: ExpenditureCategoryDetailsProps) => {
  if (!expenditureCategory) return null

  return (
    <Descriptions column={1} size="small">
      <Item label="State">{expenditureCategory.state.name}</Item>
      <Item label="Capitalizable">
        {expenditureCategory.isCapitalizable?.toString()}
      </Item>
      <Item label="Requires Depreciation">
        {expenditureCategory.requiresDepreciation?.toString()}
      </Item>
      <Item label="Accounting Code">{expenditureCategory.accountingCode}</Item>
      <Item label="Description">{expenditureCategory.description}</Item>
    </Descriptions>
  )
}

export default ExpenditureCategoryDetails
