<?php
defined( 'ABSPATH' ) || exit;

add_action( 'after_setup_theme', function () {
    add_theme_support( 'title-tag' );
    add_theme_support( 'post-thumbnails' );
    add_theme_support( 'html5', array( 'search-form', 'gallery', 'caption', 'style', 'script' ) );
    add_theme_support( 'responsive-embeds' );
    register_nav_menus( array( 'primary' => 'Main navigation' ) );
} );

add_action( 'wp_enqueue_scripts', function () {
    wp_enqueue_style( 'bearcat-releases', get_stylesheet_uri(), array(), wp_get_theme()->get( 'Version' ) );
} );

function bearcat_releases_cover_url( $post_id ) {
    $thumbnail = get_the_post_thumbnail_url( $post_id, 'medium_large' );
    if ( $thumbnail ) {
        return $thumbnail;
    }
    $processor = new WP_HTML_Tag_Processor( get_post_field( 'post_content', $post_id ) );
    while ( $processor->next_tag( 'IMG' ) ) {
        $src = $processor->get_attribute( 'src' );
        if ( is_string( $src ) && $src !== '' ) {
            return $src;
        }
    }
    return '';
}

function bearcat_releases_fallback_menu() {
    echo '<ul class="nav-menu"><li><a href="' . esc_url( home_url( '/' ) ) . '">All releases</a></li>';
    $categories = get_categories( array( 'hide_empty' => true, 'parent' => 0, 'number' => 5, 'exclude' => array( (int) get_option( 'default_category' ) ) ) );
    foreach ( $categories as $category ) {
        echo '<li><a href="' . esc_url( get_category_link( $category ) ) . '">' . esc_html( $category->name ) . '</a></li>';
    }
    echo '</ul>';
}

function bearcat_releases_poster_strip() {
    $recent = get_posts( array( 'numberposts' => 6, 'post_status' => 'publish', 'ignore_sticky_posts' => true ) );
    if ( ! $recent ) {
        return;
    }
    echo '<nav class="poster-strip" aria-label="Latest releases">';
    foreach ( $recent as $release_post ) {
        $cover = bearcat_releases_cover_url( $release_post->ID );
        echo '<a class="poster-tile' . ( $cover ? ' has-art' : '' ) . '" href="' . esc_url( get_permalink( $release_post ) ) . '" aria-label="' . esc_attr( get_the_title( $release_post ) ) . '">';
        if ( $cover ) {
            echo '<img src="' . esc_url( $cover ) . '" alt="" width="300" height="450" loading="lazy">';
        } else {
            echo '<span class="poster-placeholder" aria-hidden="true">B</span>';
        }
        echo '<span class="poster-caption">' . esc_html( get_the_title( $release_post ) ) . '</span></a>';
    }
    echo '</nav>';
}
