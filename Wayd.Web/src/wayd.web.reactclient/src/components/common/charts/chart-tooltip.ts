/** G2's own class prefix, so the active chart theme styles this markup as it does its default tooltip. */
const PREFIX = 'g2-tooltip'

const XHTML = 'http://www.w3.org/1999/xhtml'

/** One row of a G2 tooltip: the series or category name, its value, and its marker color. */
export interface ChartTooltipItem {
  name?: unknown
  value?: unknown
  color?: unknown
}

/** What G2 hands a tooltip `render`: the title and rows it resolved from the chart's data and config. */
export interface ChartTooltipContent {
  title?: unknown
  items?: ChartTooltipItem[]
}

const toText = (value: unknown): string =>
  value === undefined || value === null ? '' : String(value)

/**
 * Creates an element with `createElementNS`, never `createElement`.
 *
 * `@ant-design/plots` reads the source of every function in a chart config, and one whose source
 * holds `.createElement('<tag>'` is taken for a React component and its result rendered through
 * React, which throws on hover. `createElementNS` cannot match that check, whatever a minifier inlines.
 */
const element = <K extends keyof HTMLElementTagNameMap>(
  tag: K,
  className: string,
  text?: string,
): HTMLElementTagNameMap[K] => {
  const el = document.createElementNS(XHTML, tag) as HTMLElementTagNameMap[K]
  el.className = className
  if (text !== undefined) {
    el.textContent = text
    el.title = text
  }
  return el
}

/**
 * Builds a chart tooltip's content as DOM, every title, name and value set as text.
 *
 * G2's default tooltip writes these strings into the page as HTML, and many of them are names users
 * choose — teams, strategic themes, statuses, work types — so a name must never be parsed as markup.
 * This mirrors G2's default markup, class names and empty-value rule, so it looks the same.
 */
export function renderChartTooltip(
  _event: unknown,
  { title, items = [] }: ChartTooltipContent,
): HTMLElement {
  const root = element('div', '')

  const titleText = toText(title)
  if (titleText) root.appendChild(element('div', `${PREFIX}-title`, titleText))

  const list = element('ul', `${PREFIX}-list`)
  items.forEach((item, index) => {
    const row = element('li', `${PREFIX}-list-item`)
    row.dataset.index = String(index)

    const name = element('span', `${PREFIX}-list-item-name`)
    const marker = element('span', `${PREFIX}-list-item-marker`)
    // A style property, never an attribute string: an invalid color is dropped, not parsed.
    marker.style.background = toText(item.color ?? 'black')
    name.append(
      marker,
      element('span', `${PREFIX}-list-item-name-label`, toText(item.name)),
    )

    // G2's default tooltip shows any empty value other than 0 as blank.
    const value = !item.value && item.value !== 0 ? '' : item.value
    row.append(
      name,
      element('span', `${PREFIX}-list-item-value`, toText(value)),
    )
    list.appendChild(row)
  })
  root.appendChild(list)

  return root
}

const withTextTooltipInteraction = <T extends object>(view: T): T => {
  const interaction = (view as { interaction?: Record<string, unknown> })
    .interaction
  if (interaction === undefined) return view
  const tooltip = interaction.tooltip
  if (tooltip === false) return view

  return {
    ...view,
    interaction: {
      ...interaction,
      tooltip: {
        ...(typeof tooltip === 'object' ? tooltip : {}),
        render: renderChartTooltip,
      },
    },
  }
}

/**
 * Returns the chart config with its tooltip rendered by `renderChartTooltip`.
 *
 * The render replaces any the config supplies, on the chart and on each of its `children`, because G2
 * lets a child's interaction override the chart's, so no part of a chart can fall back to G2's HTML
 * rendering. A tooltip turned off (`interaction.tooltip: false`) stays off.
 */
export function withTextTooltip<T extends object>(config: T): T {
  const { children } = config as { children?: unknown }
  const withChildren = Array.isArray(children)
    ? {
        ...config,
        children: children.map((child) =>
          child && typeof child === 'object'
            ? withTextTooltipInteraction(child)
            : child,
        ),
      }
    : config

  const interaction = (withChildren as { interaction?: unknown }).interaction
  return withTextTooltipInteraction({
    ...withChildren,
    interaction: interaction ?? {},
  })
}
