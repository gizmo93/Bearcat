// @ts-check
import { defineConfig } from 'astro/config';
import starlight from '@astrojs/starlight';
import starlightThemeNova from 'starlight-theme-nova';
import mermaid from 'astro-mermaid';

const isGitHubActionsBuild = process.env.GITHUB_ACTIONS === 'true';
const base = '/Bearcat';

const redirectToPreferredLocaleScript = `
(() => {
	const storageKey = 'bearcat-docs-locale';
	const basePath = '${base}/';
	const isGermanPath = (path) => {
		const rest = path.slice(basePath.length);
		return rest === 'de' || rest.startsWith('de/');
	};
	const readStoredLocale = () => {
		try {
			return localStorage.getItem(storageKey);
		} catch {
			return null;
		}
	};
	const storeLocale = (locale) => {
		try {
			localStorage.setItem(storageKey, locale);
		} catch {}
	};

	document.addEventListener('change', (event) => {
		const select = event.target;
		if (!(select instanceof HTMLSelectElement) || !select.closest('starlight-lang-select')) return;
		storeLocale(isGermanPath(select.value) ? 'de' : 'en');
	}, true);

	const path = location.pathname;
	if (!path.startsWith(basePath) && path !== '${base}') return;
	const browserPrefersGerman = (navigator.languages?.[0] ?? navigator.language ?? '').toLowerCase().startsWith('de');
	const preferredLocale = readStoredLocale() ?? (browserPrefersGerman ? 'de' : 'en');
	const currentLocale = isGermanPath(path) ? 'de' : 'en';
	if (preferredLocale === currentLocale) return;

	const rest = path.slice(basePath.length);
	const target = preferredLocale === 'de'
		? basePath + 'de/' + rest
		: basePath + rest.replace(/^de(\\/|$)/, '');
	location.replace(target + location.search + location.hash);
})();
`;

// https://astro.build/config
export default defineConfig({
	site: 'https://gizmo93.github.io',
	base,
	integrations: [
		mermaid({
			theme: 'dark',
			autoTheme: true,
		}),
		starlight({
			title: { en: 'Bearcat Docs', de: 'Bearcat Doku' },
			defaultLocale: 'root',
			locales: {
				root: { label: 'English', lang: 'en' },
				de: { label: 'Deutsch', lang: 'de' },
			},
			description: 'Configure Bearcat for automatic FTP/FTPS downloads, RAR/7z extraction and archiving, uploads to one-click hosters, link checks, and reuploads.',
			plugins: [
				starlightThemeNova({
					nav: [
						{ label: 'Start', href: { en: '/Bearcat/', de: '/Bearcat/de/' } },
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
			head: [{ tag: 'script', content: redirectToPreferredLocaleScript }],
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
					label: 'Getting started', translations: { de: 'Erste Schritte' },
					items: [
						{ label: 'Overview', translations: { de: 'Übersicht' }, slug: 'index' },
						{ label: 'Desktop app', translations: { de: 'Desktopanwendung' }, slug: 'use-the-desktop-launcher' },
						{ label: 'Docker', slug: 'use-the-docker-image' },
						{ label: 'Windows service', translations: { de: 'Windows-Dienst' }, slug: 'use-the-windows-service' },
						{ label: 'Your first upload', translations: { de: 'Dein erster Upload' }, slug: 'post-installation' },
					],
				},
				{
					label: 'Releases and uploads', translations: { de: 'Releases und Uploads' },
					collapsed: true,
					items: [
						{ label: 'Release types', translations: { de: 'Releasetypen' }, slug: 'release-types' },
						{ label: 'Release detail page', translations: { de: 'Releasedetailseite' }, slug: 'release-detail-page' },
						{ label: 'Accounts and reuploads', translations: { de: 'Konten und Reuploads' }, slug: 'account-settings' },
						{ label: 'Upload lifecycle', translations: { de: 'Ablauf eines Uploads' }, slug: 'upload-lifecycle' },
						{ label: 'Release collections', translations: { de: 'Releasecollections' }, slug: 'release-collections' },
						{ label: 'Metadata and cover images', translations: { de: 'Metadaten und Coverbilder' }, slug: 'release-information-and-metadata' },
						{ label: 'Additional archive contents', translations: { de: 'Zusätzliche Archivinhalte' }, slug: 'additional-archive-contents' },
						{ label: 'Mirror downloads', translations: { de: 'Mirrordownloads' }, slug: 'mirror-downloads' },
					],
				},
				{
					label: 'Automation', translations: { de: 'Automatisierung' },
					collapsed: true,
					items: [
						{ label: 'Templates and folder automations', translations: { de: 'Templates und Ordnerautomatisierungen' }, slug: 'release-templates-and-automations' },
						{ label: 'FTP / FTPS downloads', translations: { de: 'FTP-/FTPS-Downloads' }, slug: 'remote-downloads' },
						{ label: 'Quality gates', translations: { de: 'Qualitätsprüfungen' }, slug: 'quality-gates' },
						{ label: 'Telegram notifications', translations: { de: 'Telegrambenachrichtigungen' }, slug: 'telegram-notifications' },
					],
				},
				{
					label: 'Forum posting', translations: { de: 'Forenposting' },
					collapsed: true,
					items: [
						{ label: 'Posting to forums', translations: { de: 'In Foren posten' }, slug: 'posting-to-forums' },
						{ label: 'Forum post templates', translations: { de: 'Forenpostvorlagen' }, slug: 'forum-post-templates' },
						{ label: 'Post queue', translations: { de: 'Postwarteschlange' }, slug: 'post-queue' },
						{ label: 'Automatic forum posting', translations: { de: 'Automatisches Forenposting' }, slug: 'automatic-forum-posting' },
					],
				},
				{
					label: 'Advanced', translations: { de: 'Erweitert' },
					collapsed: true,
					items: [
						{ label: 'Advanced configuration', translations: { de: 'Erweiterte Konfiguration' }, slug: 'advanced-configuration' },
						{ label: 'PostgreSQL (optional)', slug: 'install-postgresql-for-desktop' },
						{ label: 'Proxy servers', translations: { de: 'Proxyserver' }, slug: 'proxy-servers' },
						{ label: 'REST API', translations: { de: 'REST-API' }, slug: 'rest-api' },
						{ label: 'External orchestration', translations: { de: 'Externe Steuerung' }, slug: 'external-orchestration' },
					],
				},
				{
					label: 'Service-specific help', translations: { de: 'Hilfe zu einzelnen Diensten' },
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
