/** Static brand values used across light theme, PWA manifest, and meta tags. */
export class ThemeConstants {
  /** Primary brand color — the single source of truth for colorPrimary. */
  static readonly COLOR_PRIMARY = '#2196f3'
}

/**
 * Fixed widths for the record page rails. Layout constants rather than colors:
 * they have no Ant Design equivalent, and every theme must lay these out the
 * same way, so they are not part of the per-theme token set.
 *
 * See docs/contributing/record-pages.mdx.
 */
export class RecordLayoutConstants {
  /** Section rail width — fits "Dependency Management" on one line. */
  static readonly SECTION_RAIL_WIDTH = 190

  /** Record facts panel default width — fits a full email address at 14px unbroken. */
  static readonly FACTS_RAIL_WIDTH = 296

  /** Narrow enough to still show a label and its value on separate lines. */
  static readonly FACTS_RAIL_MIN_WIDTH = 220

  /** Past this the panel starts competing with the section content it flanks. */
  static readonly FACTS_RAIL_MAX_WIDTH = 560
}

/**
 * Layout constants for `ListDetailLayout` — a list with the selected
 * row's details in a panel beside it, rather than on a page of its own.
 */
export class ListDetailConstants {
  /** Default detail panel width. Wider than the facts rail: this panel is the
   *  record, not reference material beside one. */
  static readonly PANEL_WIDTH = 340

  /** Narrow enough to still show a label and its value on separate lines. */
  static readonly PANEL_MIN_WIDTH = 260

  /** Past this the panel starts crowding out the list it is meant to serve. */
  static readonly PANEL_MAX_WIDTH = 620
}
