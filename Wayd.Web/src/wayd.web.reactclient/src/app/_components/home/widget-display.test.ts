import { recordWidgetState, widgetForm, WidgetState } from './widget-display'
import { EmptyDisplay } from './widget-settings'

describe('widgetForm', () => {
  it.each<[WidgetState | undefined, EmptyDisplay, boolean, string]>([
    ['empty', 'explain', false, 'full'],
    ['empty', 'line', false, 'line'],
    ['empty', 'hide', false, 'hidden'],
    ['empty', 'hide', true, 'full'],
    // Shown hidden widgets change nothing for one drawn as a line.
    ['empty', 'line', true, 'line'],
  ])(
    'draws an %s widget set to %s (revealed: %s) as %s',
    (state, emptyDisplay, revealed, expected) => {
      // Act
      const form = widgetForm(state, emptyDisplay, revealed)

      // Assert
      expect(form).toBe(expected)
    },
  )

  it.each<WidgetState | undefined>(['loading', 'failed', 'rows', undefined])(
    'draws a widget in full under every setting while %s',
    (state) => {
      // Act
      const forms = (['explain', 'line', 'hide'] as const).map((d) =>
        widgetForm(state, d, false),
      )

      // Assert
      expect(forms).toEqual(['full', 'full', 'full'])
    },
  )
})

describe('recordWidgetState', () => {
  it('records a changed state', () => {
    // Act
    const states = recordWidgetState(
      { a: 'loading' },
      { widgetId: 'a', state: 'empty' },
    )

    // Assert
    expect(states).toEqual({ a: 'empty' })
  })

  it('keeps the same record for an identical report, so the page does not render again', () => {
    // Arrange
    const states = { a: 'empty' as const }

    // Act
    const result = recordWidgetState(states, { widgetId: 'a', state: 'empty' })

    // Assert
    expect(result).toBe(states)
  })
})
