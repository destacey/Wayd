import {
  AUTOSIZE_MAX_WIDTH,
  AUTOSIZE_MIN_WIDTH,
  computeAutosizeWidth,
  measureColumnContent,
} from './column-autosize'

describe('column-autosize', () => {
  describe('computeAutosizeWidth', () => {
    it('falls back to the min width when nothing measured', () => {
      // Arrange / Act
      const width = computeAutosizeWidth({
        maxCellContentWidth: 0,
        headerContentWidth: 0,
      })

      // Assert
      expect(width).toBe(AUTOSIZE_MIN_WIDTH)
    })

    it('sizes to the widest cell plus the cell allowance', () => {
      // Arrange / Act — cell 200 + 18 beats header 50 + 76
      const width = computeAutosizeWidth({
        maxCellContentWidth: 200,
        headerContentWidth: 50,
      })

      // Assert
      expect(width).toBe(218)
    })

    it('sizes to the header plus the header allowance when it is wider', () => {
      // Arrange / Act — header 200 + 76 beats cell 100 + 18
      const width = computeAutosizeWidth({
        maxCellContentWidth: 100,
        headerContentWidth: 200,
      })

      // Assert
      expect(width).toBe(276)
    })

    it('rounds fractional measurements up', () => {
      // Arrange / Act
      const width = computeAutosizeWidth({
        maxCellContentWidth: 100.4,
        headerContentWidth: 0,
      })

      // Assert
      expect(width).toBe(119)
    })

    it('clamps to the default max width', () => {
      // Arrange / Act
      const width = computeAutosizeWidth({
        maxCellContentWidth: 5000,
        headerContentWidth: 0,
      })

      // Assert
      expect(width).toBe(AUTOSIZE_MAX_WIDTH)
    })

    it('respects the column def min/max over the defaults', () => {
      // Arrange / Act / Assert
      expect(
        computeAutosizeWidth({
          maxCellContentWidth: 0,
          headerContentWidth: 0,
          minWidth: 120,
        }),
      ).toBe(120)
      expect(
        computeAutosizeWidth({
          maxCellContentWidth: 5000,
          headerContentWidth: 0,
          maxWidth: 250,
        }),
      ).toBe(250)
    })
  })

  describe('measureColumnContent', () => {
    let headerRoot: HTMLElement
    let bodyRoot: HTMLElement
    let rectSpy: jest.SpyInstance

    beforeEach(() => {
      // jsdom has no layout — report a width derived from the text so the
      // grouping/max logic is observable (10px per character).
      rectSpy = jest
        .spyOn(Element.prototype, 'getBoundingClientRect')
        .mockImplementation(function (this: Element) {
          return { width: (this.textContent ?? '').length * 10 } as DOMRect
        })

      const host = document.createElement('div')
      headerRoot = document.createElement('div')
      headerRoot.innerHTML =
        '<table><thead><tr>' +
        '<th data-column-id="name"><span data-column-header-text="">Name</span></th>' +
        '<th data-column-id="team"><span data-column-header-text="">Team</span></th>' +
        '</tr></thead></table>'
      bodyRoot = document.createElement('div')
      bodyRoot.innerHTML =
        '<table><tbody>' +
        '<tr><td data-column-id="name">Wolverine</td><td data-column-id="team">A</td></tr>' +
        '<tr><td data-column-id="name">Ant</td><td data-column-id="team">Falcons</td></tr>' +
        '</tbody></table>'
      host.append(headerRoot, bodyRoot)
      document.body.appendChild(host)
    })

    afterEach(() => {
      rectSpy.mockRestore()
      document.body.innerHTML = ''
    })

    it('measures the header label and the widest rendered cell per column', () => {
      // Arrange / Act
      const measured = measureColumnContent(headerRoot, bodyRoot, [
        'name',
        'team',
      ])

      // Assert — max cell wins per column ('Wolverine' = 90, 'Falcons' = 70)
      expect(measured.get('name')).toEqual({
        headerContentWidth: 40,
        maxCellContentWidth: 90,
      })
      expect(measured.get('team')).toEqual({
        headerContentWidth: 40,
        maxCellContentWidth: 70,
      })
    })

    it('ignores columns that were not requested', () => {
      // Arrange / Act
      const measured = measureColumnContent(headerRoot, bodyRoot, ['name'])

      // Assert
      expect(Array.from(measured.keys())).toEqual(['name'])
    })

    it('reports zeros for a column with no rendered DOM', () => {
      // Arrange / Act
      const measured = measureColumnContent(headerRoot, bodyRoot, ['missing'])

      // Assert
      expect(measured.get('missing')).toEqual({
        headerContentWidth: 0,
        maxCellContentWidth: 0,
      })
    })

    it('removes the off-screen measurer when done', () => {
      // Arrange / Act
      measureColumnContent(headerRoot, bodyRoot, ['name'])

      // Assert — no stray absolutely-positioned measurement nodes left behind
      expect(
        bodyRoot.parentElement?.querySelectorAll(':scope > div[style]'),
      ).toHaveLength(0)
    })
  })
})
