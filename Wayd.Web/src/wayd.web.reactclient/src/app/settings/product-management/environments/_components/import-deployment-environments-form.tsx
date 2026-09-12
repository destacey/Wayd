'use client'

import { CsvImportForm } from '@/src/components/common/import'
import { useImportDeploymentEnvironmentsMutation } from '@/src/store/features/product-management/deployment-environments-api'
import { Typography } from 'antd'

const { Paragraph, Text } = Typography

export interface ImportDeploymentEnvironmentsFormProps {
  onFormComplete: () => void
  onFormCancel: () => void
}

const COLUMNS = 'ImportId,Name,Category,RingOrder,IsActive'

const ImportDeploymentEnvironmentsForm = ({
  onFormComplete,
  onFormCancel,
}: ImportDeploymentEnvironmentsFormProps) => {
  const [importEnvironments] = useImportDeploymentEnvironmentsMutation()

  const handleImport = (file: File) => importEnvironments(file).unwrap()

  return (
    <CsvImportForm
      title="Import Environments"
      columns={COLUMNS}
      onImport={handleImport}
      successMessage="Environments imported."
      onFormComplete={onFormComplete}
      onFormCancel={onFormCancel}
    >
      <Paragraph type="secondary">
        Loads environments by name. <Text code>Category</Text> is Development,
        Testing, Staging or Production. Leave <Text code>IsActive</Text> blank
        for an environment still in use, or set it to <Text code>false</Text> to
        create and retire one — for the environments a historical
        backfill&apos;s deployments still point at.
      </Paragraph>
    </CsvImportForm>
  )
}

export default ImportDeploymentEnvironmentsForm
