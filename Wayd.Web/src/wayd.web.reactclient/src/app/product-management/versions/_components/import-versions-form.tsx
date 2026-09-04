'use client'

import { CsvImportForm } from '@/src/components/common/import'
import { useImportVersionsMutation } from '@/src/store/features/product-management/versions-api'
import { Typography } from 'antd'

const { Paragraph, Text } = Typography

export interface ImportVersionsFormProps {
  onFormComplete: () => void
  onFormCancel: () => void
}

const COLUMNS =
  'ProductName,Number,Name,TargetDate,CutDate,ReleasedDate,Sequence,Notes'

const ImportVersionsForm = ({
  onFormComplete,
  onFormCancel,
}: ImportVersionsFormProps) => {
  const [importVersions] = useImportVersionsMutation()

  const handleImport = async (file: File) => {
    const response = await importVersions(file)
    if (response.error) throw response.error
  }

  return (
    <CsvImportForm
      title="Import Versions"
      columns={COLUMNS}
      onImport={handleImport}
      successMessage="Versions imported successfully."
      onFormComplete={onFormComplete}
      onFormCancel={onFormCancel}
    >
      <Paragraph type="secondary">
        Loads versions against products named by <Text code>ProductName</Text>.
        There is no status column — the dates decide: no dates leaves a version
        planned, a <Text code>CutDate</Text> makes it ready, and a{' '}
        <Text code>ReleasedDate</Text> makes it released.
      </Paragraph>
    </CsvImportForm>
  )
}

export default ImportVersionsForm
