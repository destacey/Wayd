'use client'

import { CsvImportForm } from '@/src/components/common/import'
import { useImportReleasesMutation } from '@/src/store/features/product-management/releases-api'
import { Typography } from 'antd'

const { Paragraph, Text } = Typography

export interface ImportReleasesFormProps {
  onFormComplete: () => void
  onFormCancel: () => void
}

const RELEASE_COLUMNS =
  'Version,Name,ProductName,TargetDate,ReleasedDate,Sequence,Notes'
const CONTENTS_COLUMNS =
  'ReleaseVersion,Kind,PackageVersion,ProductName,VersionNumber'

const ImportReleasesForm = ({
  onFormComplete,
  onFormCancel,
}: ImportReleasesFormProps) => {
  const [importReleases] = useImportReleasesMutation()

  const handleImport = async (file: File, contentsFile?: File) => {
    const response = await importReleases({ file, contentsFile })
    if (response.error) throw response.error
  }

  return (
    <CsvImportForm
      title="Import Releases"
      columns={RELEASE_COLUMNS}
      secondFile={{
        label: 'Contents',
        columns: CONTENTS_COLUMNS,
        required: false,
      }}
      onImport={handleImport}
      successMessage="Releases imported successfully."
      onFormComplete={onFormComplete}
      onFormCancel={onFormCancel}
    >
      <Paragraph type="secondary">
        The first file lists the releases. The second, optional, lists what each
        one announces — a <Text code>Kind</Text> of <Text code>Package</Text> or{' '}
        <Text code>Version</Text>, pointing back by{' '}
        <Text code>ReleaseVersion</Text>.
      </Paragraph>
      <Paragraph type="secondary">
        A release given a <Text code>ReleasedDate</Text> is refused while
        anything it carries has not shipped, so import those versions and
        packages first.
      </Paragraph>
    </CsvImportForm>
  )
}

export default ImportReleasesForm
