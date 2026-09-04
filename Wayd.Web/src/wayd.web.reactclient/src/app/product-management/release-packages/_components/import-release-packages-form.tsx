'use client'

import { CsvImportForm } from '@/src/components/common/import'
import { useImportReleasePackagesMutation } from '@/src/store/features/product-management/release-packages-api'
import { Typography } from 'antd'

const { Paragraph, Text } = Typography

export interface ImportReleasePackagesFormProps {
  onFormComplete: () => void
  onFormCancel: () => void
}

const PACKAGE_COLUMNS = 'Version,Name,TargetDate,ReleasedDate'
const MANIFEST_COLUMNS = 'PackageVersion,ProductName,VersionNumber,Kind'

const ImportReleasePackagesForm = ({
  onFormComplete,
  onFormCancel,
}: ImportReleasePackagesFormProps) => {
  const [importReleasePackages] = useImportReleasePackagesMutation()

  const handleImport = async (file: File, manifestFile?: File) => {
    if (!manifestFile) return

    const response = await importReleasePackages({ file, manifestFile })
    if (response.error) throw response.error
  }

  return (
    <CsvImportForm
      title="Import Release Packages"
      columns={PACKAGE_COLUMNS}
      secondFile={{ label: 'Manifest', columns: MANIFEST_COLUMNS, required: true }}
      onImport={handleImport}
      successMessage="Release packages imported successfully."
      onFormComplete={onFormComplete}
      onFormCancel={onFormCancel}
    >
      <Paragraph type="secondary">
        Takes two files. The first lists the packages; the second lists their
        manifest lines, each pointing back at its package by{' '}
        <Text code>PackageVersion</Text>. Both are required — a package cannot
        be assembled without a manifest.
      </Paragraph>
    </CsvImportForm>
  )
}

export default ImportReleasePackagesForm
