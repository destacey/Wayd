/**
 * Reads the import templates out of the API's OpenAPI document.
 *
 * Every import endpoint carries an `x-wayd-import` extension naming its import definition and, for each
 * file it takes, the component schema of the rows that file holds. Each property of those schemas carries
 * `x-csv-column` — the header as the file spells it, which the camel-cased JSON name cannot be turned
 * back into — and, where the column parses into an enum, `x-csv-values`.
 *
 * Kept free of file access so the drift test can run it against the committed document.
 */

const IMPORT_EXTENSION = 'x-wayd-import'
const COLUMN_EXTENSION = 'x-csv-column'
const VALUES_EXTENSION = 'x-csv-values'

const HTTP_METHODS = ['get', 'put', 'post', 'delete', 'patch']

const resolve = (spec, schema) => {
  if (!schema?.$ref) return schema
  return spec.components.schemas[
    schema.$ref.replace('#/components/schemas/', '')
  ]
}

/** What a person has to type into the cell, in words. */
const columnType = (property) => {
  const actual = property.oneOf?.[0] ?? property.allOf?.[0] ?? property

  switch (actual.format) {
    case 'guid':
    case 'uuid':
      return 'id'
    case 'date':
      return 'date'
    case 'date-time':
      return 'timestamp'
  }

  switch (actual.type) {
    case 'integer':
      return 'integer'
    case 'number':
      return 'number'
    case 'boolean':
      return 'boolean'
    default:
      return 'text'
  }
}

const buildColumns = (spec, schemaName) => {
  const schema = spec.components.schemas[schemaName]
  if (!schema) {
    throw new Error(`The document has no schema named '${schemaName}'.`)
  }

  const required = new Set(schema.required ?? [])

  return Object.entries(schema.properties ?? {}).map(([jsonName, raw]) => {
    const property = resolve(spec, raw)
    const name = raw[COLUMN_EXTENSION] ?? property[COLUMN_EXTENSION]
    if (!name) {
      throw new Error(
        `${schemaName}.${jsonName} has no ${COLUMN_EXTENSION}; the API's CsvImportOperationProcessor did not run over it.`,
      )
    }

    const column = {
      name,
      type: columnType(raw),
      required: required.has(jsonName),
    }

    // XML doc comments keep their source line breaks; the form lays the text out itself.
    const description = (raw.description ?? property.description)
      ?.replace(/\s+/g, ' ')
      .trim()
    if (description) column.description = description

    const values = raw[VALUES_EXTENSION]
    if (values?.length) column.values = values

    if (raw.maxLength) column.maxLength = raw.maxLength

    return column
  })
}

/** The module an endpoint belongs to: the first segment after `api` — `ppm` in `/api/ppm/projects/import`. */
const moduleOf = (path) => {
  const [prefix, module] = path.split('/').filter(Boolean)
  if (prefix !== 'api' || !module) {
    throw new Error(`Cannot read a module from the import route '${path}'.`)
  }
  return module
}

/**
 * @returns {Record<string, { module: string, description?: string, files: { field: string, label?: string, required: boolean, columns: object[] }[] }>}
 *   One entry per import definition key, sorted by key so the generated file diffs cleanly.
 */
export const buildImportTemplates = (spec) => {
  const templates = {}

  for (const [path, pathItem] of Object.entries(spec.paths ?? {})) {
    for (const method of HTTP_METHODS) {
      const operation = pathItem[method]
      const extension = operation?.[IMPORT_EXTENSION]
      if (!extension) continue

      if (templates[extension.key]) {
        throw new Error(`Two endpoints declare the import '${extension.key}'.`)
      }

      const template = { module: moduleOf(path) }
      const description = operation.description?.replace(/\s+/g, ' ').trim()
      if (description) template.description = description
      template.files = extension.files.map((file) => {
        const built = { field: file.field }
        if (file.label) built.label = file.label
        built.required = file.required
        built.columns = buildColumns(spec, file.schema)
        return built
      })

      templates[extension.key] = template
    }
  }

  return Object.fromEntries(
    Object.entries(templates).sort(([a], [b]) => a.localeCompare(b)),
  )
}
