import { DependencyStrength } from '@/src/services/wayd-api'
import {
  renderDependencyMapSvg,
  type DependencySvgNode,
  type DependencySvgTheme,
} from './dependency-map-svg'

const theme: DependencySvgTheme = {
  background: '#101010',
  nodeFill: '#202020',
  nodeStroke: '#303030',
  nodeText: '#f0f0f0',
  subjectFill: '#003a8c',
  subjectStroke: '#1677ff',
  groupFill: '#181818',
  groupStroke: '#303030',
  groupText: '#a0a0a0',
  hardStroke: '#cccccc',
  softStroke: '#666666',
  fontFamily: 'Segoe UI',
  fontSize: 12,
  borderRadius: 6,
}

const node = (
  id: string,
  label: string,
  x: number,
  y: number,
  overrides: Partial<DependencySvgNode> = {},
): DependencySvgNode => ({
  id,
  label,
  x,
  y,
  width: 180,
  height: 48,
  isGroup: false,
  isSubject: false,
  ...overrides,
})

/** Only the edges: the arrowhead markers in <defs> are paths too. */
const edgePaths = (svg: string) =>
  (svg.match(/<path [^>]*>/g) ?? []).filter((path) =>
    path.includes('marker-end'),
  )

const render = (
  nodes: DependencySvgNode[],
  edges: Parameters<typeof renderDependencyMapSvg>[0]['edges'] = [],
) => renderDependencyMapSvg({ nodes, edges, theme })

describe('renderDependencyMapSvg', () => {
  it('draws a rectangle and a label for every product', () => {
    // Arrange
    const nodes = [
      node('a', 'Storefront Web', 0, 0, { isSubject: true }),
      node('b', 'Identity Service', 400, 0),
    ]

    // Act
    const { svg } = render(nodes)

    // Assert
    expect(svg.match(/<rect /g)).toHaveLength(3) // the background plus one per node
    expect(svg).toContain('Storefront Web')
    expect(svg).toContain('Identity Service')
  })

  it('sizes the document to the graph plus a margin', () => {
    // Arrange
    const nodes = [
      node('a', 'Storefront Web', 0, 0),
      node('b', 'Core Platform', 400, 100),
    ]

    // Act
    const { svg, width, height } = render(nodes)

    // Assert
    // Padded on both sides, and the viewBox has to agree or the file crops when it is rasterised.
    expect(width).toBe(400 + 180 + 64)
    expect(height).toBe(100 + 48 + 64)
    expect(svg).toContain(`viewBox="0 0 ${width} ${height}"`)
  })

  it('shifts a graph laid out at negative coordinates into the document', () => {
    // Arrange
    const nodes = [
      node('a', 'Storefront Web', -300, -80),
      node('b', 'Core Platform', 0, 0),
    ]

    // Act
    const { svg } = render(nodes)

    // Assert
    // React Flow lays out around an arbitrary origin, so nothing guarantees the graph starts at 0,0;
    // unshifted, everything above or left of it would fall outside the document.
    expect(svg).not.toMatch(/(x|y)="-/)
  })

  it('dashes a soft dependency and leaves a hard one solid', () => {
    // Arrange
    const nodes = [
      node('a', 'Storefront Web', 0, 0),
      node('b', 'Identity Service', 400, 0),
      node('c', 'Mapping API', 400, 100),
    ]

    // Act
    const { svg } = render(nodes, [
      { source: 'a', target: 'b', strength: DependencyStrength.Hard },
      { source: 'a', target: 'c', strength: DependencyStrength.Soft },
    ])

    // Assert
    // Strength is the whole reason the map is worth exporting, so it has to survive the file.
    const paths = edgePaths(svg)
    expect(paths).toHaveLength(2)
    expect(paths.filter((p) => p.includes('stroke-dasharray'))).toHaveLength(1)
  })

  it('draws an edge between the facing sides of its two nodes', () => {
    // Arrange
    const nodes = [
      node('a', 'Storefront Web', 0, 0),
      node('b', 'Identity Service', 400, 0),
    ]

    // Act
    const { svg } = render(nodes, [
      { source: 'a', target: 'b', strength: DependencyStrength.Hard },
    ])

    // Assert
    // Leaves the source's right edge (32 + 180) at its middle (32 + 24) and lands on the target's left.
    const path = edgePaths(svg)[0]
    expect(path).toContain('M212,56')
    expect(path).toContain('432,56')
  })

  it('joins the sides the canvas does for an edge running back toward the subject', () => {
    // Arrange — the source sits right of its target.
    const nodes = [
      node('a', 'Identity Service', 0, 0),
      node('b', 'Directory', 400, 0),
    ]

    // Act
    const { svg } = render(nodes, [
      {
        source: 'b',
        target: 'a',
        strength: DependencyStrength.Hard,
        leavesLeft: true,
        entersRight: true,
      },
    ])

    // Assert
    // Leaves Directory's left edge (32 + 400) and lands on Identity Service's right edge (32 + 180).
    const path = edgePaths(svg)[0]
    expect(path).toContain('M432,56')
    expect(path).toContain('212,56')
  })

  it('leaves out an edge whose node is not on the map', () => {
    // Arrange — the far side of a link the per-side cap left off.
    const nodes = [node('a', 'Storefront Web', 0, 0)]

    // Act
    const { svg } = render(nodes, [
      { source: 'a', target: 'missing', strength: DependencyStrength.Hard },
    ])

    // Assert
    expect(edgePaths(svg)).toHaveLength(0)
  })

  it('draws the container box before the nodes inside it', () => {
    // Arrange
    const nodes = [
      node('group', 'Storefront', 300, 0, {
        isGroup: true,
        width: 204,
        height: 200,
      }),
      node('a', 'Storefront Web', 312, 34),
    ]

    // Act
    const { svg } = render(nodes)

    // Assert
    // Painted in order, so a box drawn after its children would cover them.
    expect(svg.indexOf('Storefront<')).toBeLessThan(
      svg.indexOf('Storefront Web'),
    )
    expect(svg).toContain('stroke-dasharray="4 4"')
  })

  it('escapes a name that would otherwise break the document', () => {
    // Arrange
    const nodes = [node('a', 'Trust & Safety <Reporting>', 0, 0)]

    // Act
    const { svg } = render(nodes)

    // Assert
    // Wrapped across two lines, so the escaping is asserted a line at a time.
    expect(svg).toContain('Trust &amp; Safety')
    expect(svg).toContain('&lt;Reporting&gt;')
  })
})
