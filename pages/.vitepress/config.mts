import { defineConfig } from 'vitepress'

export default defineConfig({
  title: "BASpark",
  description: "Official documentation for BASpark - An elegant Blue Archive style mouse effects utility.",
  base: '/',
  cleanUrls: true,

  head: [
    ['link', { rel: 'icon', href: '/logo.png' }]
  ],

  themeConfig: {
    logo: '/logo.png',
    nav: [
      { text: 'Home', link: '/' },
      { text: 'Guide', link: '/guide' },
      { text: 'FAQ', link: '/faq' },
      { text: 'About', link: '/about' }
    ],

    sidebar: [
      {
        text: 'Getting Started',
        collapsed: false,
        items: [
          { text: 'About BASpark', link: '/about' },
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
      { icon: 'github', link: 'https://github.com/DoomVoss/BASpark' },
      { icon: 'discord', link: 'https://discord.gg/sgZMdqhfZu' }
    ],

    footer: {
      message: 'Released under the MIT License.',
      copyright: 'Copyright © 2026 DoomVoss'
    }
  }
})