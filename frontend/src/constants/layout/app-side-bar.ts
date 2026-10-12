export const SIDEBAR_DENSITY = {
  COMPACT: 'compact',
  DEFAULT: 'default',
  COMFORTABLE: 'comfortable',
} as const

export type SidebarDensity = (typeof SIDEBAR_DENSITY)[keyof typeof SIDEBAR_DENSITY]

export const SIDEBAR_PADDING = 16 // px per side

const ITEM_SIZE = { compact: 40, default: 48, comfortable: 56 } as const satisfies Record<
  SidebarDensity,
  number
>

export const getSidebarRailWidth = (
  sidebarDensity: SidebarDensity,
  padding: number = SIDEBAR_PADDING,
) => ITEM_SIZE[sidebarDensity] + padding * 2
