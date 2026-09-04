'use client'

import { CsvImportForm } from '@/src/components/common/import'
import { useImportProductsMutation } from '@/src/store/features/product-management/products-api'
import { Typography } from 'antd'

const { Paragraph, Text } = Typography

export interface ImportProductsFormProps {
  onFormComplete: () => void
  onFormCancel: () => void
}

const COLUMNS =
  'Number,Name,Description,ProductTypeName,ParentNumber,ExternalId,Status,Tags'

const ImportProductsForm = ({
  onFormComplete,
  onFormCancel,
}: ImportProductsFormProps) => {
  const [importProducts] = useImportProductsMutation()

  const handleImport = async (file: File) => {
    const response = await importProducts(file)
    if (response.error) throw response.error
  }

  return (
    <CsvImportForm
      title="Import Products"
      columns={COLUMNS}
      onImport={handleImport}
      successMessage="Products imported successfully."
      onFormComplete={onFormComplete}
      onFormCancel={onFormCancel}
    >
      <Paragraph type="secondary">
        Loads a whole catalog from one CSV. Rows reference each other by a{' '}
        <Text code>Number</Text> that is used only within the file, so a child
        can name its parent before either exists.
      </Paragraph>
    </CsvImportForm>
  )
}

export default ImportProductsForm
