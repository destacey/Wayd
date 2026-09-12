'use client'

import { CsvImportForm } from '@/src/components/common/import'
import { useImportDeploymentsMutation } from '@/src/store/features/product-management/deployments-api'
import { Typography } from 'antd'

const { Paragraph, Text } = Typography

export interface ImportDeploymentsFormProps {
  onFormComplete: () => void
  onFormCancel: () => void
}

const COLUMNS =
  'ImportId,VersionId,PackageId,EnvironmentName,ArtifactId,StartedAt,Outcome,CompletedAt,RolledBackAt,Reason'

const ImportDeploymentsForm = ({
  onFormComplete,
  onFormCancel,
}: ImportDeploymentsFormProps) => {
  const [importDeployments] = useImportDeploymentsMutation()

  const handleImport = (file: File) => importDeployments(file).unwrap()

  return (
    <CsvImportForm
      title="Import Deployments"
      columns={COLUMNS}
      onImport={handleImport}
      successMessage="Deployments imported."
      onFormComplete={onFormComplete}
      onFormCancel={onFormCancel}
    >
      <Paragraph type="secondary">
        Loads deployments of a version or a package, named by id, into an
        environment named by <Text code>EnvironmentName</Text>. There is no
        status column — <Text code>Outcome</Text> decides: blank leaves a
        deployment in flight, <Text code>Succeeded</Text> and{' '}
        <Text code>Failed</Text> complete it at <Text code>CompletedAt</Text>,
        and <Text code>RolledBack</Text> records a success and then the rollback
        at <Text code>RolledBackAt</Text>. Timestamps carry their offset, such
        as <Text code>2026-03-01T14:30:00Z</Text>.
      </Paragraph>
    </CsvImportForm>
  )
}

export default ImportDeploymentsForm
