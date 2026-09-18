'use client'

import { caseInsensitiveCompare } from '@/src/components/common/wayd-grid'
import { useMessage } from '@/src/components/contexts/messaging'
import {
  ImportAtomicity,
  ImportDefinitionDto,
  ImportProcessDto,
  ImportProcessStatus,
} from '@/src/services/wayd-api'
import { useSubmitImportMutation } from '@/src/store/features/admin/imports-api'
import { isApiError, toFileName, type ApiError } from '@/src/utils'
import { downloadCsv, generateCsv } from '@/src/utils/csv-utils'
import {
  CheckCircleOutlined,
  DownloadOutlined,
  InboxOutlined,
} from '@ant-design/icons'
import {
  Alert,
  Button,
  Collapse,
  Flex,
  Form,
  Modal,
  Select,
  Typography,
  Upload,
} from 'antd'
import Link from 'next/link'
import { useRouter } from 'next/navigation'
import { useState } from 'react'
import ImportColumnsTable from './import-columns-table'
import type { ImportFileTemplate, ImportTemplate } from './import-template'
import { ImportKey, importTemplates } from './import-templates.generated'

const { Dragger } = Upload
const { Paragraph, Text } = Typography

export interface CsvImportFormProps {
  /**
   * The import types the viewer can see. Only those they may submit, and that this client knows the
   * template for, are offered.
   */
  definitions: ImportDefinitionDto[]
  onFormComplete: () => void
  onFormCancel: () => void
}

/**
 * Whether this client can post the import. A server newer than the client can list a definition it has no
 * template or submitter for.
 */
export const isKnownImport = (key: string): key is ImportKey =>
  key in importTemplates

/**
 * Turns an import's refusal into something a person can act on.
 *
 * Only the refusals that happen before the run starts reach here. 422 carries per-row shape errors
 * naming the row, plus the file-level rules the submission checks as a set — a repeated key, a parent
 * cycle, a child row naming no parent. 400 carries one message about the submission itself. Both are
 * worth showing verbatim, because they name the offending value and that is what the reader has to go
 * and fix.
 *
 * A reference that does not resolve is NOT one of these: the run accepts the file and rejects the row,
 * so it arrives as a finished run with rejections — see {@link describeRun}.
 */
const describeFailure = (error: unknown): string => {
  const apiError: ApiError = isApiError(error) ? error : {}

  if (apiError.status === 422 && apiError.errors) {
    const messages = Object.values(apiError.errors).flat().filter(Boolean)
    if (messages.length > 0) {
      return messages.join('\n')
    }
  }

  return apiError.detail ?? 'The import failed. Check the file and try again.'
}

/**
 * Sums up a run that finished without applying every row, or any preflight. The row-by-row reasons are on
 * its page.
 */
const describeRun = (run: ImportProcessDto) => {
  const rejected =
    run.failedRowCount > 0
      ? `${run.failedRowCount} row${run.failedRowCount === 1 ? ' was' : 's were'} rejected.`
      : ''

  if (run.isPreflight) {
    const title =
      run.status === ImportProcessStatus.Cancelled
        ? 'The check was stopped'
        : run.failedRowCount === 0
          ? run.totalRowCount === 1
            ? 'The row passed'
            : `All ${run.totalRowCount} rows passed`
          : `${run.succeededRowCount} of ${run.totalRowCount} rows passed`

    return {
      title,
      description: [rejected, run.error, 'Nothing has been imported.']
        .filter(Boolean)
        .join(' '),
    }
  }

  const title =
    run.status === ImportProcessStatus.Cancelled
      ? 'The import was stopped'
      : run.succeededRowCount === 0
        ? 'Nothing was imported'
        : `${run.succeededRowCount} of ${run.totalRowCount} rows were imported`

  return { title, description: [rejected, run.error].filter(Boolean).join(' ') }
}

const alertTypeFor = (run: ImportProcessDto) =>
  run.isPreflight && run.status === ImportProcessStatus.Succeeded
    ? 'success'
    : run.succeededRowCount > 0
      ? 'warning'
      : 'error'

type ImportModule = (typeof importTemplates)[ImportKey]['module']

/**
 * The group each API module's imports are listed under. Keyed by every generated module, so an import from
 * a new module cannot be offered without a group. Strategic themes sit with PPM, where they are set up
 * alongside the portfolios and initiatives that use them.
 */
const IMPORT_GROUPS: Record<ImportModule, string> = {
  organization: 'Organization',
  planning: 'Planning',
  ppm: 'Project Portfolio Management',
  'product-management': 'Product Management',
  'strategic-management': 'Project Portfolio Management',
}

