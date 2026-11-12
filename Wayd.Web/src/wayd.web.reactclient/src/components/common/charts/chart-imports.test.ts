import { readdirSync, readFileSync } from 'node:fs'
import { join, relative, sep } from 'node:path'

const clientRoot = join(__dirname, '../../../..')
const chartsModule = join('src', 'components', 'common', 'charts') + sep

const sourceFiles = (dir: string): string[] =>
  readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    const path = join(dir, entry.name)
    if (entry.isDirectory()) return sourceFiles(path)
    return /\.tsx?$/.test(entry.name) && !/\.test\.tsx?$/.test(entry.name)
      ? [path]
      : []
  })

const withoutComments = (source: string) =>
  source.replace(/\/\*[\s\S]*?\*\//g, '').replace(/^\s*\/\/.*$/gm, '')

// The packages that render G2's HTML tooltip, at any path; the same set lint restricts.
const chartPackage = String.raw`(?:@ant-design/(?:charts|plots)|@antv/(?:g2|g2plot))(?:/[^'"\x60]*)?`
const dynamicImport = new RegExp(
  String.raw`import\s*\(\s*['"\x60]${chartPackage}['"\x60]`,
)
const staticImport = new RegExp(
  String.raw`\b(?:import|export)\s+([^'"\x60;]*?)\s*from\s*['"]${chartPackage}['"]`,
  'g',
)

/** True for `import type …` and for a braced list in which every name is marked `type`. */
const isTypeOnly = (clause: string) =>
  /^type\b/.test(clause) ||
  /^\{\s*type\s+\w+(?:\s+as\s+\w+)?(?:\s*,\s*type\s+\w+(?:\s+as\s+\w+)?)*\s*,?\s*\}$/.test(
    clause,
  )

const loadsChartPackage = (source: string) => {
  const code = withoutComments(source)
  return (
    dynamicImport.test(code) ||
    [...code.matchAll(staticImport)].some(([, clause]) => !isTypeOnly(clause))
  )
}

describe('chart imports', () => {
  it('recognises the ways a module can load a chart package', () => {
    // Arrange
    const loading = [
      "import { Pie } from '@ant-design/charts'",
      "import { Line } from '@ant-design/plots'",
      "import { Column } from '@ant-design/charts/es/index'",
      "import { Chart } from '@antv/g2'",
      "export { Area } from '@ant-design/charts'",
      "import { type PieConfig, Pie } from '@ant-design/charts'",
      "const load = () => import('@ant-design/charts')",
      'const load = () => import(`@ant-design/plots`)',
    ]
    const notLoading = [
      "import type { PieConfig } from '@ant-design/charts'",
      "import { type PieConfig } from '@ant-design/charts'",
      "// import('@ant-design/charts')",
      "import { OrganizationChart } from '@ant-design/graphs'",
      "import { Pie } from '@/src/components/common/charts'",
    ]

    // Act
    const flaggedLoading = loading.filter(loadsChartPackage)
    const flaggedNotLoading = notLoading.filter(loadsChartPackage)

    // Assert
    expect(flaggedLoading).toEqual(loading)
    expect(flaggedNotLoading).toEqual([])
  })

  it('loads every chart through the shared charts module', () => {
    // Arrange
    const files = sourceFiles(join(clientRoot, 'src'))
      .map((file) => relative(clientRoot, file))
      .filter((file) => !file.startsWith(chartsModule))

    // Act
    const bypassing = files.filter((file) =>
      loadsChartPackage(readFileSync(join(clientRoot, file), 'utf8')),
    )

    // Assert
    expect(files.length).toBeGreaterThan(0)
    expect(bypassing).toEqual([])
  })
})
