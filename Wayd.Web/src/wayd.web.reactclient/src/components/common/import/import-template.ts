/** What a cell must hold, in the terms a person filling in a spreadsheet uses. */
export type ImportColumnType =
  'text' | 'id' | 'date' | 'timestamp' | 'integer' | 'number' | 'boolean'

export interface ImportColumn {
  /** The header, exactly as the file must spell it. */
  readonly name: string
  readonly type: ImportColumnType
  /** Whether a row may leave the cell empty. The header itself is always required. */
  readonly required: boolean
  readonly description?: string
  /** The names the cell accepts, case-insensitively, where it parses into a fixed set. */
  readonly values?: readonly string[]
  readonly maxLength?: number
}

export interface ImportFileTemplate {
  /** The multipart field the endpoint reads the file from. */
  readonly field: string
  /** What the file is, for every file but the main one, which takes the import's own name. */
  readonly label?: string
  readonly required: boolean
  readonly columns: readonly ImportColumn[]
}

export interface ImportTemplate {
  /** The endpoint's own account of what the import does, from its OpenAPI description. */
  readonly description?: string
  readonly files: readonly ImportFileTemplate[]
}
