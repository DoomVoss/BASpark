import { defineConfig } from 'vitepress'

export default defineConfig({
  title: "BASpark",
  description: "Official documentation for BASpark - An elegant Blue Archive style mouse effects utility.",
  base: '/',

  themeConfig: {
    nav: [
      { text: 'Home', link: '/' },
      { text: 'Guide', link: '/guide' },
      { text: 'FAQ', link: '/faq' }
    ],

    sidebar: [
      {
        text: 'Getting Started',
        collapsed: false,
        items: [
          { text: 'Software Overview', link: '/guide#software-overview' },
          { text: 'Installation & Uninstallation', link: '/guide#installation--uninstallation' }
        ]
      },
      {
        text: 'Core Features',
        collapsed: false,
        items: [
          { text: 'Basic Settings', link: '/guide#basic-settings' },
          { text: 'Visual Adjustments', link: '/guide#visual-adjustments' },
          { text: 'Environment Filter', link: '/guide#intelligent-environment-filter' },
          { text: 'Multi-Screen Management', link: '/guide#multi-screen-management' }
        ]
      },
      {
        text: 'Troubleshooting',
        collapsed: false,
        items: [
          { text: 'Frequently Asked Questions', link: '/faq' },
          { text: 'Updates & Notices', link: '/guide#real-time-notices--auto-updates' }
        ]
      }
    ],

    socialLinks: [
      { icon: 'github', link: 'https://github.com/DoomVoss/BASpark' }
    ],

    footer: {
      message: 'Released under the MIT License.',
      copyright: 'Copyright © 2026 DoomVoss'
    }
  }
})