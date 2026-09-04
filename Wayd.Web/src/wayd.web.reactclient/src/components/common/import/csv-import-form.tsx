'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { isApiError, type ApiError } from '@/src/utils'
import { InboxOutlined } from '@ant-design/icons'
import { Alert, Modal, Typography, Upload } from 'antd'
import { ReactNode, useState } from 'react'

const { Dragger } = Upload
const { Paragraph, Text } = Typography

export interface CsvImportFormProps {
  /** Modal title, e.g. "Import Versions". */
  title: string
  /** The columns the endpoint expects, in order. Rendered as a copyable header row. */
  columns: string
  /**
   * A second file the import requires, where one applies — the packages import takes its manifest
   * this way, since a package cannot be assembled without one.
   */
  secondFile?: {
    /** What the file is, e.g. "Manifest". */
    label: string
    /** Its columns, rendered as a second copyable header row. */
    columns: string
    /**
     * Whether the import needs it. A package's manifest is required — a package cannot exist without
     * one — while a release's contents are optional, because an empty release is a real state.
     */
    required: boolean
  }
  /** What the file loads, shown above the column list. */
  children?: ReactNode
  /** Posts the file(s). Rejects with the API error on failure. */
  onImport: (file: File, secondFile?: File) => Promise<void>
  /** Success toast, e.g. "Versions imported successfully." */
  successMessage: string
  onFormComplete: () => void
  onFormCancel: () => void
}

/**
 * Turns an import's refusal into something a person can act on.
 *
 * An import fails in two shapes and they read differently: 422 carries per-row validation errors
 * naming the row, and 400 carries one message about the batch as a whole — an unresolvable name, a
 * duplicate, a broken reference. Both are worth showing verbatim, because they name the offending
 * value and that is what the reader has to go and fix.
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
 * The shared shape of every CSV import: pick one file, post it, and show what came back.
 *
 * One component rather than one per area because the endpoints behave identically — a single
 * multipart file, all-or-nothing, 422 for a bad row and 400 for a bad batch. Only the wording and the
 * mutation differ, so those are the props.
 */
const CsvImportForm = ({
  title,
  columns,
  secondFile,
  children,
  onImport,
  successMessage,
  onFormComplete,
  onFormCancel,
}: CsvImportFormProps) => {
  const messageApi = useMessage()

  // The browser File itself, not antd's UploadFile wrapper: the mutation needs the real thing, and
  // going through the wrapper means unwrapping an originFileObj that may or may not be set.
  const [file, setFile] = useState<File | null>(null)
  const [second, setSecond] = useState<File | null>(null)
  const [isImporting, setIsImporting] = useState(false)
  const [failure, setFailure] = useState<string | null>(null)

  const requiresSecond = secondFile?.required === true
  const isReady = file !== null && (!requiresSecond || second !== null)

  const handleOk = async () => {
    if (!file || (requiresSecond && !second)) return

    setIsImporting(true)
    setFailure(null)

    try {
      await onImport(file, second ?? undefined)
      messageApi.success(successMessage)
      onFormComplete()
    } catch (error) {
      // Shown in the modal rather than as a toast: a batch failure names the value to fix, and a
      // toast disappears before anyone can copy it out.
      setFailure(describeFailure(error))
    } finally {
      setIsImporting(false)
    }
  }

  return (
    <Modal
      title={title}
      open
      onOk={handleOk}
      okText="Import"
      okButtonProps={{ disabled: !isReady }}
      confirmLoading={isImporting}
      onCancel={onFormCancel}
      keyboard={false}
      destroyOnHidden
    >
      {children}

      <Paragraph type="secondary">
        The file must carry every column, even where a cell is empty:
      </Paragraph>
      <Paragraph>
        <Text code copyable style={{ fontSize: 12 }}>
          {columns}
        </Text>
      </Paragraph>

      <Alert
        type="info"
        showIcon
        title="All or nothing"
        description="If any reference cannot be resolved, the whole file is rejected and nothing is created."
        style={{ marginBottom: 16 }}
      />

      <Dragger
        accept=".csv,text/csv"
        maxCount={1}
        beforeUpload={(selected) => {
          setFile(selected)
          setFailure(null)
          // False keeps antd from uploading on drop: the file is posted by the Import button, so the
          // modal's action stays the thing that performs the import.
          return false
        }}
        onRemove={() => {
          setFile(null)
          setFailure(null)
        }}
        fileList={
          file ? [{ uid: file.name, name: file.name, status: 'done' }] : []
        }
      >
        <p className="ant-upload-drag-icon">
          <InboxOutlined />
        </p>
        <p className="ant-upload-text">Click or drag a CSV file here</p>
        <p className="ant-upload-hint">One file, one import.</p>
      </Dragger>

      {secondFile && (
        <>
          <Paragraph type="secondary" style={{ marginTop: 16 }}>
            {secondFile.label} columns:
          </Paragraph>
          <Paragraph>
            <Text code copyable style={{ fontSize: 12 }}>
              {secondFile.columns}
            </Text>
          </Paragraph>

          <Dragger
            accept=".csv,text/csv"
            maxCount={1}
            beforeUpload={(selected) => {
              setSecond(selected)
              setFailure(null)
              return false
            }}
            onRemove={() => {
              setSecond(null)
              setFailure(null)
            }}
            fileList={
              second
                ? [{ uid: second.name, name: second.name, status: 'done' }]
                : []
            }
          >
            <p className="ant-upload-drag-icon">
              <InboxOutlined />
            </p>
            <p className="ant-upload-text">
              Click or drag the {secondFile.label.toLowerCase()} CSV here
            </p>
            <p className="ant-upload-hint">
              {secondFile.required ? 'Required.' : 'Optional.'}
            </p>
          </Dragger>
        </>
      )}

      {failure && (
        <Alert
          type="error"
          showIcon
          title="The import was rejected"
          description={<span style={{ whiteSpace: 'pre-wrap' }}>{failure}</span>}
          style={{ marginTop: 16 }}
        />
      )}
    </Modal>
  )
}

export default CsvImportForm
