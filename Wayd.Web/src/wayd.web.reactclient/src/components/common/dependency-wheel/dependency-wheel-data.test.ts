import { DependencyHealth } from '@/src/components/types'
import { DependencyDto } from '@/src/services/wayd-api'
import {
  buildDependencyWheelData,
  matchesDependencyWheelSelection,
  NO_TEAM_KEY,
} from './dependency-wheel-data'

const team = (id: string, name: string, code: string) => ({
  id,
  key: 1,
  name,
  code,
  type: 'Team',
})

const atlas = team('t-atlas', 'Atlas', 'ATL')
const beacon = team('t-beacon', 'beacon', 'BCN')
const comet = team('t-comet', 'Comet', 'CMT')

const healthNames: Record<DependencyHealth, string> = {
  [DependencyHealth.Healthy]: 'Healthy',
  [DependencyHealth.AtRisk]: 'At Risk',
  [DependencyHealth.Unhealthy]: 'Unhealthy',
  [DependencyHealth.Unknown]: 'Unknown',
  [DependencyHealth.Closed]: 'Closed',
}

let nextId = 1
const dependency = (
  sourceTeam: ReturnType<typeof team> | undefined,
  targetTeam: ReturnType<typeof team> | undefined,
  health: DependencyHealth = DependencyHealth.Healthy,
): DependencyDto => {
  const item = (t: ReturnType<typeof team> | undefined) =>
    ({
      id: `w${nextId++}`,
      key: `W-${nextId}`,
      title: 'Item',
      workspaceKey: 'W',
      type: 'Story',
      status: 'New',
      statusCategory: { id: 1, name: 'Proposed' },
      team: t,
    }) as DependencyDto['source']
  return {
    id: `d${nextId++}`,
    source: item(sourceTeam),
    target: item(targetTeam),
    linkType: { id: 1, name: 'Dependency' },
    state: { id: 1, name: 'To Do' },
    health: { id: health, name: healthNames[health] },
    scope: { id: 2, name: 'Cross-Team' },
    createdOn: new Date(),
  }
}

