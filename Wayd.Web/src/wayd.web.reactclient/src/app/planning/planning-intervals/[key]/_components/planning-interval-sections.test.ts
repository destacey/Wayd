import {
  PlanningIntervalSections,
  planningIntervalSections,
} from './planning-interval-sections'

describe('planningIntervalSections', () => {
  it('offers the sections the page had before capacity when capacity is not shown', () => {
    // Arrange / Act
    const sections = planningIntervalSections(false)

    // Assert
    expect(sections.map((s) => s.id)).toEqual([
      PlanningIntervalSections.Overview,
      PlanningIntervalSections.PlanReview,
      PlanningIntervalSections.Objectives,
      PlanningIntervalSections.Risks,
      PlanningIntervalSections.Backlog,
      PlanningIntervalSections.Activities,
    ])
  })

  it('offers Capacity after the Backlog when capacity is shown', () => {
    // Arrange / Act
    const sections = planningIntervalSections(true)

    // Assert
    expect(sections.map((s) => s.id)).toEqual([
      PlanningIntervalSections.Overview,
      PlanningIntervalSections.PlanReview,
      PlanningIntervalSections.Objectives,
      PlanningIntervalSections.Risks,
      PlanningIntervalSections.Backlog,
      PlanningIntervalSections.Capacity,
      PlanningIntervalSections.Activities,
    ])
  })
})
