import { homeContext } from './home-context-test-utils'
import { Involvement, involvementPlacement } from './home-involvement'
import {
  arrangeWidgets,
  HomeWidget,
  WidgetAccess,
  WidgetColumn,
} from './widget-registry'

const everything: WidgetAccess = {
  hasPermissionClaim: () => true,
  isFeatureEnabled: () => true,
  hasLinkedEmployee: true,
}

const widget = (
  id: string,
  defaultColumn: WidgetColumn,
  defaultOrder: number,
  involvement?: HomeWidget['involvement'],
): HomeWidget => ({
  id,
  title: id,
  description: '',
  defaultColumn,
  defaultOrder,
  requires: {},
  Component: () => null,
  involvement,
})

const ids = (columns: Record<WidgetColumn, HomeWidget[]>) => ({
  main: columns.main.map((w) => w.id),
  side: columns.side.map((w) => w.id),
})

// A shared widget with no rule, one per area, and a side-column widget a team member leads.
const widgets = [
  widget('shared', 'main', 10),
  widget('projects', 'main', 20, (c) =>
    c.projects.managerCount > 0
      ? Involvement.Leads
      : c.projects.totalCount > 0
        ? Involvement.Involved
        : Involvement.None,
  ),
  widget('risks', 'main', 30, (c) =>
    c.assignedRiskCount > 0 ? Involvement.Involved : Involvement.None,
  ),
  widget('context', 'side', 10),
  widget('team', 'side', 20, (c) =>
    c.teams.teamCount > 0 ? Involvement.Leads : Involvement.None,
  ),
]

describe('involvementPlacement', () => {
  it('keeps the registry defaults while the context is unknown', () => {
    // Arrange
    const place = involvementPlacement(widgets, undefined)

    // Act
    const columns = arrangeWidgets(widgets, everything, place)

    // Assert
    expect(ids(columns)).toEqual({
      main: ['shared', 'projects', 'risks'],
      side: ['context', 'team'],
    })
  })

  it('keeps the registry defaults for an account with no linked employee', () => {
    // Arrange
    const place = involvementPlacement(widgets, {
      ...homeContext({ teams: { teamCount: 3 } }),
      hasLinkedEmployee: false,
    })

    // Act
    const columns = arrangeWidgets(widgets, everything, place)

    // Assert
    expect(ids(columns).side).toEqual(['context', 'team'])
  })

  it('leads the main column with a team member’s team, and sinks the areas they are not in', () => {
    // Arrange
    const place = involvementPlacement(
      widgets,
      homeContext({ teams: { teamCount: 1 } }),
    )

    // Act
    const columns = arrangeWidgets(widgets, everything, place)

    // Assert
    expect(ids(columns)).toEqual({
      main: ['team', 'shared', 'projects', 'risks'],
      side: ['context'],
    })
  })

  it('leads with the projects of someone who manages them', () => {
    // Arrange
    const place = involvementPlacement(
      widgets,
      homeContext({ projects: { totalCount: 2, managerCount: 2 } }),
    )

    // Act
    const columns = arrangeWidgets(widgets, everything, place)

    // Assert
    expect(ids(columns).main).toEqual(['projects', 'shared', 'risks'])
  })

  it('interleaves several areas by rank, ties in registry order', () => {
    // Arrange
    const place = involvementPlacement(
      widgets,
      homeContext({
        teams: { teamCount: 1 },
        projects: { totalCount: 1, managerCount: 1 },
        assignedRiskCount: 2,
      }),
    )

    // Act
    const columns = arrangeWidgets(widgets, everything, place)

    // Assert
    expect(ids(columns)).toEqual({
      main: ['projects', 'team', 'risks', 'shared'],
      side: ['context'],
    })
  })

  it('ranks working on an area below leading one but above shared context', () => {
    // Arrange
    const place = involvementPlacement(
      widgets,
      homeContext({ projects: { totalCount: 1, memberCount: 1 } }),
    )

    // Act
    const columns = arrangeWidgets(widgets, everything, place)

    // Assert
    expect(ids(columns).main).toEqual(['projects', 'shared', 'risks'])
  })
})
