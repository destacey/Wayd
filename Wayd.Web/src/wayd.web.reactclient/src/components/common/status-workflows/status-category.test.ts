import { StatusCategory } from '@/src/services/wayd-api'
import { statusCategoryDescription } from './status-category'

describe('statusCategoryDescription', () => {
  it('describes every category', () => {
    // A category with no description leaves a tooltip empty, which reads as a broken control rather
    // than as nothing to say.
    // Arrange / Act / Assert
    for (const category of Object.values(StatusCategory)) {
      expect(statusCategoryDescription(category)).not.toBe('')
    }
  })

  it('distinguishes done from removed', () => {
    // The two terminal categories are the pair a reader most needs told apart: both mean the record
    // has stopped moving, and only one means it succeeded.
    // Arrange / Act
    const done = statusCategoryDescription(StatusCategory.Done)
    const removed = statusCategoryDescription(StatusCategory.Removed)

    // Assert
    expect(done).not.toBe(removed)
    expect(removed).toMatch(/not completed/i)
  })
})
