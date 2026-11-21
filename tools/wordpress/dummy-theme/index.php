<?php defined( 'ABSPATH' ) || exit; get_header(); ?>
<?php if ( is_home() ) { bearcat_releases_poster_strip(); } ?>
<?php if ( is_search() ) : ?>
    <h1 class="entry-title">Search: <?php echo esc_html( get_search_query() ); ?></h1>
<?php elseif ( is_archive() ) : ?>
    <?php the_archive_title( '<h1>', '</h1>' ); ?>
<?php else : ?>
    <h1 class="screen-reader-text">Latest releases</h1>
<?php endif; ?>
<div class="section-line"><?php echo is_search() ? 'Search results' : 'Release overview'; ?></div>
<div class="content-layout"><div>
    <?php if ( have_posts() ) : while ( have_posts() ) : the_post(); ?>
        <?php get_template_part( 'template-parts/content' ); ?>
    <?php endwhile; the_posts_pagination( array( 'prev_text' => '← Previous', 'next_text' => 'Next →' ) ); else : ?>
        <div class="release-card"><h2>No releases found</h2><p class="empty-state">Create a post or add sample releases under Appearance → Bearcat Demo.</p><?php get_search_form(); ?></div>
    <?php endif; ?>
</div><?php get_sidebar(); ?></div>
<?php get_footer(); ?>
