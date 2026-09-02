'use client'

import { caseInsensitiveCompare } from '@/src/components/common/wayd-grid'
import {
  ManifestEntryKind,
  ProductDto,
  ReleaseDto,
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
  releaseId?: string
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
  releases: ReleaseDto[]
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
 * Picking a release fills the version from it, since that is the version in all but the hand-authored
 * case. The field stays editable: a manifest can record a version that was never cut as a release.
 */
const ManifestEditor = ({
  value = [],
  onChange = () => {},
  products,
  releases,
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

        const releaseOptions = releases
          .filter((release) => release.product.id === entry.productId)
          .map((release) => ({ value: release.id, label: release.version }))

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
              // Changing the component invalidates a release chosen under the old one.
              onChange={(productId) =>
                updateEntry(index, { productId, releaseId: undefined })
              }
            />
            <Select
              style={{ width: '22%' }}
              placeholder="Release"
              options={releaseOptions}
              value={entry.releaseId}
              disabled={disabled || !entry.productId}
              allowClear
              showSearch
              optionFilterProp="label"
              onChange={(releaseId) => {
                const release = releases.find((r) => r.id === releaseId)
                updateEntry(index, {
                  releaseId,
                  // Only overwrite from a release, never clear on deselect — a version typed by
                  // hand should survive clearing the release that is not its source.
                  ...(release ? { version: release.version } : {}),
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
