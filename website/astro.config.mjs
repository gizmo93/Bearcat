// @ts-check
import { defineConfig } from 'astro/config';
import starlight from '@astrojs/starlight';
import starlightThemeNova from 'starlight-theme-nova';
import mermaid from 'astro-mermaid';

const isGitHubActionsBuild = process.env.GITHUB_ACTIONS === 'true';

// https://astro.build/config
export default defineConfig({
	site: 'https://gizmo93.github.io',
	base: '/Bearcat',
	integrations: [
		mermaid({
			theme: 'dark',
			autoTheme: true,
		}),
		starlight({
			title: 'Bearcat Docs',
			description: 'User documentation for Bearcat.',
			plugins: [
				starlightThemeNova({
					nav: [
						{ label: 'Start', href: '/Bearcat/' },
						{ label: 'GitHub', href: 'https://github.com/gizmo93/Bearcat' },
					],
					stylingSystem: 'css',
				}),
			],
			logo: {
				src: './src/assets/bearcat-logo.png',
				alt: 'Bearcat',
			},
			favicon: '/favicon.png',
			social: [{ icon: 'github', label: 'GitHub', href: 'https://github.com/gizmo93/Bearcat' }],
			...(isGitHubActionsBuild
				? {}
				: {
						editLink: {
							baseUrl: 'https://github.com/gizmo93/Bearcat/edit/main/website/',
						},
					}),
			sidebar: [
				{
					label: 'Start',
					items: [
						{ label: 'Overview', slug: 'index' },
					],
				},
				{
					label: 'Installation',
					items: [
						{
							label: 'Desktop App',
							items: [
								{ label: 'Overview', slug: 'use-the-desktop-launcher' },
								{ label: 'PostgreSQL for Desktop', slug: 'install-postgresql-for-desktop' },
							],
						},
						{ label: 'Windows Service', slug: 'use-the-windows-service' },
						{ label: 'Docker Container', slug: 'use-the-docker-image' },
					],  
				},
                {
                    label: 'First Steps',
                    items: [
                        { label: 'Initial Setup', slug: 'post-installation' },
                        { label: 'Release Types', slug: 'release-types' },
                        { label: 'Release Collections', slug: 'release-collections' },
                        { label: 'The upload lifecycle', slug: 'upload-lifecycle' },

                    ],
                },
				{
					label: 'Advanced',
					items: [
						{ label: 'Advanced Configuration', slug: 'advanced-configuration' },
						{ label: 'Forum Post Templates', slug: 'forum-post-templates' },
					],
				},
                {
                    label: 'Special hoster related topics',
                    items: [
                        { label: 'Keep2Share', slug: 'keep2share' }
                    ],
                },
                {
                    label: 'Special link crypter related topics',
                    items: [
                        { label: 'filecrypt.cc', slug: 'filecrypt' }
                    ],
                },
			],
		}),
	],
});
