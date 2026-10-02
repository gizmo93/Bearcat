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
			description: 'Configure Bearcat for automatic FTP/FTPS downloads, RAR/7z extraction and archiving, uploads to one-click hosters, link checks, and reuploads.',
			plugins: [
				starlightThemeNova({
					nav: [
						{ label: 'Start', href: '/Bearcat/' },
						{ label: 'API', href: '/Bearcat/api/' },
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
					label: 'Getting started',
					items: [
						{ label: 'Overview', slug: 'index' },
						{ label: 'Desktop app', slug: 'use-the-desktop-launcher' },
						{ label: 'Docker', slug: 'use-the-docker-image' },
						{ label: 'Windows service', slug: 'use-the-windows-service' },
						{ label: 'Your first upload', slug: 'post-installation' },
					],
				},
				{
					label: 'Releases and uploads',
					collapsed: true,
					items: [
						{ label: 'Release types', slug: 'release-types' },
						{ label: 'Release detail page', slug: 'release-detail-page' },
						{ label: 'Accounts and reuploads', slug: 'account-settings' },
						{ label: 'Upload lifecycle', slug: 'upload-lifecycle' },
						{ label: 'Release collections', slug: 'release-collections' },
						{ label: 'Metadata and cover images', slug: 'release-information-and-metadata' },
						{ label: 'Additional archive contents', slug: 'additional-archive-contents' },
						{ label: 'Mirror downloads', slug: 'mirror-downloads' },
					],
				},
				{
					label: 'Automation',
					collapsed: true,
					items: [
						{ label: 'Templates and folder automations', slug: 'release-templates-and-automations' },
						{ label: 'FTP / FTPS downloads', slug: 'remote-downloads' },
						{ label: 'Quality gates', slug: 'quality-gates' },
						{ label: 'Telegram notifications', slug: 'telegram-notifications' },
					],
				},
				{
					label: 'Forum posting',
					collapsed: true,
					items: [
						{ label: 'Posting to forums', slug: 'posting-to-forums' },
						{ label: 'Forum post templates', slug: 'forum-post-templates' },
						{ label: 'Post queue', slug: 'post-queue' },
						{ label: 'Automatic forum posting', slug: 'automatic-forum-posting' },
					],
				},
				{
					label: 'Advanced',
					collapsed: true,
					items: [
						{ label: 'Advanced configuration', slug: 'advanced-configuration' },
						{ label: 'PostgreSQL (optional)', slug: 'install-postgresql-for-desktop' },
						{ label: 'Proxy servers', slug: 'proxy-servers' },
						{ label: 'REST API', slug: 'rest-api' },
						{ label: 'External orchestration', slug: 'external-orchestration' },
					],
				},
				{
					label: 'Service-specific help',
					collapsed: true,
					items: [
						{ label: 'Keep2Share', slug: 'keep2share' },
						{ label: 'filecrypt.cc', slug: 'filecrypt' },
					],
				},
			],
		}),
	],
});
