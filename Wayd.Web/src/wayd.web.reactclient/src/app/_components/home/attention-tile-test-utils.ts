/**
 * The inline style antd puts on a statistic's number, where an attention
 * tile's colour lands. Colour has no role to query by, so tests read it here.
 */
export const tileValueStyle = (container: HTMLElement): string =>
  container.querySelector('.ant-statistic-content')?.getAttribute('style') ?? ''
