import { toFileName } from './file-name'

describe('toFileName', () => {
  it('kebab-cases a record name', () => {
    // Act & Assert
    expect(toFileName('Onboarding Fulfillment')).toBe('onboarding-fulfillment')
  })

  it('collapses punctuation a file name cannot carry', () => {
    // Act & Assert
    // "Trust & Safety Reporting" would otherwise reach the disk with an ampersand a shell splits on.
    expect(toFileName('Trust & Safety Reporting')).toBe(
      'trust-safety-reporting',
    )
  })

  it('leaves no leading or trailing dash', () => {
    // Act & Assert
    expect(toFileName('  Billing (Portal)  ')).toBe('billing-portal')
  })
})
