<?php defined( 'ABSPATH' ) || exit; ?>
<!doctype html>
<html <?php language_attributes(); ?>>
<head>
    <meta charset="<?php bloginfo( 'charset' ); ?>">
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <?php wp_head(); ?>
</head>
<body <?php body_class(); ?>>
<?php wp_body_open(); ?>
<a class="skip-link" href="#content">Skip to content</a>
<header class="site-header">
    <div class="wrap header-main">
        <a class="brand" href="<?php echo esc_url( home_url( '/' ) ); ?>">
            <span class="brand-symbol" aria-hidden="true">B</span>
            <span><span class="brand-name"><?php bloginfo( 'name' ); ?></span><span class="brand-caption">Movies. Series. Releases.</span></span>
        </a>
        <?php get_search_form(); ?>
    </div>
    <nav class="site-nav" aria-label="Main navigation"><div class="wrap nav-row">
        <?php wp_nav_menu( array( 'theme_location' => 'primary', 'container' => false, 'menu_class' => 'nav-menu', 'fallback_cb' => 'bearcat_releases_fallback_menu', 'depth' => 1 ) ); ?>
        <span class="local-status"><span class="status-dot"></span>Local development</span>
    </div></nav>
</header>
<main id="content" class="wrap site-main">
