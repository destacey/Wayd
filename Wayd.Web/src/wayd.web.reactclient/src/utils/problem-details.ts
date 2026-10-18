import { FieldData, NamePath } from '@rc-component/form/lib/interface'

export interface ApiError {
  status?: number
  detail?: string
  errors?: { [key: string]: string[] }
  supportMessage?: string
  [key: string]: unknown
}

export const isApiError = (error: unknown): error is ApiError =>
  typeof error === 'object' && error !== null && ('status' in error || 'detail' in error || 'errors' in error)

const normalizeName = (raw: string) => {
  const trimmed = (raw ?? '').trim()
  if (!trimmed) return trimmed
  return trimmed.charAt(0).toLowerCase() + trimmed.slice(1)
}

const normalizeDottedName = (raw: string) => {
  const trimmed = (raw ?? '').trim()
  if (!trimmed) return trimmed
  return trimmed
    .split('.')
    .filter(Boolean)
    .map((segment) =>
      segment ? segment.charAt(0).toLowerCase() + segment.slice(1) : segment,
    )
    .join('.')
}

const stripJsonPathPrefix = (key: string) => key.replace(/^\$\.?/, '')

const lastPathSegment = (key: string) => {
  const normalized = stripJsonPathPrefix(key)
  const segment = normalized.split('.').filter(Boolean).pop() ?? key
  return segment.replace(/\[.*\]$/, '')
}

export interface ToFormErrorsOptions {
  /**
   * Places an error on a field whose name differs from the request property,
   * such as a range picker holding both `start` and `end`. Receives the
   * camel-cased dotted key (`start`, `iterations[0].end`); return the field's
   * name path, or `undefined` to fall back to the default name guesses.
   */
  fieldFor?: (key: string) => NamePath | undefined
}

/**
 * Turns a problem details `errors` object into antd form field errors. Each key
 * is offered under several spellings, since the server's casing and path
 * syntax rarely match the form's field names exactly; names no field has are
 * ignored by the form.
 */
const toFormErrors = (
  problemDetails: {
    [property: string]: string[] | string
  },
  options?: ToFormErrorsOptions,
): FieldData[] => {
  if (!problemDetails) return []

  const fields = new Map<string, { name: NamePath; errors: string[] }>()

  const add = (name: NamePath, errors: string[]) => {
    const id = JSON.stringify(name)
    const existing = fields.get(id)
    if (!existing) {
      fields.set(id, { name, errors: [...errors] })
      return
    }
    for (const message of errors) {
      if (!existing.errors.includes(message)) {
        existing.errors.push(message)
      }
    }
  }

  for (const key of Object.keys(problemDetails)) {
    const rawErrors = problemDetails[key]
    const errors = Array.isArray(rawErrors) ? rawErrors : [rawErrors]

    const normalizedKey = stripJsonPathPrefix(key)
    const mapped = options?.fieldFor?.(normalizeDottedName(normalizedKey))
    if (mapped !== undefined) {
      add(mapped, errors)
      continue
    }

    const candidates = [
      key,
      normalizedKey,
      lastPathSegment(key),
      normalizeName(key),
      normalizeName(normalizedKey),
      normalizeName(lastPathSegment(key)),
      normalizeDottedName(key),
      normalizeDottedName(normalizedKey),
    ].filter(Boolean)

    for (const candidate of candidates) add(candidate, errors)
  }

  return Array.from(fields.values())
}

/**
 * The errors for a form whose values live in component state rather than antd
 * form fields, by the name `fieldFor` gives each key (receiving the same
 * camel-cased dotted key as {@link ToFormErrorsOptions.fieldFor}). Keys it
 * does not map are left out.
 */
export const errorsByField = (
  problemDetails: { [property: string]: string[] | string },
  fieldFor: (key: string) => string | undefined,
): Record<string, string[]> => {
  const fields: Record<string, string[]> = {}
  for (const [key, rawErrors] of Object.entries(problemDetails ?? {})) {
    const name = fieldFor(normalizeDottedName(stripJsonPathPrefix(key)))
    if (name === undefined) continue
    const errors = Array.isArray(rawErrors) ? rawErrors : [rawErrors]
    fields[name] = [...new Set([...(fields[name] ?? []), ...errors])]
  }
  return fields
}

/**
 * A {@link ToFormErrorsOptions.fieldFor} for a form whose range picker sends
 * two request properties: shows both properties' errors on the picker.
 */
export const rangeErrorsOn =
  (field: string, startKey = 'start', endKey = 'end') =>
  (key: string): NamePath | undefined =>
    key === startKey || key === endKey ? field : undefined

export default toFormErrors
