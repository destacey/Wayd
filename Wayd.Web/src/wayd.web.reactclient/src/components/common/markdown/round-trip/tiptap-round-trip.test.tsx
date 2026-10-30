import fs from 'node:fs'
import path from 'node:path'
import type { Editor } from '@tiptap/core'
import {
  classifyFidelity,
  fidelityOrder,
  type Fidelity,
} from './classify-fidelity'
import { roundTripCases, unstableCases } from './round-trip-cases'
import seededContent from './seeded-content.json'
import { createHeadlessMarkdownEditor, roundTrip } from './tiptap-markdown'

// jest.setup.ts stubs these globally; the classifier renders through the real MarkdownRenderer.
jest.unmock('react-markdown')
jest.unmock('remark-gfm')
jest.unmock('rehype-raw')

/**
 * Markdown round-trip harness for the Tiptap editor (#1201).
 *
 * Every case loads markdown into a headless editor with the extensions the editor ships, serializes
 * it straight back, and judges the difference by what MarkdownRenderer shows. Each expectation pins
 * today's behaviour, so a Tiptap upgrade that changes any of it fails here.
 *
 * Two environment variables widen it:
 * - ROUND_TRIP_SEED_DIR — a folder written by `wayd-data generate --out`; every long-text value in
 *   it is checked instead of the committed sample.
 * - ROUND_TRIP_REPORT — a file to write the results table to, as markdown.
 */

interface SeededValue {
  field: string
  value: string
}

/** Minimal RFC 4180 reader: quoted fields, doubled quotes, and newlines inside quotes. */
const readCsv = (text: string): Record<string, string>[] => {
  const rows: string[][] = []
  let row: string[] = []
  let field = ''
  let quoted = false
  for (let i = 0; i < text.length; i++) {
    const char = text[i]
    if (quoted) {
      if (char === '"' && text[i + 1] === '"') {
        field += '"'
        i++
      } else if (char === '"') quoted = false
      else field += char
    } else if (char === '"') quoted = true
    else if (char === ',') {
      row.push(field)
      field = ''
    } else if (char === '\n' || (char === '\r' && text[i + 1] === '\n')) {
      if (char === '\r') i++
      row.push(field)
      rows.push(row)
      row = []
      field = ''
    } else field += char
  }
  if (field || row.length) rows.push([...row, field])
  const [header, ...data] = rows
  return data.map((cells) =>
    Object.fromEntries(header.map((name, index) => [name, cells[index] ?? ''])),
  )
}

/** The committed sample, or every distinct value of the same fields in a generated seed folder. */
const loadSeededValues = (): SeededValue[] => {
  const fields = Object.keys(seededContent) as (keyof typeof seededContent)[]
  const seedDir = process.env.ROUND_TRIP_SEED_DIR
  if (!seedDir) {
    return fields.flatMap((field) =>
      seededContent[field].map((value) => ({ field, value })),
    )
  }

  return fields.flatMap((field) => {
    const [file, column] = field.split('.')
    const rows = readCsv(
      fs.readFileSync(path.join(seedDir, `${file}.csv`), 'utf8'),
    )
    const values = new Set(rows.map((row) => row[column]).filter(Boolean))
    return [...values].map((value) => ({ field, value }))
  })
}

const seededValues = loadSeededValues()

interface Outcome {
  source: 'hand-written' | 'seeded'
  name: string
  input: string
  output: string
  fidelity: Fidelity
  handling?: string
  note?: string
}

const outcomes: Outcome[] = []

let editor: Editor

beforeAll(() => {
  editor = createHeadlessMarkdownEditor()
})

afterAll(() => {
  editor.destroy()
  const reportPath = process.env.ROUND_TRIP_REPORT
  if (reportPath) fs.writeFileSync(reportPath, renderReport(outcomes))
})

describe('Tiptap markdown round trip', () => {
  describe('hand-written cases', () => {
    it.each(roundTripCases)('$name is $expected', (testCase) => {
      // Arrange
      const { markdown, expected } = testCase

      // Act
      const result = roundTrip(editor, markdown)
      const fidelity = classifyFidelity(markdown, result.output)

      // Assert
      outcomes.push({
        source: 'hand-written',
        name: testCase.name,
        input: markdown,
        output: result.output,
        fidelity,
        handling: testCase.handling,
        note: testCase.note,
      })
      expect(fidelity).toBe(expected)
    })

    // Opening a record parses its text to a document; the editor can only tell "nothing was
    // edited" if serializing that document and parsing the result gives the same document back.
    it.each(
      roundTripCases.filter((testCase) => !unstableCases.has(testCase.name)),
    )('$name settles after one round trip', ({ markdown }) => {
      // Arrange / Act
      const result = roundTrip(editor, markdown)

      // Assert
      expect(result.reparsedDocument).toEqual(result.document)
      expect(result.secondOutput).toBe(result.output)
    })

    it('rewrites a fence containing a fence on every pass', () => {
      // Arrange
      const markdown = roundTripCases.find((testCase) =>
        unstableCases.has(testCase.name),
      )!.markdown

      // Act
      const result = roundTrip(editor, markdown)

      // Assert
      expect(result.secondOutput).not.toBe(result.output)
    })
  })

  describe('seeded content', () => {
    // Seeded text is plain prose. The only change the serializer makes to it is writing `&` as
    // `&amp;`; anything else is a regression.
    it.each(seededValues)('$field survives: $value', ({ field, value }) => {
      // Arrange
      const expected = value.replace(/&/g, '&amp;')

      // Act
      const result = roundTrip(editor, value)

      // Assert
      outcomes.push({
        source: 'seeded',
        name: field,
        input: value,
        output: result.output,
        fidelity: classifyFidelity(value, result.output),
      })
      expect(result.output).toBe(expected)
    })
  })
})

const code = (text: string): string =>
  text === ''
    ? '*(empty)*'
    : '`` ' + JSON.stringify(text).slice(1, -1).replace(/\|/g, '\\|') + ' ``'

function renderReport(all: Outcome[]): string {
  const lines: string[] = ['# Tiptap round-trip results', '']

  for (const source of ['seeded', 'hand-written'] as const) {
    const subset = all.filter((outcome) => outcome.source === source)
    lines.push(
      `## ${source} (${subset.length})`,
      '',
      '| Fidelity | Count |',
      '| --- | --- |',
    )
    for (const fidelity of fidelityOrder) {
      lines.push(
        `| ${fidelity} | ${subset.filter((o) => o.fidelity === fidelity).length} |`,
      )
    }
    lines.push('')
  }

  lines.push(
    '## Hand-written cases',
    '',
    '| Case | Fidelity | Handling | Input | Output | Note |',
    '| --- | --- | --- | --- | --- | --- |',
  )
  const handWritten = all
    .filter((outcome) => outcome.source === 'hand-written')
    .sort(
      (a, b) =>
        fidelityOrder.indexOf(b.fidelity) - fidelityOrder.indexOf(a.fidelity),
    )
  for (const o of handWritten) {
    lines.push(
      `| ${o.name} | ${o.fidelity} | ${o.handling} | ${code(o.input)} | ${code(o.output)} | ${o.note ?? ''} |`,
    )
  }

  const seededChanged = all.filter(
    (outcome) => outcome.source === 'seeded' && outcome.fidelity !== 'exact',
  )
  lines.push('', '## Seeded values that changed (first 10)', '')
  for (const o of seededChanged.slice(0, 10)) {
    lines.push(`- ${o.name}: ${code(o.input)} → ${code(o.output)}`)
  }
  return lines.join('\n') + '\n'
}
