import * as fs from 'node:fs'
import * as path from 'node:path'
import { buildImportTemplates } from '@/scripts/import-templates.mjs'
import { importTemplates } from './import-templates.generated'

const SPEC_PATH = path.join(
  __dirname,
  '..',
  '..',
  '..',
  '..',
  '..',
  'Wayd.Web.Api',
  'wwwroot',
  'api',
  'v1',
  'specification.json',
)

describe('importTemplates', () => {
  it('matches the OpenAPI document it is generated from', () => {
    // Arrange — the document is regenerated on every Debug build of the API, so a change to an import's
    // row class lands there first; this fails until `npm run generate:import-templates` catches up.
    const spec = JSON.parse(fs.readFileSync(SPEC_PATH, 'utf8'))

    // Act
    const expected = buildImportTemplates(spec)

    // Assert
    expect(importTemplates).toEqual(expected)
  })

  it('lists every column of a file under the header the file uses', () => {
    // Arrange — the JSON names are camel-cased; the headers are the row class's property names
    const manifest =
      importTemplates['product-management.release-packages'].files[1]

    // Act
    const names = manifest.columns.map((c) => c.name)

    // Assert
    expect(manifest.field).toBe('manifestFile')
    expect(manifest.label).toBe('Manifest')
    expect(names).toEqual([
      'PackageImportId',
      'ProductId',
      'VersionNumber',
      'Kind',
    ])
  })
})
