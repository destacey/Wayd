import { ThemeName } from '@/src/components/contexts/theme/types'
import { TimeLineStyles } from './theme-preset'
import { lightTimeLineColors } from './light-theme'
import { darkTimeLineColors } from './dark-theme'
import { slateTimeLineColors } from './slate-theme'
import { cartoonThemeConfig } from './cartoon-theme'
import { shadcnTimeLineColors } from './shadcn-theme'
import { glassTimeLineStyles } from './glass-theme'
import { geekTimeLineStyles } from './geek-theme'

export const timeLineColorsByTheme: Record<ThemeName, TimeLineStyles> = {
  light: lightTimeLineColors,
  dark: darkTimeLineColors,
  slate: slateTimeLineColors,
  cartoon: cartoonThemeConfig.timeLineColors,
  shadcn: shadcnTimeLineColors,
  glass: glassTimeLineStyles,
  geek: geekTimeLineStyles,
}

