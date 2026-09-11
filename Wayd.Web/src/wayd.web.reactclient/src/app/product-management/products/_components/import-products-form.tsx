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
  'ImportId,Name,Description,ProductTypeName,ParentImportId,ExternalId,Status,Tags'

const ImportProductsForm = ({
  onFormComplete,
  onFormCancel,
}: ImportProductsFormProps) => {
  const [importProducts] = useImportProductsMutation()

  const handleImport = (file: File) => importProducts(file).unwrap()

  return (
    <CsvImportForm
      title="Import Products"
      columns={COLUMNS}
      onImport={handleImport}
      successMessage="Products imported."
      onFormComplete={onFormComplete}
      onFormCancel={onFormCancel}
    >
      <Paragraph type="secondary">
        Loads a whole catalog from one CSV. Rows reference each other by an{' '}
        <Text code>ImportId</Text> that is used only within the file, so a child
        can name its parent before either exists.
      </Paragraph>
    </CsvImportForm>
  )
}

export default ImportProductsForm
