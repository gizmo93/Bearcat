<?php defined( 'ABSPATH' ) || exit; get_header(); ?>
<a class="back-link" href="<?php echo esc_url( home_url( '/' ) ); ?>">← Back to releases</a>
<div class="content-layout"><div>
    <?php while ( have_posts() ) : the_post(); get_template_part( 'template-parts/content' ); endwhile; ?>
    <?php the_post_navigation( array( 'prev_text' => '← %title', 'next_text' => '%title →' ) ); ?>
</div><?php get_sidebar(); ?></div>
<?php get_footer(); ?>