const fileTitle = (definition: ImportDefinitionDto, file: ImportFileTemplate) =>
  file.label ?? definition.displayName

// A secondary file keeps its import's name too: "manifest-import-template.csv" says nothing once it is
// sitting in a downloads folder.
const templateFileName = (
  definition: ImportDefinitionDto,
  file: ImportFileTemplate,
) =>
  [definition.displayName, file.label, 'import-template']
    .filter(Boolean)
    .map((part) => toFileName(part!))
    .join('-') + '.csv'

/**
 * The one place a CSV import is submitted from the app: pick the kind of import, read what its files
 * must carry, take a template, and post the file.
 *
 * Every import endpoint behaves identically — multipart files, answered with the run, and the same two
 * refusal shapes before it starts — so the kind of import is a choice inside the form, and what differs
 * between them comes from the generated templates and the definition list.
 *
 * The endpoint waits a few seconds for the run, so most files come back finished. One that applied every
 * row closes the form; one that did not stays open to say so, linking to the rows; one still running
 * hands over to its page, which follows it to the end. Check File posts the same files as a preflight,
 * which always stays open to report, since it imported nothing.
 */
const CsvImportForm = ({
  definitions,
  onFormComplete,
  onFormCancel,
}: CsvImportFormProps) => {
  const messageApi = useMessage()
  const router = useRouter()
  const [submitImport] = useSubmitImportMutation()

  const [importKey, setImportKey] = useState<ImportKey>()
  // Browser Files rather than antd's UploadFile wrappers: the client needs the real thing, and the
  // wrapper's originFileObj may or may not be set.
  const [files, setFiles] = useState<Partial<Record<string, File>>>({})
  const [submitting, setSubmitting] = useState<'import' | 'check' | null>(null)
  const [failure, setFailure] = useState<string | null>(null)
  const [finishedRun, setFinishedRun] = useState<ImportProcessDto | null>(null)

  const offered = definitions
    .filter((d) => d.canSubmit && isKnownImport(d.key))
    .sort((a, b) => caseInsensitiveCompare(a.displayName, b.displayName))

  // Groups by name, and only the ones holding an import this viewer may submit.
  const groups = Object.entries(
    Object.groupBy(
      offered,
      (d) => IMPORT_GROUPS[importTemplates[d.key as ImportKey].module],
    ),
  )
    .sort(([a], [b]) => caseInsensitiveCompare(a, b))
    .map(([label, imports]) => ({
      label,
      title: label,
      options: (imports ?? []).map((d) => ({
        value: d.key as ImportKey,
        label: d.displayName,
      })),
    }))

  const definition = offered.find((d) => d.key === importKey)
  const template: ImportTemplate | undefined = importKey
    ? importTemplates[importKey]
    : undefined

  const isReady =
    !!template && template.files.every((f) => !f.required || files[f.field])

  const resetResult = () => {
    setFailure(null)
    setFinishedRun(null)
  }

  const selectImport = (key: ImportKey) => {
    setImportKey(key)
    setFiles({})
    resetResult()
  }

  const setFile = (field: string, file: File | undefined) => {
    setFiles((current) => ({ ...current, [field]: file }))
    resetResult()
  }

  const downloadTemplate = (file: ImportFileTemplate) => {
    if (!definition) return
    downloadCsv(
      generateCsv(
        file.columns.map((c) => c.name),
        [],
      ),
      templateFileName(definition, file),
    )
  }

  const submit = async (validateOnly: boolean) => {
    if (!importKey || !definition || !isReady) return

    setSubmitting(validateOnly ? 'check' : 'import')
    resetResult()

    try {
      const run = await submitImport({
        importKey,
        files,
        validateOnly,
      }).unwrap()

      if (!run.isTerminal) {
        messageApi.info(
          validateOnly
            ? 'The check is still running. Opening its page.'
            : 'The import is still running. Opening its page.',
        )
        onFormComplete()
        router.push(`/settings/imports/${run.id}`)
        return
      }

      // A preflight always stays open to report, clean or not: closing would hide that nothing was imported.
      if (!validateOnly && run.status === ImportProcessStatus.Succeeded) {
        messageApi.success(`${definition.displayName} imported.`)
        onFormComplete()
        return
      }

      setFinishedRun(run)
    } catch (error) {
      // Shown in the modal rather than as a toast: a batch failure names the value to fix, and a
      // toast disappears before anyone can copy it out.
      setFailure(describeFailure(error))
    } finally {
      setSubmitting(null)
    }
  }

  const preflightCapNote =
    definition && definition.preflightMaxRows < definition.maxRows
      ? ` A check covers up to ${definition.preflightMaxRows.toLocaleString()}.`
      : ''

  return (
    <Modal
      title="Import"
      open
      width={760}
      onOk={() => submit(false)}
      okText="Import"
      okButtonProps={{ disabled: !isReady || submitting === 'check' }}
      confirmLoading={submitting === 'import'}
      onCancel={onFormCancel}
      footer={(_, { OkBtn, CancelBtn }) => (
        <>
          <CancelBtn />
          <Button
            icon={<CheckCircleOutlined />}
            onClick={() => submit(true)}
            disabled={!isReady || submitting === 'import'}
            loading={submitting === 'check'}
          >
            Check File
          </Button>
          <OkBtn />
        </>
      )}
      keyboard={false}
      destroyOnHidden
    >
      <Form layout="vertical">
        <Form.Item label="What are you importing?" required>
          <Select<ImportKey>
            showSearch={{
              // Matches imports by name only. antd otherwise matches a group's name too and keeps
              // the whole group, so "management" would list every Product Management import.
              filterOption: (input, option) =>
                !option?.options &&
                String(option?.label ?? '')
                  .toLowerCase()
                  .includes(input.toLowerCase()),
            }}
            placeholder="Select an import"
            value={importKey}
            onChange={selectImport}
            options={groups}
            notFoundContent={
              offered.length === 0
                ? 'You have no imports you may submit.'
                : 'No import matches.'
            }
          />
        </Form.Item>
      </Form>

      {definition && template && (
        <>
          {template.description && (
            <Paragraph type="secondary">{template.description}</Paragraph>
          )}

          {definition.atomicity === ImportAtomicity.Atomic ? (
            <Alert
              type="info"
              showIcon
              title="All or nothing"
              description={`The file applies as one unit: if any row is rejected, nothing is created. Up to ${definition.maxRows.toLocaleString()} rows.${preflightCapNote}`}
              style={{ marginBottom: 16 }}
            />
          ) : (
            <Alert
              type="info"
              showIcon
              title="Row by row"
              description={`Each row applies on its own: a rejected row changes nothing, and the rest are still imported. Up to ${definition.maxRows.toLocaleString()} rows.${preflightCapNote}`}
              style={{ marginBottom: 16 }}
            />
          )}

          {template.files.map((file) => {
            const title = fileTitle(definition, file)
            const selected = files[file.field]

            return (
              <Flex
                key={`${importKey}-${file.field}`}
                vertical
                gap={8}
                style={{ marginBottom: 16 }}
              >
                <Flex justify="space-between" align="center" gap={8} wrap>
                  <Text strong>
                    {title} file
                    {!file.required && (
                      <Text type="secondary"> (optional)</Text>
                    )}
                  </Text>
                  <Button
                    size="small"
                    icon={<DownloadOutlined />}
                    onClick={() => downloadTemplate(file)}
                  >
                    Download Template
                  </Button>
                </Flex>

                <Collapse
                  size="small"
                  items={[
                    {
                      key: 'columns',
                      label: `${file.columns.length} columns — the file must carry every one, even where a cell is empty`,
                      children: <ImportColumnsTable columns={file.columns} />,
                    },
                  ]}
                />

                <Dragger
                  accept=".csv,text/csv"
                  maxCount={1}
                  beforeUpload={(chosen) => {
                    setFile(file.field, chosen)
                    // False keeps antd from uploading on drop: the Import button posts the files, so
                    // the modal's action stays the thing that performs the import.
                    return false
                  }}
                  onRemove={() => setFile(file.field, undefined)}
                  fileList={
                    selected
                      ? [
                          {
                            uid: selected.name,
                            name: selected.name,
                            status: 'done',
                          },
                        ]
                      : []
                  }
                >
                  <p className="ant-upload-drag-icon">
                    <InboxOutlined />
                  </p>
                  <p className="ant-upload-text">
                    Click or drag the {title.toLowerCase()} CSV here
                  </p>
                </Dragger>
              </Flex>
            )
          })}
        </>
      )}

      {failure && (
        <Alert
          type="error"
          showIcon
          title="The import was rejected"
          description={
            <span style={{ whiteSpace: 'pre-wrap' }}>{failure}</span>
          }
          style={{ marginTop: 16 }}
        />
      )}

      {finishedRun && (
        <Alert
          type={alertTypeFor(finishedRun)}
          showIcon
          {...describeRun(finishedRun)}
          action={
            <Link href={`/settings/imports/${finishedRun.id}`}>
              View Details
            </Link>
          }
          style={{ marginTop: 16 }}
        />
      )}
    </Modal>
  )
}

export default CsvImportForm
