import { SprintSections, sprintSections } from './sprint-sections'

describe('sprintSections', () => {
  it('offers the sections the page had before capacity when capacity is not shown', () => {
    // Arrange / Act
    const sections = sprintSections(false)

    // Assert
    expect(sections).toEqual([
      { id: SprintSections.Overview, label: 'Overview' },
      { id: SprintSections.Backlog, label: 'Backlog' },
      { id: SprintSections.Activities, label: 'Activity' },
    ])
  })

  it('offers Capacity after the Backlog when capacity is shown', () => {
    // Arrange / Act
    const sections = sprintSections(true)

    // Assert
    expect(sections.map((s) => s.id)).toEqual([
      SprintSections.Overview,
      SprintSections.Backlog,
      SprintSections.Capacity,
      SprintSections.Activities,
    ])
  })
})
