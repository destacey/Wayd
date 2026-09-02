'use client'

import { caseInsensitiveCompare } from '@/src/components/common/wayd-grid'
import {
  ManifestEntryKind,
  ProductDto,
  VersionDto,
} from '@/src/services/wayd-api'
import { DeleteOutlined, PlusOutlined } from '@ant-design/icons'
import { Button, Empty, Flex, Input, Select, Space, Typography } from 'antd'

const { Text } = Typography

/**
 * One manifest line as the editor holds it.
 *
 * `productId` is empty on a freshly added row — the row exists before it names anything, so the
 * caller validates rather than the type.
 */
export interface ManifestEntryDraft {
  productId: string
  versionId?: string
  version: string
  kind: ManifestEntryKind
}

/**
 * `value` and `onChange` are optional because a `Form.Item` supplies them — the editor is always
 * rendered as a form control, never with props of its own.
 */
export interface ManifestEditorProps {
  value?: ManifestEntryDraft[]
  onChange?: (entries: ManifestEntryDraft[]) => void
  products: ProductDto[]
  /** Releases across every product, used to offer versions for the product a row names. */
  versions: VersionDto[]
  isLoading?: boolean
  disabled?: boolean
}

export const emptyManifestEntry = (): ManifestEntryDraft => ({
  productId: '',
  version: '',
  kind: ManifestEntryKind.Changed,
})

/**
 * Edits a package manifest as a whole.
 *
 * A component may appear only once, so a product already named by another row is not offered again —
 * the API rejects a duplicate, and a picker that lets one be chosen turns that into a failed submit
 * rather than an unavailable choice.
 *
 * Picking a version record fills the version string from it, since that is the version in all but the hand-authored
 * case. The field stays editable: a manifest can record a version that was never cut here.
 */
const ManifestEditor = ({
  value = [],
  onChange = () => {},
  products,
  versions,
  isLoading,
  disabled,
}: ManifestEditorProps) => {
  const updateEntry = (index: number, patch: Partial<ManifestEntryDraft>) => {
    onChange(
      value.map((entry, i) => (i === index ? { ...entry, ...patch } : entry)),
    )
  }

  const removeEntry = (index: number) => {
    onChange(value.filter((_, i) => i !== index))
  }

  const productOptions = (products ?? [])
    .map((product) => ({ value: product.id, label: product.name }))
    .sort((a, b) => caseInsensitiveCompare(a.label, b.label))

  return (
    <Flex vertical gap="small">
      {value.length === 0 && (
        <Empty
          image={Empty.PRESENTED_IMAGE_SIMPLE}
          description="No components. A package ships at least one."
        />
      )}

      {value.map((entry, index) => {
        // Every product another row already names, so this row cannot name it too.
        const takenProductIds = new Set(
          value
            .filter((_, i) => i !== index)
            .map((other) => other.productId)
            .filter(Boolean),
        )

        const availableProducts = productOptions.filter(
          (option) => !takenProductIds.has(option.value),
        )

        const versionOptions = versions
          .filter((version) => version.product.id === entry.productId)
          .map((version) => ({ value: version.id, label: version.number }))

        return (
          <Space.Compact key={index} style={{ width: '100%' }}>
            <Select
              style={{ width: '30%' }}
              placeholder="Component"
              options={availableProducts}
              value={entry.productId || undefined}
              loading={isLoading}
              disabled={disabled}
              showSearch
              optionFilterProp="label"
              // Changing the component invalidates a version record chosen under the old one.
              onChange={(productId) =>
                updateEntry(index, { productId, versionId: undefined })
              }
            />
            <Select
              style={{ width: '22%' }}
              placeholder="Version"
              options={versionOptions}
              value={entry.versionId}
              disabled={disabled || !entry.productId}
              allowClear
              showSearch
              optionFilterProp="label"
              onChange={(versionId) => {
                const version = versions.find((v) => v.id === versionId)
                updateEntry(index, {
                  versionId,
                  // Only overwrite from a version record, never clear on deselect — a version typed by
                  // hand should survive clearing the version record that is not its source.
                  ...(version ? { version: version.number } : {}),
                })
              }}
            />
            <Input
              style={{ width: '23%' }}
              placeholder="Version"
              value={entry.version}
              disabled={disabled}
              maxLength={128}
              onChange={(e) => updateEntry(index, { version: e.target.value })}
            />
            <Select
              style={{ width: '20%' }}
              options={[
                { value: ManifestEntryKind.Changed, label: 'Changed' },
                {
                  value: ManifestEntryKind.CarriedForward,
                  label: 'Carried Forward',
                },
              ]}
              value={entry.kind}
              disabled={disabled}
              onChange={(kind) => updateEntry(index, { kind })}
            />
            <Button
              icon={<DeleteOutlined />}
              disabled={disabled}
              onClick={() => removeEntry(index)}
              aria-label={`Remove component ${index + 1}`}
            />
          </Space.Compact>
        )
      })}

      <Flex justify="space-between" align="center">
        <Button
          type="dashed"
          icon={<PlusOutlined />}
          disabled={disabled || value.length >= productOptions.length}
          onClick={() => onChange([...value, emptyManifestEntry()])}
        >
          Add Component
        </Button>
        <Text type="secondary">
          {value.length} component{value.length === 1 ? '' : 's'}
        </Text>
      </Flex>
    </Flex>
  )
}

export default ManifestEditor