describe('buildDependencyWheelData', () => {
  it('keeps No team last even when team names sort after it', () => {
    // Arrange
    const pulsar = team('t-pulsar', 'Pulsar', 'PLS')
    const quasar = team('t-quasar', 'Quasar', 'QSR')
    const dependencies = [
      dependency(undefined, quasar),
      dependency(pulsar, undefined),
    ]

    // Act
    const data = buildDependencyWheelData(dependencies)

    // Assert
    expect(data.groups.map((g) => g.key)).toEqual([
      pulsar.id,
      quasar.id,
      NO_TEAM_KEY,
    ])
  })

  it('orders teams by name, ignoring case, with No team last', () => {
    // Arrange
    const dependencies = [
      dependency(comet, undefined),
      dependency(beacon, atlas),
    ]

    // Act
    const data = buildDependencyWheelData(dependencies)

    // Assert
    expect(data.groups.map((g) => g.key)).toEqual([
      atlas.id,
      beacon.id,
      comet.id,
      NO_TEAM_KEY,
    ])
    expect(data.groups.map((g) => g.label)).toEqual([
      'ATL',
      'BCN',
      'CMT',
      'No team',
    ])
  })

  it('counts each direction separately, from predecessor team to successor team', () => {
    // Arrange
    const dependencies = [
      dependency(atlas, beacon),
      dependency(atlas, beacon),
      dependency(beacon, atlas),
    ]

    // Act
    const data = buildDependencyWheelData(dependencies)

    // Assert
    expect(data.matrix).toEqual([
      [0, 2],
      [1, 0],
    ])
    expect(data.groups).toEqual([
      expect.objectContaining({ key: atlas.id, outgoing: 2, incoming: 1 }),
      expect.objectContaining({ key: beacon.id, outgoing: 1, incoming: 2 }),
    ])
  })

  it('gives each flow the worst health among its dependencies', () => {
    // Arrange
    const dependencies = [
      dependency(atlas, beacon, DependencyHealth.Healthy),
      dependency(atlas, beacon, DependencyHealth.Unhealthy),
      dependency(atlas, beacon, DependencyHealth.AtRisk),
      dependency(beacon, atlas, DependencyHealth.Healthy),
      dependency(beacon, atlas, DependencyHealth.Unknown),
    ]

    // Act
    const data = buildDependencyWheelData(dependencies)

    // Assert
    expect(data.flows).toEqual(
      expect.arrayContaining([
        expect.objectContaining({
          sourceKey: atlas.id,
          targetKey: beacon.id,
          count: 3,
          health: DependencyHealth.Unhealthy,
        }),
        expect.objectContaining({
          sourceKey: beacon.id,
          targetKey: atlas.id,
          count: 2,
          health: DependencyHealth.Unknown,
        }),
      ]),
    )
  })

  it("ranks closed below healthy, so a flow's health describes its open dependencies", () => {
    // Arrange
    const dependencies = [
      dependency(atlas, beacon, DependencyHealth.Closed),
      dependency(atlas, beacon, DependencyHealth.Healthy),
      dependency(beacon, atlas, DependencyHealth.Closed),
    ]

    // Act
    const data = buildDependencyWheelData(dependencies)

    // Assert
    expect(data.flows).toEqual(
      expect.arrayContaining([
        expect.objectContaining({
          sourceKey: atlas.id,
          health: DependencyHealth.Healthy,
        }),
        expect.objectContaining({
          sourceKey: beacon.id,
          health: DependencyHealth.Closed,
        }),
      ]),
    )
  })

  it('leaves a dependency between two items with no team off the wheel and counts it', () => {
    // Arrange
    const dependencies = [
      dependency(undefined, undefined),
      dependency(atlas, beacon),
    ]

    // Act
    const data = buildDependencyWheelData(dependencies)

    // Assert
    expect(data.undrawn).toBe(1)
    expect(data.groups.map((g) => g.key)).toEqual([atlas.id, beacon.id])
    expect(data.flows).toHaveLength(1)
  })
  it('counts health and states per flow, and partners per team by size', () => {
    // Arrange
    const done = dependency(atlas, beacon, DependencyHealth.Healthy)
    done.state = { id: 3, name: 'Done' }
    const removed = dependency(atlas, beacon, DependencyHealth.Unhealthy)
    removed.state = { id: 4, name: 'Removed' }
    const dependencies = [
      removed,
      done,
      dependency(atlas, beacon, DependencyHealth.AtRisk),
      dependency(beacon, comet, DependencyHealth.Unhealthy),
      dependency(atlas, comet, DependencyHealth.Healthy),
      dependency(comet, atlas, DependencyHealth.Healthy),
    ]

    // Act
    const data = buildDependencyWheelData(dependencies)

    // Assert
    const atlasToBeacon = data.flows.find(
      (f) => f.sourceKey === atlas.id && f.targetKey === beacon.id,
    )!
    expect(atlasToBeacon).toEqual(
      expect.objectContaining({
        states: [
          { id: 1, name: 'To Do', count: 1 },
          { id: 3, name: 'Done', count: 1 },
          { id: 4, name: 'Removed', count: 1 },
        ],
        healthCounts: {
          [DependencyHealth.Healthy]: 1,
          [DependencyHealth.AtRisk]: 1,
          [DependencyHealth.Unhealthy]: 1,
          [DependencyHealth.Unknown]: 0,
          [DependencyHealth.Closed]: 0,
        },
      }),
    )
    const atlasGroup = data.groups.find((g) => g.key === atlas.id)!
    expect(atlasGroup.healthCounts[DependencyHealth.Healthy]).toBe(3)
    expect(atlasGroup.healthCounts[DependencyHealth.AtRisk]).toBe(1)
    expect(atlasGroup.partners).toEqual([
      { key: beacon.id, label: 'BCN', count: 3 },
      { key: comet.id, label: 'CMT', count: 2 },
    ])
  })
})

describe('matchesDependencyWheelSelection', () => {
  const atlasToBeacon = dependency(atlas, beacon)
  const beaconToAtlas = dependency(beacon, atlas)
  const cometToNoTeam = dependency(comet, undefined)

  it('matches everything when nothing is picked', () => {
    // Act / Assert
    expect(
      [atlasToBeacon, beaconToAtlas, cometToNoTeam].every((d) =>
        matchesDependencyWheelSelection(d, null),
      ),
    ).toBe(true)
  })

  it('matches a picked team at either end', () => {
    // Arrange
    const selection = { kind: 'group' as const, key: atlas.id }

    // Act
    const matches = [atlasToBeacon, beaconToAtlas, cometToNoTeam].map((d) =>
      matchesDependencyWheelSelection(d, selection),
    )

    // Assert
    expect(matches).toEqual([true, true, false])
  })

  it('matches a picked flow in its direction only', () => {
    // Arrange
    const selection = {
      kind: 'flow' as const,
      sourceKey: atlas.id,
      targetKey: beacon.id,
    }

    // Act
    const matches = [atlasToBeacon, beaconToAtlas].map((d) =>
      matchesDependencyWheelSelection(d, selection),
    )

    // Assert
    expect(matches).toEqual([true, false])
  })

  it('matches the No team group by its key', () => {
    // Arrange
    const selection = { kind: 'group' as const, key: NO_TEAM_KEY }

    // Act / Assert
    expect(matchesDependencyWheelSelection(cometToNoTeam, selection)).toBe(true)
    expect(matchesDependencyWheelSelection(atlasToBeacon, selection)).toBe(
      false,
    )
  })
})
