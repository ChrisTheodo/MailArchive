import { presetIcons, defineConfig } from 'unocss'

export default defineConfig({
  presets: [
    presetIcons({
      processor(props) {
        delete props.color
      },
    }),
  ],
})
