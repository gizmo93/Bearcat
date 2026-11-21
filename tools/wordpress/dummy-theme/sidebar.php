<?php defined( 'ABSPATH' ) || exit; ?>
<aside class="sidebar" aria-label="Sidebar">
    <section class="sidebar-box">
        <h2>Categories</h2>
        <ul class="sidebar-list">
        <?php
        $categories = get_categories( array( 'hide_empty' => true ) );
        foreach ( $categories as $category ) {
            echo '<li><a href="' . esc_url( get_category_link( $category ) ) . '">' . esc_html( $category->name ) . '<span class="count">' . esc_html( $category->count ) . '</span></a></li>';
        }
        if ( ! $categories ) {
            echo '<li>No posts yet.</li>';
        }
        ?>
        </ul>
    </section>
    <section class="sidebar-box">
        <h2>Recently added</h2>
        <ul class="sidebar-list">
        <?php foreach ( get_posts( array( 'numberposts' => 4, 'post_status' => 'publish' ) ) as $recent_post ) : ?>
            <li><a href="<?php echo esc_url( get_permalink( $recent_post ) ); ?>"><?php echo esc_html( get_the_title( $recent_post ) ); ?></a><span class="recent-date"><?php echo esc_html( get_the_date( 'M j, Y', $recent_post ) ); ?></span></li>
        <?php endforeach; ?>
        </ul>
    </section>
    <section class="sidebar-box sidebar-note">
        <span class="sidebar-kicker">Bearcat × WordPress</span>
        <h2>A place for your releases.</h2>
        <p>Test posts, cover images and automatic updates from Bearcat.</p>
    </section>
</aside>
