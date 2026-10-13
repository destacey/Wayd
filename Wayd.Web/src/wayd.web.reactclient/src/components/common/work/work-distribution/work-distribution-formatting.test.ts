import { theme } from 'antd'
import { getLuminance } from '@/src/utils'
import {
  WorkDistributionDimension,
  WorkDistributionGroupDto,
  WorkDistributionGroupKind,
} from '@/src/services/wayd-api'
import {
  WorkDistributionPaletteTokens,
  describeGroup,
  formatAmount,
  formatShare,
  groupHref,
  groupLabel,
  groupSwatches,
} from './work-distribution-formatting'

const lightToken: WorkDistributionPaletteTokens = theme.getDesignToken()
const darkToken: WorkDistributionPaletteTokens = theme.getDesignToken({
  algorithm: theme.darkAlgorithm,
})

const group = (
  overrides: Partial<WorkDistributionGroupDto>,
): WorkDistributionGroupDto => ({
  id: 'g',
  kind: WorkDistributionGroupKind.Record,
  name: 'Group',
  projectKeys: [],
  programCount: 0,
  items: 0,
  storyPoints: 0,
  filledStoryPoints: 0,
  value: 0,
  share: 0,
  ...overrides,
})

const records = (count: number) =>
  Array.from({ length: count }, (_, i) => group({ id: `g${i}` }))

describe('groupSwatches', () => {
  it('colors records in palette order and keeps the gap groups out of it', () => {
    const swatches = groupSwatches(
      [
        group({ id: 'a' }),
        group({ id: 'missing', kind: WorkDistributionGroupKind.MissingLevel }),
        group({ id: 'b' }),
        group({ id: 'none', kind: WorkDistributionGroupKind.NoProject }),
      ],
      lightToken,
    )

    expect(swatches[0].background).toBe(lightToken.blue7)
    expect(swatches[0].ink).toBe(lightToken.colorTextLightSolid)
    expect(swatches[1].background).toBe(lightToken.colorFill)
    expect(swatches[2].background).toBe(lightToken.blue4)
    expect(swatches[2].ink).toBe(lightToken.colorText)
    expect(swatches[3].background).toContain('repeating-linear-gradient')
  })

  it.each([
    ['light', lightToken],
    ['dark', darkToken],
  ])('keeps every record color clear of the %s background', (_, token) => {
    const background = getLuminance(token.colorBgContainer)

    for (const swatch of groupSwatches(records(10), token)) {
      expect(
        Math.abs(getLuminance(swatch.background) - background),
      ).toBeGreaterThanOrEqual(0.25)
    }
  })

  it('keeps dark text dark on a light swatch in the dark theme', () => {
    const [, lighter] = groupSwatches(records(2), darkToken)

    expect(getLuminance(lighter.background)).toBeGreaterThanOrEqual(0.55)
    expect(lighter.ink).toBe(darkToken.colorBgContainer)
  })
})

describe('describeGroup', () => {
  it('summarises a portfolio by its programs and projects', () => {
    const portfolio = group({ programCount: 1, projectKeys: ['P1', 'P2'] })

    expect(describeGroup(portfolio, WorkDistributionDimension.Portfolio)).toBe(
      '1 program · 2 projects',
    )
  })

  it('places a project in its portfolio and program', () => {
    const project = group({
      portfolio: { id: 'p', key: 1, name: 'Customer Experience' },
      program: { id: 'g', key: 2, name: 'Digital Onboarding' },
    })

    expect(describeGroup(project, WorkDistributionDimension.Project)).toBe(
      'Customer Experience · Digital Onboarding',
    )
  })

  it('names the projects in a missing level, shortening a long list', () => {
    const missing = group({
      kind: WorkDistributionGroupKind.MissingLevel,
      projectKeys: ['A', 'B', 'C', 'D', 'E', 'F'],
    })

    expect(describeGroup(missing, WorkDistributionDimension.Program)).toBe(
      'Projects directly in a portfolio: A, B, C, D +2 more',
    )
    expect(
      describeGroup(missing, WorkDistributionDimension.StrategicTheme),
    ).toBe('Projects with no theme: A, B, C, D +2 more')
  })

  it('explains the no project group in every dimension', () => {
    const none = group({ kind: WorkDistributionGroupKind.NoProject })

    expect(describeGroup(none, WorkDistributionDimension.StrategicTheme)).toBe(
      'Neither the item nor its parents link to a project',
    )
  })
})

describe('groupHref and groupLabel', () => {
  it('links a record group to its page', () => {
    const project = group({ recordKey: 'ATLAS' })

    expect(groupHref(project, WorkDistributionDimension.Project)).toBe(
      '/ppm/projects/ATLAS',
    )
    expect(groupLabel(project, WorkDistributionDimension.Project)).toBe(
      'ATLAS · Group',
    )
  })

  it('does not link a work type or a gap group', () => {
    expect(
      groupHref(group({ recordKey: 'x' }), WorkDistributionDimension.WorkType),
    ).toBeUndefined()
    expect(
      groupHref(
        group({ kind: WorkDistributionGroupKind.NoProject }),
        WorkDistributionDimension.Portfolio,
      ),
    ).toBeUndefined()
  })
})

describe('formatting numbers', () => {
  it('shows small shares to one decimal and large ones whole', () => {
    expect(formatShare(3.25)).toBe('3.3%')
    expect(formatShare(38.12)).toBe('38%')
    expect(formatShare(0)).toBe('0%')
  })

  it('keeps whole amounts whole and rounds split ones', () => {
    expect(formatAmount(12)).toBe('12')
    expect(formatAmount(1.25)).toBe('1.3')
  })
})
