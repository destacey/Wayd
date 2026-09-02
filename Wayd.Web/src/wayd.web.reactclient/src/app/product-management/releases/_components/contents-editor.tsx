'use client'

import { caseInsensitiveCompare } from '@/src/components/common/wayd-grid'
import { ReleasePackageDto, VersionDto } from '@/src/services/wayd-api'
import { Alert, Flex, Select, Typography } from 'antd'

const { Text } = Typography

/**
 * The contents a release announces, as the editor holds them.
 *
 * Both routes together, because they are set together: the rule that a version is announced once
 * spans them, so a form editing one without the other could only judge that rule against half of the
 * intended result.
 */
export interface ContentsDraft {
  versionIds: string[]
  packageIds: string[]
}

/**
 * `value` and `onChange` are optional because a `Form.Item` supplies them — the editor is always
 * rendered as a form control, never with props of its own.
 */
export interface ContentsEditorProps {
  value?: ContentsDraft
  onChange?: (contents: ContentsDraft) => void
  versions: VersionDto[]
  packages: ReleasePackageDto[]
  isLoading?: boolean
  disabled?: boolean
}

/**
 * Every version reachable through the given packages, by package name.
 *
 * Mirrors what the API resolves: a manifest line links to a version record only where one was
 * recorded, and a carried-forward line naming a version string Wayd never held links to nothing. Those
 * lines cover no version, so they must not be treated as a conflict — the API would accept a version
 * they name being carried directly, and disabling it here would refuse something legitimate.
 */
const coverageByVersionId = (
  packages: ReleasePackageDto[],
  selectedPackageIds: string[],
): Map<string, string> => {
  const covered = new Map<string, string>()

  packages
    .filter((pkg) => selectedPackageIds.includes(pkg.id))
    .forEach((pkg) => {
      ;(pkg.components ?? []).forEach((component) => {
        if (component.versionRecord && !covered.has(component.versionRecord.id)) {
          covered.set(component.versionRecord.id, pkg.version)
        }
      })
    })

  return covered
}

/**
 * Sets everything a release announces, both routes at once.
 *
 * A version already shipping inside a selected package cannot also be carried directly, so it is
 * offered as disabled and labelled with the package covering it. Naming the package answers "why not?"
 * without a second lookup, and leaving the option listed rather than filtering it out keeps a version
 * from appearing to be missing data.
 *
 * Because both routes are edited here, that coverage is recomputed as packages are selected: dropping
 * a package frees the versions it covered, in the same form and without a save in between. This is
 * what makes moving a version from one route to the other a single change of mind rather than an
 * ordered pair of edits.
 */
const ContentsEditor = ({
  value = { versionIds: [], packageIds: [] },
  onChange = () => {},
  versions,
  packages,
  isLoading,
  disabled,
}: ContentsEditorProps) => {
  const covered = coverageByVersionId(packages, value.packageIds)

  // A version already carried directly that a newly-selected package would also ship. The picker
  // cannot prevent this on its own: the package is chosen in the other field, so the conflict is
  // reported and cleared by dropping either side.
  const conflicts = value.versionIds
    .filter((id) => covered.has(id))
    .map((id) => {
      const version = versions.find((v) => v.id === id)
      return {
        id,
        label: version
          ? `${version.product?.name ?? ''} ${version.number}`.trim()
          : 'A version',
        packageVersion: covered.get(id)!,
      }
    })

  const packageOptions = packages
    .map((pkg) => ({
      value: pkg.id,
      label: pkg.name ? `${pkg.version} — ${pkg.name}` : pkg.version,
    }))
    .sort((a, b) => caseInsensitiveCompare(a.label, b.label))

  const versionOptions = versions
    .map((version) => {
      const coveringPackage = covered.get(version.id)
      const name = `${version.product?.name ?? ''} ${version.number}`.trim()

      return {
        value: version.id,
        label: coveringPackage ? `${name} — in ${coveringPackage}` : name,
        // Disabled only while it is covered and not already chosen. A version that is both selected
        // and covered stays selectable so it can be deselected: locking it would strand the release in
        // a state the form could not leave.
        disabled: !!coveringPackage && !value.versionIds.includes(version.id),
      }
    })
    .sort((a, b) => caseInsensitiveCompare(a.label, b.label))

  return (
    <Flex vertical gap="middle">
      <Flex vertical gap={4}>
        <Text strong>Packages</Text>
        <Text type="secondary" style={{ fontSize: 12 }}>
          The usual route — a package is the deployment unit, so most contents arrive this way.
        </Text>
        <Select
          mode="multiple"
          placeholder="Select packages"
          options={packageOptions}
          value={value.packageIds}
          loading={isLoading}
          disabled={disabled}
          showSearch
          optionFilterProp="label"
          style={{ width: '100%' }}
          onChange={(packageIds) => onChange({ ...value, packageIds })}
        />
      </Flex>

      <Flex vertical gap={4}>
        <Text strong>Versions carried directly</Text>
        <Text type="secondary" style={{ fontSize: 12 }}>
          For an artifact that shipped on its own, where nobody assembled a package.
        </Text>
        <Select
          mode="multiple"
          placeholder="Select versions"
          options={versionOptions}
          value={value.versionIds}
          loading={isLoading}
          disabled={disabled}
          showSearch
          optionFilterProp="label"
          style={{ width: '100%' }}
          onChange={(versionIds) => onChange({ ...value, versionIds })}
        />
      </Flex>

      {conflicts.length > 0 && (
        <Alert
          type="error"
          showIcon
          title="A version is carried twice"
          description={
            <Flex vertical gap={4}>
              {conflicts.map((conflict) => (
                <span key={conflict.id}>
                  <strong>{conflict.label}</strong> is carried directly and also ships
                  inside {conflict.packageVersion}. Remove it from the versions, or
                  drop that package.
                </span>
              ))}
            </Flex>
          }
        />
      )}
    </Flex>
  )
}

export default ContentsEditor
