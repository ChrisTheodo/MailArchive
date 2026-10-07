import { presetIcons, defineConfig } from 'unocss'
import { aliases } from 'vuetify/iconsets/mdi-unocss'

export default defineConfig({
  safelist: [...Object.values(aliases)],
  content: {
    pipeline: {
      include: [/\.(vue|svelte|[jt]sx|mdx?|astro|elm|php|phtml|html)($|\?)/, 'src/**/*.{js,ts}'],
    },
  },
  presets: [
    presetIcons({
      processor(props) {
        delete props.color
      },
      collections: {
        mdi: () =>
          import('@iconify-json/mdi/icons.json', { with: { type: 'json' } }).then((i) => i.default),
      },
    }),
  ],
})
